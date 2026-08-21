#!/bin/bash
# One-shot bootstrap for the farm server (Debian/Ubuntu mini PC in the cabinet).
# Run on a fresh install:  curl -fsSL <raw-url>/setup-linux-server.sh | sudo bash
#
# Site-specific: HOUSE_SYNC_ID=<Syncthing device ID of the house PC> (not committed).
#
# Sets up: Tailscale (with SSH so the house PC can deploy/maintain remotely),
# Syncthing (receives the AgOpenWeb Fields tree), the coverage stack
# (ingest + map viewer, same files as deploy/pi-coverage), and a restic
# backup skeleton. The feed hub (Node/Flask) and ChirpStack migrate later —
# this script owns only the coverage/records side.
set -euo pipefail

REF="${1:-feature/route-planning}"
RAW="https://raw.githubusercontent.com/lessthancompetent/Agopenwebpick-from-map/$REF/deploy/pi-coverage"
AGUSER="${SUDO_USER:-$(id -un)}"
id "$AGUSER" >/dev/null

echo "== packages =="
apt-get update -qq
apt-get install -y -qq curl syncthing restic python3 >/dev/null
# This box doubles as the cowshed screen: browser for the herd/draft-gate UI
# and the farm dashboards, LibreOffice for spreadsheets. Install with a
# desktop selected in the Debian installer; these land on top.
apt-get install -y -qq libreoffice-calc chromium >/dev/null || \
  apt-get install -y -qq libreoffice-calc chromium-browser >/dev/null || true

echo "== tailscale =="
if ! command -v tailscale >/dev/null; then
  curl -fsSL https://tailscale.com/install.sh | sh
fi
# --ssh is the whole point: passwordless remote maintenance from the tailnet.
tailscale up --ssh || true   # first run prints an auth URL — open it, then re-run this script

echo "== directories =="
mkdir -p /srv/agdata/fields /srv/agdata/static /srv/agdata/loads

echo "== coverage stack =="
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

echo "== syncthing =="
systemctl enable --now "syncthing@$AGUSER"
sleep 5
# Pre-trust the house PC and create the receive-only Fields folder; the house
# side completes the pairing (adds this device ID and shares the folder).
# Pass HOUSE_SYNC_ID=<syncthing device id> — installation-specific, not committed.
HOUSE="${HOUSE_SYNC_ID:-}"
[ -n "$HOUSE" ] && sudo -u "$AGUSER" syncthing cli config devices add --device-id "$HOUSE" --name House || true
sudo -u "$AGUSER" syncthing cli config folders add --id agopen-fields \
  --label "AgOpenWeb Fields" --path /srv/agdata/fields --type receiveonly || true
sudo -u "$AGUSER" syncthing cli config folders agopen-fields devices add --device-id "$HOUSE" || true
API=$(sudo -u "$AGUSER" syncthing cli config gui apikey get)
curl -s -X PATCH -H "X-API-Key: $API" -H 'Content-Type: application/json' \
  -d '{"versioning":{"type":"staggered","params":{"maxAge":"31536000"}}}' \
  http://127.0.0.1:8384/rest/config/folders/agopen-fields >/dev/null || true

echo "== services =="
systemctl daemon-reload
systemctl enable --now agdata-ingest agdata-viewer
systemctl restart agdata-ingest agdata-viewer

echo "== restic skeleton =="
# Point RESTIC_REPOSITORY at an off-box target (house PC share / USB disk /
# cloud bucket) and set the password file, then enable the timer.
mkdir -p /etc/restic
[ -f /etc/restic/password ] || { openssl rand -hex 24 > /etc/restic/password; chmod 600 /etc/restic/password; }
cat > /etc/systemd/system/agdata-backup.service << 'UNIT'
[Unit]
Description=Nightly restic backup of the farm records
[Service]
Type=oneshot
EnvironmentFile=-/etc/restic/env
ExecStart=/usr/bin/restic backup /srv/agdata/fields /srv/agdata/loads --password-file /etc/restic/password
UNIT
cat > /etc/systemd/system/agdata-backup.timer << 'UNIT'
[Unit]
Description=Nightly farm-records backup
[Timer]
OnCalendar=daily
RandomizedDelaySec=1h
Persistent=true
[Install]
WantedBy=timers.target
UNIT
echo "restic: set RESTIC_REPOSITORY in /etc/restic/env, run 'restic init', then: systemctl enable --now agdata-backup.timer"

sleep 1
systemctl --no-pager --lines=2 status agdata-ingest agdata-viewer || true
echo
echo "== DONE =="
echo "Map viewer: http://$(tailscale ip -4 2>/dev/null || hostname -I | awk '{print $1}'):8082"
echo "Syncthing device ID (give this to the house PC for pairing):"
sudo -u "$AGUSER" syncthing cli show system 2>/dev/null | grep -oE '"myID": *"[^"]*"' | cut -d'"' -f4
