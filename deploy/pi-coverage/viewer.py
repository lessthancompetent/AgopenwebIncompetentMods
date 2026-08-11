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
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse, parse_qs

STATIC = Path(__file__).parent / "static"
DB_PATH = "/srv/agdata/coverage.db"
_local = threading.local()


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
                feats = []
                for r in db().execute(
                        "SELECT geojson FROM applications" + where + " ORDER BY started_at", args):
                    feats.append(json.loads(r["geojson"]))
                self._send(json.dumps({"type": "FeatureCollection", "features": feats}))
            elif u.path == "/api/summary":
                where, args = app_filters(q)
                rows = db().execute(
                    "SELECT product, count(*) AS jobs, round(sum(worked_ha), 2) AS ha,"
                    " min(started_at) AS first, max(started_at) AS last"
                    " FROM applications" + where + " GROUP BY product ORDER BY ha DESC", args)
                self._send(json.dumps([dict(r) for r in rows]))
            else:
                self._send('"not found"', code=404)
        except Exception as ex:  # noqa: BLE001
            self._send(json.dumps({"error": str(ex)}), code=500)


def main():
    global DB_PATH
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", default=DB_PATH)
    ap.add_argument("--port", type=int, default=8080)
    ap.add_argument("--bind", default="0.0.0.0")
    args = ap.parse_args()
    DB_PATH = args.db
    srv = ThreadingHTTPServer((args.bind, args.port), Handler)
    print(f"[viewer] serving on http://{args.bind}:{args.port} db={DB_PATH}", flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
