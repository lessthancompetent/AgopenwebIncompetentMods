#!/usr/bin/env python3
"""AgOpenWeb application-record ingest (Pi server).

Watches the Syncthing-mirrored Fields tree for per-job coverage.geojson files
(written by the app on field close) and field.geojson boundaries, and maintains
a SQLite database over them. The FILES are the archival record; this database
is a rebuildable index — delete coverage.db at any time and it repopulates on
the next scan.

Stdlib only (no pip installs on the Pi). Geometry is stored as GeoJSON text
plus computed bbox/area columns; the viewer serves it straight back out, so no
SpatiaLite native extension is needed at this scale.

Usage:
    ingest.py --data /srv/agdata/fields --db /srv/agdata/coverage.db [--once]

Run continuously via the provided agdata-ingest.service (systemd). --once runs
a single scan and exits (cron-friendly, and used for testing).
"""

import argparse
import json
import os
import sqlite3
import sys
import time
from pathlib import Path

SCAN_SECS = 60

SCHEMA = """
CREATE TABLE IF NOT EXISTS files (
    path TEXT PRIMARY KEY,
    mtime REAL NOT NULL,
    size INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS applications (
    field TEXT NOT NULL,
    job TEXT NOT NULL,
    product TEXT NOT NULL DEFAULT '',
    rate REAL NOT NULL DEFAULT 0,
    rate_unit TEXT NOT NULL DEFAULT '',
    work_type TEXT NOT NULL DEFAULT '',
    worked_ha REAL NOT NULL DEFAULT 0,
    tool_width_m REAL NOT NULL DEFAULT 0,
    applied_amount REAL NOT NULL DEFAULT 0,
    applied_unit TEXT NOT NULL DEFAULT '',
    applied_measured INTEGER NOT NULL DEFAULT 0,
    actual_rate REAL NOT NULL DEFAULT 0,
    started_at TEXT,
    ended_at TEXT,
    exported_at TEXT,
    geojson TEXT NOT NULL,
    min_lon REAL, min_lat REAL, max_lon REAL, max_lat REAL,
    src_path TEXT NOT NULL,
    ingested_at TEXT NOT NULL,
    PRIMARY KEY (field, job, product)
);
CREATE TABLE IF NOT EXISTS fields (
    name TEXT PRIMARY KEY,
    geojson TEXT NOT NULL,          -- outer boundary Feature
    min_lon REAL, min_lat REAL, max_lon REAL, max_lat REAL,
    src_path TEXT NOT NULL,
    ingested_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_app_product ON applications(product);
CREATE INDEX IF NOT EXISTS idx_app_started ON applications(started_at);
"""


def migrate(con):
    """Add columns introduced after a DB was first created (CREATE TABLE IF NOT
    EXISTS won't). Re-scan of the source files then backfills the values."""
    have = {r[1] for r in con.execute("PRAGMA table_info(applications)")}
    for col, decl in (("applied_amount", "REAL NOT NULL DEFAULT 0"),
                      ("applied_unit", "TEXT NOT NULL DEFAULT ''"),
                      ("applied_measured", "INTEGER NOT NULL DEFAULT 0"),
                      ("actual_rate", "REAL NOT NULL DEFAULT 0")):
        if col not in have:
            con.execute(f"ALTER TABLE applications ADD COLUMN {col} {decl}")
            # force re-ingest so existing rows pick the new fields up
            con.execute("DELETE FROM files WHERE path LIKE '%coverage.geojson'")
    con.commit()


def bbox_of(coords):
    """Recursive min/max over any GeoJSON coordinate nesting."""
    lons, lats = [], []

    def walk(c):
        if isinstance(c[0], (int, float)):
            lons.append(c[0])
            lats.append(c[1])
        else:
            for x in c:
                walk(x)

    walk(coords)
    return min(lons), min(lats), max(lons), max(lats)


def changed(con, path, st):
    row = con.execute("SELECT mtime, size FROM files WHERE path=?", (str(path),)).fetchone()
    return row is None or row[0] != st.st_mtime or row[1] != st.st_size


def mark(con, path, st):
    con.execute(
        "INSERT INTO files(path, mtime, size) VALUES(?,?,?) "
        "ON CONFLICT(path) DO UPDATE SET mtime=excluded.mtime, size=excluded.size",
        (str(path), st.st_mtime, st.st_size),
    )


def ingest_coverage(con, path):
    doc = json.loads(path.read_text(encoding="utf-8"))
    props = doc.get("properties") or {}
    geom = doc.get("geometry") or {}
    if geom.get("type") != "MultiPolygon" or not geom.get("coordinates"):
        raise ValueError("not a MultiPolygon coverage record")
    lo_lon, lo_lat, hi_lon, hi_lat = bbox_of(geom["coordinates"])
    con.execute(
        """INSERT INTO applications
           (field, job, product, rate, rate_unit, work_type, worked_ha, tool_width_m,
            applied_amount, applied_unit, applied_measured, actual_rate,
            started_at, ended_at, exported_at, geojson,
            min_lon, min_lat, max_lon, max_lat, src_path, ingested_at)
           VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,datetime('now'))
           ON CONFLICT(field, job, product) DO UPDATE SET
             rate=excluded.rate, rate_unit=excluded.rate_unit,
             work_type=excluded.work_type, worked_ha=excluded.worked_ha,
             tool_width_m=excluded.tool_width_m,
             applied_amount=excluded.applied_amount,
             applied_unit=excluded.applied_unit,
             applied_measured=excluded.applied_measured,
             actual_rate=excluded.actual_rate, started_at=excluded.started_at,
             ended_at=excluded.ended_at, exported_at=excluded.exported_at,
             geojson=excluded.geojson, min_lon=excluded.min_lon,
             min_lat=excluded.min_lat, max_lon=excluded.max_lon,
             max_lat=excluded.max_lat, src_path=excluded.src_path,
             ingested_at=datetime('now')""",
        (
            props.get("field", ""), props.get("job", ""), props.get("product", ""),
            props.get("rate", 0), props.get("rateUnit", ""), props.get("workType", ""),
            props.get("workedHa", 0), props.get("toolWidthM", 0),
            props.get("appliedAmount", 0), props.get("appliedUnit", ""),
            1 if props.get("appliedMeasured") else 0, props.get("actualRate", 0),
            props.get("startedAt"), props.get("endedAt"), props.get("exportedAt"),
            json.dumps(doc, separators=(",", ":")),
            lo_lon, lo_lat, hi_lon, hi_lat, str(path),
        ),
    )


def ingest_field(con, path):
    doc = json.loads(path.read_text(encoding="utf-8"))
    outer = None
    for f in doc.get("features", []):
        if (f.get("properties") or {}).get("role") == "outer-boundary":
            outer = f
            break
    if outer is None or not (outer.get("geometry") or {}).get("coordinates"):
        return False
    name = path.parent.name
    lo_lon, lo_lat, hi_lon, hi_lat = bbox_of(outer["geometry"]["coordinates"])
    con.execute(
        """INSERT INTO fields(name, geojson, min_lon, min_lat, max_lon, max_lat,
                              src_path, ingested_at)
           VALUES(?,?,?,?,?,?,?,datetime('now'))
           ON CONFLICT(name) DO UPDATE SET geojson=excluded.geojson,
             min_lon=excluded.min_lon, min_lat=excluded.min_lat,
             max_lon=excluded.max_lon, max_lat=excluded.max_lat,
             src_path=excluded.src_path, ingested_at=datetime('now')""",
        (name, json.dumps(outer, separators=(",", ":")),
         lo_lon, lo_lat, hi_lon, hi_lat, str(path)),
    )
    return True


def scan(con, data_dir):
    n_cov = n_fld = n_err = 0
    for pattern, fn in (("*/jobs/*/coverage.geojson", ingest_coverage),
                        ("*/field.geojson", ingest_field)):
        for path in sorted(data_dir.glob(pattern)):
            try:
                st = path.stat()
                if not changed(con, path, st):
                    continue
                result = fn(con, path)
                mark(con, path, st)  # mark even on skip so bad files aren't rescanned
                if result is not False:
                    if fn is ingest_coverage:
                        n_cov += 1
                    else:
                        n_fld += 1
            except Exception as ex:  # noqa: BLE001 — one bad file must not stop the scan
                n_err += 1
                print(f"[ingest] ERROR {path}: {ex}", flush=True)
    con.commit()
    if n_cov or n_fld or n_err:
        print(f"[ingest] {n_cov} coverage, {n_fld} fields, {n_err} errors", flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default="/srv/agdata/fields")
    ap.add_argument("--db", default="/srv/agdata/coverage.db")
    ap.add_argument("--once", action="store_true")
    args = ap.parse_args()

    data_dir = Path(args.data)
    if not data_dir.is_dir():
        print(f"[ingest] data dir not found: {data_dir}", file=sys.stderr)
        return 1

    os.makedirs(Path(args.db).parent, exist_ok=True)
    con = sqlite3.connect(args.db)
    con.executescript(SCHEMA)
    migrate(con)
    con.execute("PRAGMA journal_mode=WAL")  # survives power cuts far better

    while True:
        scan(con, data_dir)
        if args.once:
            break
        time.sleep(SCAN_SECS)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
