# Pi coverage-record server

Per-product application history for AgOpenWeb, self-hosted on the farm Pi
(coexists with the RTK base + LoRaWAN gateway). The app writes a
`coverage.geojson` application record into each job folder on field close;
Syncthing mirrors the Fields tree to the Pi; `ingest.py` maintains a SQLite
index over the files. **Files are the record — the DB can be deleted and
rebuilt at any time.**

## Setup (once)

1. **Syncthing**: share the app's `Fields` folder from the in-cab device
   (send-only) to the Pi at `/srv/agdata/fields` (enable staggered file
   versioning on the Pi share for an extra safety net).
2. **Ingest**:
   ```bash
   sudo mkdir -p /srv/agdata
   sudo cp ingest.py /srv/agdata/
   sudo cp agdata-ingest.service /etc/systemd/system/
   sudo systemctl enable --now agdata-ingest
   journalctl -u agdata-ingest -f     # watch it pick files up
   ```
   Stdlib-only Python 3 — nothing to pip-install.
3. **Viewer** (map UI over the tailnet):
   ```bash
   sudo cp viewer.py /srv/agdata/
   sudo cp -r static /srv/agdata/
   sudo cp agdata-viewer.service /etc/systemd/system/
   sudo systemctl enable --now agdata-viewer
   ```
   Then browse to `http://<pi-tailscale-name>:8082` from any tailnet device
   (8080 belongs to ChirpStack on this Pi).
   Stdlib-only again — Leaflet is vendored in `static/`, only the OSM map
   tiles come from the internet (the browser fetches those, not the Pi).
4. **Backup**: nightly restic/rclone of `/srv/agdata/fields` to another
   machine or bucket. (The `.db` need not be backed up — rebuildable.)

## Poking at the data

```bash
sqlite3 /srv/agdata/coverage.db \
  "SELECT field, job, product, rate || ' ' || rate_unit, round(worked_ha,2)
   FROM applications ORDER BY started_at DESC LIMIT 20;"

# total area per product this season
sqlite3 /srv/agdata/coverage.db \
  "SELECT product, round(sum(worked_ha),1) AS ha FROM applications
   WHERE started_at >= '2026-03-01' GROUP BY product ORDER BY ha DESC;"
```

Schema: `applications(field, job, product, rate, rate_unit, work_type,
worked_ha, tool_width_m, started_at, ended_at, geojson, bbox…)` with
PK (field, job, product); `fields(name, geojson, bbox…)` holds outer
boundaries for the map viewer. Geometry is stored as GeoJSON text — the
viewer (phase 4) serves it straight out; SpatiaLite/PostGIS is an upgrade
path, not a requirement.
