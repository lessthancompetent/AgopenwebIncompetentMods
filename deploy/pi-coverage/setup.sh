#!/bin/bash
# One-shot install/update of the AgOpenWeb coverage stack on the Pi.
# Run on the Pi:  curl -fsSL <raw-url>/setup.sh | sudo bash
set -euo pipefail

RAW="https://raw.githubusercontent.com/lessthancompetent/Agopenwebpick-from-map/feature/route-planning/deploy/pi-coverage"

# Newer Raspberry Pi OS has no 'pi' user — run services as whoever invoked sudo.
AGUSER="${SUDO_USER:-$(id -un)}"
id "$AGUSER" >/dev/null

mkdir -p /srv/agdata/fields /srv/agdata/static

echo "Fetching files..."
curl -fsSL "$RAW/ingest.py"          -o /srv/agdata/ingest.py
curl -fsSL "$RAW/viewer.py"          -o /srv/agdata/viewer.py
curl -fsSL "$RAW/static/index.html"  -o /srv/agdata/static/index.html
curl -fsSL "$RAW/static/leaflet.js"  -o /srv/agdata/static/leaflet.js
curl -fsSL "$RAW/static/leaflet.css" -o /srv/agdata/static/leaflet.css
curl -fsSL "$RAW/agdata-ingest.service" -o /etc/systemd/system/agdata-ingest.service
curl -fsSL "$RAW/agdata-viewer.service" -o /etc/systemd/system/agdata-viewer.service

sed -i "s/^User=.*/User=$AGUSER/" /etc/systemd/system/agdata-ingest.service \
                                  /etc/systemd/system/agdata-viewer.service
chown -R "$AGUSER:$AGUSER" /srv/agdata

systemctl daemon-reload
systemctl enable --now agdata-ingest agdata-viewer
systemctl restart agdata-ingest agdata-viewer

sleep 1
systemctl --no-pager --lines=3 status agdata-ingest agdata-viewer || true
echo
echo "Done. Map viewer: http://$(tailscale ip -4 2>/dev/null || hostname -I | awk '{print $1}'):8080"
echo "Point Syncthing (or a manual copy) of the Fields tree at /srv/agdata/fields"
