#!/bin/bash
# One-shot bootstrap for the FZ-G1 in-cab tablet (Debian 13, XFCE).
# Run after a fresh install:  curl -fsSL <raw-url>/setup-fzg1.sh | sudo bash
#
# Turns the tablet into the tractor guidance unit:
#  - Tailscale + OpenSSH with the house key (remote maintenance from anywhere)
#  - Syncthing sharing the app's Fields tree (send/receive with House + cowshed)
#  - Chromium kiosk autostart pointing at the AgOpenWeb web UI (localhost:5174)
#  - A systemd unit for the AgOpenWeb Linux daemon (binary pushed over SSH after
#    first boot — the app updates remotely from then on)
set -euo pipefail

REF="${1:-feature/route-planning}"
RAW="https://raw.githubusercontent.com/lessthancompetent/Agopenwebpick-from-map/$REF/deploy/server"
AGUSER="${SUDO_USER:-$(id -un)}"
AGHOME=$(getent passwd "$AGUSER" | cut -d: -f6)
id "$AGUSER" >/dev/null

echo "== packages =="
apt-get update -qq
apt-get install -y -qq curl syncthing chromium openssh-server >/dev/null 2>&1 || \
  apt-get install -y -qq curl syncthing chromium-browser openssh-server >/dev/null

echo "== remote access =="
mkdir -p "$AGHOME/.ssh"
curl -fsSL "$RAW/house-key.pub" >> "$AGHOME/.ssh/authorized_keys"
sort -u "$AGHOME/.ssh/authorized_keys" -o "$AGHOME/.ssh/authorized_keys"
chown -R "$AGUSER:$AGUSER" "$AGHOME/.ssh"; chmod 700 "$AGHOME/.ssh"; chmod 600 "$AGHOME/.ssh/authorized_keys"
echo "$AGUSER ALL=(ALL) NOPASSWD: ALL" > /etc/sudoers.d/"$AGUSER"
if ! command -v tailscale >/dev/null; then
  curl -fsSL https://tailscale.com/install.sh | sh
fi
tailscale up || true    # prints an auth URL on first run

echo "== fields dir + syncthing =="
FIELDS="$AGHOME/Documents/AgOpenWeb/Fields"
mkdir -p "$FIELDS"; chown -R "$AGUSER:$AGUSER" "$AGHOME/Documents"
systemctl enable --now "syncthing@$AGUSER"
sleep 5
HOUSE=HOUSE_SYNC_ID
COWSHED=COWSHED_SYNC_ID
sudo -u "$AGUSER" syncthing cli config devices add --device-id "$HOUSE" --name House || true
sudo -u "$AGUSER" syncthing cli config devices add --device-id "$COWSHED" --name cowshed || true
sudo -u "$AGUSER" syncthing cli config folders add --id agopen-fields \
  --label "AgOpenWeb Fields" --path "$FIELDS" --type sendreceive || true
sudo -u "$AGUSER" syncthing cli config folders agopen-fields devices add --device-id "$HOUSE" || true
sudo -u "$AGUSER" syncthing cli config folders agopen-fields devices add --device-id "$COWSHED" || true

echo "== agopenweb service (binary pushed later over SSH) =="
mkdir -p /opt/agopenweb
chown "$AGUSER:$AGUSER" /opt/agopenweb
cat > /etc/systemd/system/agopenweb.service << EOF
[Unit]
Description=AgOpenWeb guidance backend
After=network.target

[Service]
User=$AGUSER
WorkingDirectory=/opt/agopenweb
ExecStart=/opt/agopenweb/AgOpenWeb.Desktop
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
# enabled but will fail-loop harmlessly until the binary lands:
systemctl enable agopenweb

echo "== kiosk: boot straight into the guidance UI =="
mkdir -p /etc/lightdm/lightdm.conf.d
cat > /etc/lightdm/lightdm.conf.d/50-autologin.conf << EOF
[Seat:*]
autologin-user=$AGUSER
autologin-user-timeout=0
EOF
rm -f /etc/xdg/autostart/light-locker.desktop
mkdir -p "$AGHOME/.config/autostart"
cat > "$AGHOME/.config/autostart/agopenweb-kiosk.desktop" << EOF
[Desktop Entry]
Type=Application
Name=AgOpenWeb Kiosk
Exec=sh -c 'sleep 6; chromium --kiosk --noerrdialogs --disable-session-crashed-bubble http://localhost:5174'
X-GNOME-Autostart-enabled=true
EOF
chown -R "$AGUSER:$AGUSER" "$AGHOME/.config"

echo
echo "== DONE =="
echo "1. Open the tailscale auth link above (if shown)."
echo "2. Paste back the Syncthing device ID below for pairing:"
sudo -u "$AGUSER" syncthing cli show system 2>/dev/null | grep -oE '"myID": *"[^"]*"' | cut -d'"' -f4
echo "3. The AgOpenWeb app itself gets pushed over SSH next (systemd unit is ready)."
