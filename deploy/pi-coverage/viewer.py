#!/usr/bin/env python3
"""AgOpenWeb coverage map viewer (Pi server).

Serves the application-history map over the tailnet: field boundaries + per-
product coverage polygons out of the SQLite index that ingest.py maintains.
Stdlib only — no pip installs, no reverse proxy needed.

Usage:
    viewer.py --db /srv/agdata/coverage.db --port 8080

Endpoints:
    /                    the Leaflet map page (static/)
    /api/fields          FeatureCollection of field outer boundaries
    /api/products        distinct products (for the filter)
    /api/applications    FeatureCollection; filters: ?field=&product=&from=&to=
    /api/summary         per-product totals over the same filters
"""

import argparse
import json
import sqlite3
import threading
import time
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse, parse_qs

STATIC = Path(__file__).parent / "static"
DB_PATH = "/srv/agdata/coverage.db"
LOADS_DIR = "/srv/agdata/loads"
_local = threading.local()
_loads_lock = threading.Lock()


def db():
    con = getattr(_local, "con", None)
    if con is None:
        con = _local.con = sqlite3.connect(DB_PATH)
        con.row_factory = sqlite3.Row
    return con


def app_filters(q):
    """WHERE clause + params from ?field=&product=&from=&to=."""
    where, args = [], []
    if q.get("field"):
        where.append("field = ?")
        args.append(q["field"][0])
    if q.get("product"):
        where.append("product = ?")
        args.append(q["product"][0])
    if q.get("from"):
        where.append("started_at >= ?")
        args.append(q["from"][0])
    if q.get("to"):
        where.append("started_at <= ?")
        args.append(q["to"][0] + "T23:59:59")
    return (" WHERE " + " AND ".join(where)) if where else "", args


def _parse_iso(s):
    try:
        d = datetime.fromisoformat(s.replace("Z", "+00:00"))
        if d.tzinfo is None:  # naive timestamps are in the server's local zone
            d = d.replace(tzinfo=datetime.now().astimezone().tzinfo)
        return d
    except Exception:  # noqa: BLE001
        return None


def attribute_loads(apps):
    """Assign loader-scale records to applications by time window.

    Each load goes to at most ONE application: among windows
    [started_at - 90 min, ended_at (or start + 12 h)] containing it, the one
    with the latest start (the load was most plausibly for the job that began
    right after it). apps is a list of dicts with started_at/ended_at; gains
    loads_kg / loads_n. Returns total kg attributed.
    """
    try:
        # ts_approx records were queued across a loader power cycle: their true time is
        # unknowable, so they appear in the loads history but are never auto-attributed —
        # a weeks-old scoop must not land on whatever job is open when it finally syncs.
        rows = db().execute(
            "SELECT ts_epoch, kg FROM loads WHERE ts_approx=0 ORDER BY ts_epoch").fetchall()
    except sqlite3.OperationalError:   # ingest hasn't created the table yet
        rows = []
    windows = []
    for a in apps:
        a["loads_kg"] = 0.0
        a["loads_n"] = 0
        st = _parse_iso(a.get("started_at") or "")
        if st is None:
            continue
        en = _parse_iso(a.get("ended_at") or "") or (st + timedelta(hours=12))
        windows.append((st - timedelta(minutes=90), en, st, a))
    total = 0.0
    for r in rows:
        t = datetime.fromtimestamp(r["ts_epoch"], tz=timezone.utc)
        best = None
        for lo, hi, st, a in windows:
            if lo <= t <= hi and (best is None or st > best[0]):
                best = (st, a)
        if best:
            best[1]["loads_kg"] += r["kg"]
            best[1]["loads_n"] += 1
            total += r["kg"]
    return total


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *a):  # quiet journald
        pass

    def _send(self, body, ctype="application/json", code=200):
        data = body if isinstance(body, bytes) else body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        u = urlparse(self.path)
        q = parse_qs(u.query)
        try:
            if u.path in ("/", "/index.html"):
                self._send((STATIC / "index.html").read_bytes(), "text/html; charset=utf-8")
            elif u.path.startswith("/static/"):
                name = Path(u.path).name  # no traversal
                f = STATIC / name
                if not f.is_file():
                    self._send('"not found"', code=404)
                    return
                ctype = {"js": "text/javascript", "css": "text/css"}.get(
                    f.suffix.lstrip("."), "application/octet-stream")
                self._send(f.read_bytes(), ctype)
            elif u.path == "/api/fields":
                feats = [json.loads(r["geojson"])
                         for r in db().execute("SELECT geojson FROM fields ORDER BY name")]
                # tag each with its name for the popup
                for f, r in zip(feats, db().execute("SELECT name FROM fields ORDER BY name")):
                    f.setdefault("properties", {})["name"] = r["name"]
                self._send(json.dumps({"type": "FeatureCollection", "features": feats}))
            elif u.path == "/api/products":
                rows = db().execute(
                    "SELECT DISTINCT product FROM applications WHERE product != '' ORDER BY product")
                self._send(json.dumps([r["product"] for r in rows]))
            elif u.path == "/api/applications":
                where, args = app_filters(q)
                apps = [dict(r) for r in db().execute(
                    "SELECT started_at, ended_at, worked_ha, geojson FROM applications"
                    + where + " ORDER BY started_at", args)]
                attribute_loads(apps)
                feats = []
                for a in apps:
                    f = json.loads(a["geojson"])
                    p = f.setdefault("properties", {})
                    # Loader-scale records beat both the manual entry and the estimate.
                    if a["loads_kg"] > 0:
                        p["appliedAmount"] = round(a["loads_kg"], 1)
                        p["appliedUnit"] = "kg"
                        p["appliedMeasured"] = True
                        p["loadsN"] = a["loads_n"]
                        if a["worked_ha"]:
                            p["actualRate"] = round(a["loads_kg"] / a["worked_ha"], 1)
                    feats.append(f)
                self._send(json.dumps({"type": "FeatureCollection", "features": feats}))
            elif u.path == "/api/summary":
                where, args = app_filters(q)
                apps = [dict(r) for r in db().execute(
                    "SELECT product, started_at, ended_at, worked_ha, applied_amount,"
                    " applied_unit, applied_measured FROM applications" + where, args)]
                attribute_loads(apps)
                agg = {}
                for a in apps:
                    s = agg.setdefault(a["product"], {
                        "product": a["product"], "jobs": 0, "ha": 0.0, "applied": 0.0,
                        "appliedUnit": "", "allMeasured": 1, "first": None, "last": None})
                    s["jobs"] += 1
                    s["ha"] += a["worked_ha"] or 0
                    if a["loads_kg"] > 0:
                        s["applied"] += a["loads_kg"]
                        s["appliedUnit"] = "kg"
                    else:
                        s["applied"] += a["applied_amount"] or 0
                        if a["applied_unit"]:
                            s["appliedUnit"] = a["applied_unit"]
                        if not a["applied_measured"]:
                            s["allMeasured"] = 0
                    st = a["started_at"] or ""
                    if st:
                        s["first"] = min(s["first"], st) if s["first"] else st
                        s["last"] = max(s["last"], st) if s["last"] else st
                out = []
                for s in sorted(agg.values(), key=lambda x: -x["ha"]):
                    s["ha"] = round(s["ha"], 2)
                    s["applied"] = round(s["applied"], 1)
                    s["avgRate"] = round(s["applied"] / s["ha"], 1) if s["ha"] and s["applied"] else None
                    out.append(s)
                self._send(json.dumps(out))
            elif u.path == "/api/loads":
                rows = db().execute(
                    "SELECT ts, product, kg, attach, device FROM loads ORDER BY ts_epoch DESC LIMIT 200")
                self._send(json.dumps([dict(r) for r in rows]))
            else:
                self._send('"not found"', code=404)
        except Exception as ex:  # noqa: BLE001
            self._send(json.dumps({"error": str(ex)}), code=500)

    def do_POST(self):
        """POST /api/loads — loader-scale scoop records (store-and-forward).

        Body: {"device": "loader", "events": [{"kg", "product", "attach", "age_s"}]}
        The loader has no RTC: age_s is seconds-since-scoop at send time (-1 =
        unknown, e.g. queued across a power cycle → stamped with receive time).
        Events append to NDJSON files — the FILES are the record, the DB is the
        rebuildable index (ingest.py picks them up within a minute).
        """
        u = urlparse(self.path)
        try:
            if u.path != "/api/loads":
                self._send('"not found"', code=404)
                return
            n = int(self.headers.get("Content-Length") or 0)
            if n <= 0 or n > 1_000_000:
                self._send('"bad length"', code=400)
                return
            doc = json.loads(self.rfile.read(n).decode("utf-8"))
            device = str(doc.get("device") or "unknown")[:32]
            now = time.time()
            lines = []
            for ev in (doc.get("events") or [])[:500]:
                kg = float(ev.get("kg") or 0)
                if kg <= 0:
                    continue
                age = ev.get("age_s")
                approx = not isinstance(age, (int, float)) or age < 0
                epoch = now if approx else now - float(age)
                lines.append(json.dumps({
                    "ts_epoch": round(epoch, 1),
                    "ts": datetime.fromtimestamp(epoch).astimezone().isoformat(),
                    "product": str(ev.get("product") or "")[:32],
                    "kg": round(kg, 1),
                    "attach": str(ev.get("attach") or "")[:16],
                    "device": device,
                    "ts_approx": approx,
                    "received_at": datetime.now().astimezone().isoformat(),
                }, separators=(",", ":")))
            if lines:
                path = Path(LOADS_DIR) / f"loads-{datetime.now():%Y%m}.ndjson"
                with _loads_lock:
                    path.parent.mkdir(parents=True, exist_ok=True)
                    with open(path, "a", encoding="utf-8") as f:
                        f.write("\n".join(lines) + "\n")
            self._send(json.dumps({"ok": True, "stored": len(lines)}))
        except Exception as ex:  # noqa: BLE001
            self._send(json.dumps({"error": str(ex)}), code=500)


def main():
    global DB_PATH, LOADS_DIR
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", default=DB_PATH)
    ap.add_argument("--loads", default=LOADS_DIR)
    ap.add_argument("--port", type=int, default=8080)
    ap.add_argument("--bind", default="0.0.0.0")
    args = ap.parse_args()
    DB_PATH = args.db
    LOADS_DIR = args.loads
    srv = ThreadingHTTPServer((args.bind, args.port), Handler)
    print(f"[viewer] serving on http://{args.bind}:{args.port} db={DB_PATH}", flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
