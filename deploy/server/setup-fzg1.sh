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
Exec=sh -c 'sleep 6; chromium --kiosk --noerrdialogs --disable-session-crashed-bubble --autoplay-policy=no-user-gesture-required http://localhost:5174'
X-GNOME-Autostart-enabled=true
EOF
chown -R "$AGUSER:$AGUSER" "$AGHOME/.config"

echo "== entertainment split (autosteer passenger mode) =="
apt-get install -y -qq wmctrl xdotool x11-utils >/dev/null
# Spotify web player needs Widevine DRM that Debian chromium lacks — use the
# official Linux client instead.
curl -fsSL https://download.spotify.com/debian/pubkey_C85668DF69375001.gpg | gpg --dearmor -o /usr/share/keyrings/spotify.gpg 2>/dev/null || true
echo "deb [signed-by=/usr/share/keyrings/spotify.gpg] http://repository.spotify.com stable non-free" > /etc/apt/sources.list.d/spotify.list
apt-get update -qq && apt-get install -y -qq spotify-client >/dev/null 2>&1 || echo "(spotify install failed - retry later)"

cat > /usr/local/bin/ag-split << 'EOF'
#!/bin/bash
# Toggle guidance/entertainment split. Usage: ag-split youtube|spotify|full
# Geometry is read live from the current screen, so rotation just works.
read -r W H < <(xdotool getdisplaygeometry)
HALF=$((W / 2))
AG=$(wmctrl -l | grep -i "AgOpenWeb" | head -1 | cut -d" " -f1)
case "$1" in
  youtube)
    [ -n "$AG" ] && { wmctrl -i -r "$AG" -b remove,fullscreen,maximized_vert,maximized_horz
                      wmctrl -i -r "$AG" -e "0,0,0,$HALF,$H"; }
    chromium --new-window --app=https://www.youtube.com &
    sleep 3
    YT=$(wmctrl -l | grep -iE "youtube" | head -1 | cut -d" " -f1)
    [ -n "$YT" ] && { wmctrl -i -r "$YT" -b remove,maximized_vert,maximized_horz
                      wmctrl -i -r "$YT" -e "0,$HALF,0,$HALF,$H"; }
    ;;
  spotify)
    [ -n "$AG" ] && { wmctrl -i -r "$AG" -b remove,fullscreen,maximized_vert,maximized_horz
                      wmctrl -i -r "$AG" -e "0,0,0,$HALF,$H"; }
    pgrep -x spotify >/dev/null || spotify &
    sleep 4
    SP=$(wmctrl -l | grep -i "spotify" | head -1 | cut -d" " -f1)
    [ -n "$SP" ] && { wmctrl -i -r "$SP" -b remove,maximized_vert,maximized_horz
                      wmctrl -i -r "$SP" -e "0,$HALF,0,$HALF,$H"; }
    ;;
  full|*)
    # close entertainment windows, guidance back to full screen
    for w in $(wmctrl -l | grep -iE "youtube|spotify" | cut -d" " -f1); do wmctrl -i -c "$w"; done
    [ -n "$AG" ] && wmctrl -i -r "$AG" -b add,fullscreen
    ;;
esac
EOF
chmod +x /usr/local/bin/ag-split
mkdir -p "$AGHOME/Desktop"
mkdesk() { cat > "$AGHOME/Desktop/$1.desktop" << EOF
[Desktop Entry]
Type=Application
Name=$1
Exec=/usr/local/bin/ag-split $2
Icon=$3
Terminal=false
EOF
chmod +x "$AGHOME/Desktop/$1.desktop"; }
mkdesk "Split YouTube" youtube youtube
mkdesk "Split Spotify" spotify spotify-client
mkdesk "Guidance Full" full view-fullscreen
chown -R "$AGUSER:$AGUSER" "$AGHOME/Desktop"

echo
echo "== DONE =="
echo "1. Open the tailscale auth link above (if shown)."
echo "2. Paste back the Syncthing device ID below for pairing:"
sudo -u "$AGUSER" syncthing cli show system 2>/dev/null | grep -oE '"myID": *"[^"]*"' | cut -d'"' -f4
echo "3. The AgOpenWeb app itself gets pushed over SSH next (systemd unit is ready)."
