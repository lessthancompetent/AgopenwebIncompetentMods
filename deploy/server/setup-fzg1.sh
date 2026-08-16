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
apt-get install -y -qq curl syncthing chromium openssh-server onboard >/dev/null 2>&1 || \
  apt-get install -y -qq curl syncthing chromium-browser openssh-server onboard >/dev/null

echo "== clock =="
# Debian installers often land on the mirror's timezone; job/coverage records are
# stamped in LOCAL time, so a wrong zone mis-dates farm records.
timedatectl set-timezone "${AGTZ:-Pacific/Auckland}" || true

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

echo "== wifi profiles (ethernet + wifi run together) =="
# The cab tablet keeps BOTH links up: ethernet talks to the AIO/rate modules,
# wifi carries internet. Wired keeps the lower route metric so module traffic
# never leaves via wifi, even when both sit on the same subnet.
# One radio = one association at a time, so these are two saved profiles that
# auto-connect by availability; the house wifi outranks rtkwifi when both are
# in range. Passphrases are NOT stored here — enter them once on the tablet
# (network tray icon) or with: nmcli --ask connection up "<name>"
WIFI_DEV=$(nmcli -t -f DEVICE,TYPE device status | awk -F: '$2=="wifi"{print $1; exit}')
if [ -n "$WIFI_DEV" ]; then
  addwifi() { # name, priority
    nmcli connection show "$1" >/dev/null 2>&1 || \
      nmcli connection add type wifi con-name "$1" ifname "$WIFI_DEV" ssid "$1" \
        wifi-sec.key-mgmt wpa-psk connection.autoconnect yes \
        connection.autoconnect-priority "$2" ipv4.route-metric 600 ipv6.route-metric 600 >/dev/null
  }
  addwifi "HOUSE_SSID" 10
  addwifi "rtkwifi" 5
  nmcli connection modify "Wired connection 1" ipv4.route-metric 100 2>/dev/null || true
fi

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
# .desktop Exec lines do NOT understand shell single-quote syntax — an inline
# `sh -c '…'` gets word-split into garbage and dies silently (found on the
# agproxy dress rehearsal). A real launcher script sidesteps quoting entirely.
cat > /usr/local/bin/agopenweb-kiosk << 'EOF'
#!/bin/sh
sleep 6
# App-mode maximized window, NOT --kiosk: a hard kiosk is a touch-only jail
# (no keyboard in the cab). Maximized keeps the XFCE panel reachable so the
# operator can tap the desktop launchers (Split YouTube / Split Spotify /
# Guidance Full) without any keyboard.
exec chromium --app=http://localhost:5174 --start-maximized --force-device-scale-factor=1.5 --noerrdialogs --disable-session-crashed-bubble --autoplay-policy=no-user-gesture-required
EOF

# Bezel buttons -> short-press and long-press actions.
#
# Probed on the real tablet 2026-08-15 by pressing each button while watching
# all 16 input devices, the kernel log and the ACPI event layer:
#   ROTATION is the ONLY bezel button that reaches Linux. It fires AT scancode
#      0x65 *together with* Left Meta — it carries its Windows "Tablet PC
#      Settings -> Buttons" assignment in firmware, so Linux sees Super+<key>
#      and a desktop shortcut bound to the bare key NEVER fires. It also emits
#      ACPI hotkeys 0x54/0x55 alongside. The daemon reads evdev directly, which
#      survives a fullscreen browser holding keyboard focus.
#   A1 and A2 emit NOTHING on any channel — in Windows they were serviced by a
#      Panasonic driver that has no Debian equivalent. Nothing to bind.
# Actions live in /etc/fzg1-buttons.conf and are re-read on every press.
cat > /etc/udev/hwdb.d/90-fzg1-buttons.hwdb << 'EOF'
evdev:atkbd:dmi:bvn*:bvr*:bd*:svnPanasonic*:pnFZG1*:*
 KEYBOARD_KEY_65=prog1
EOF
systemd-hwdb update
udevadm trigger --sysname-match="event*" || true
# belt-and-braces: pin the scancode in the kernel table too, so the mapping
# does not depend on udev re-probing the AT keyboard
cat > /etc/systemd/system/fzg1-buttons.service << 'EOF'
[Unit]
Description=FZ-G1 bezel button scancode mapping
After=systemd-udev-settle.service

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/usr/bin/setkeycodes 65 148

[Install]
WantedBy=multi-user.target
EOF
systemctl enable --now fzg1-buttons >/dev/null 2>&1 || true

apt-get install -y -qq xinput x11-xserver-utils >/dev/null 2>&1 || true
curl -fsSL "$RAW/fzg1-buttonsd" -o /usr/local/bin/fzg1-buttonsd
curl -fsSL "$RAW/ag-rotate"    -o /usr/local/bin/ag-rotate
# Configure a rate/steer module from the TABLET instead of a phone. The module's
# settings page only exists on its own AP, and the phone is usually the thing
# providing the network the module is being pointed at — joining from the phone
# drops that hotspot, so the module has nothing to join when it reboots. The
# tablet has no such conflict and keeps its Ethernet throughout.
curl -fsSL "$RAW/ag-modwifi"   -o /usr/local/bin/ag-modwifi
chmod +x /usr/local/bin/fzg1-buttonsd /usr/local/bin/ag-rotate /usr/local/bin/ag-modwifi
if [ ! -f /etc/fzg1-buttons.conf ]; then   # never clobber the operator's edits
cat > /etc/fzg1-buttons.conf << 'EOF'
# FZ-G1 bezel button actions.
# Edit a command and it takes effect on the next press - no restart needed.
#
# A1/A2 only work once the patched panasonic-hbtn driver is installed - run
# install-fzg1-buttons-driver.sh. Without it they emit nothing at all (see that
# script for why). The ROTATION button works without any driver.
#
# Available commands:
#   /usr/local/bin/ag-osk            toggle the on-screen keyboard
#   /usr/local/bin/ag-rotate         rotate the screen a step (touch follows)
#   /usr/local/bin/ag-rotate normal  put rotation back to landscape
#   /usr/local/bin/ag-split full     guidance back to full screen
#   /usr/local/bin/ag-split youtube  half guidance / half YouTube
#   /usr/local/bin/ag-split spotify  half guidance / half Spotify

# Hold this many milliseconds or more to count as a long press.
# Measured normal presses: ~250ms (A1/A2), ~650ms (rotation), so keep well
# above - 600 was too low and turned every ordinary press into a long one.
LONG_MS=1500

A1_SHORT="/usr/local/bin/ag-osk"
A1_LONG="/usr/local/bin/ag-split full"

A2_SHORT="/usr/local/bin/ag-rotate"
A2_LONG="/usr/local/bin/ag-rotate normal"

ROTATION_SHORT="/usr/local/bin/ag-osk"
ROTATION_LONG="/usr/local/bin/ag-rotate"
EOF
fi
cat > /etc/systemd/system/fzg1-buttons-daemon.service << 'EOF'
[Unit]
Description=FZ-G1 bezel button short/long press dispatch
After=multi-user.target

[Service]
ExecStart=/usr/local/bin/fzg1-buttonsd
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable --now fzg1-buttons-daemon >/dev/null 2>&1 || true
cat > /usr/local/bin/ag-osk << 'EOF'
#!/bin/sh
# FZ-G1 A1 button: toggle on-screen keyboard visibility.
# onboard stays running (autostart, hidden); we only flip visibility over
# D-Bus — kill/restart toggling raced the bezel button's key-repeat and
# left onboard dead. Debounce absorbs the repeat events of one long press.
now=$(date +%s%N); last=$(cat /tmp/.ag-osk-stamp 2>/dev/null || echo 0)
[ $(( (now - last) / 1000000 )) -lt 700 ] && exit 0
echo "$now" > /tmp/.ag-osk-stamp
if pgrep -x onboard >/dev/null; then
  dbus-send --type=method_call --dest=org.onboard.Onboard \
    /org/onboard/Onboard/Keyboard org.onboard.Onboard.Keyboard.ToggleVisible
else
  onboard &
fi
EOF
chmod +x /usr/local/bin/ag-osk
cat > "$AGHOME/.config/autostart/ag-osk-daemon.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Onboard OSK daemon
Exec=onboard
X-GNOME-Autostart-enabled=true
EOF

# HiDPI: the FZ-G1 is 1920x1200 at 10" (~220 DPI) — stock XFCE renders tiny.
# xfconf needs the user's session bus, so apply at login via autostart
# (idempotent). Chromium/Spotify get their own scale flags where launched.
cat > /usr/local/bin/ag-hidpi << 'EOF'
#!/bin/sh
xfconf-query -c xsettings -p /Xft/DPI -n -t int -s 168
xfconf-query -c xsettings -p /Gtk/CursorThemeSize -n -t int -s 48
xfconf-query -c xfwm4 -p /general/theme -n -t string -s Default-xhdpi
xfconf-query -c xfce4-panel -p /panels/panel-1/size -n -t int -s 52 2>/dev/null
xfconf-query -c xfce4-desktop -p /desktop-icons/icon-size -n -t uint -s 64 2>/dev/null
# cab screen: never blank or power down
xset s off
xset s noblank
xset -dpms
xfconf-query -c xfce4-power-manager -p /xfce4-power-manager/dpms-enabled -n -t bool -s false
# NOTE: no XF86Launch1 desktop shortcut here on purpose — fzg1-buttons-daemon
# owns the bezel buttons. A second binding would fire the SHORT action on every
# press, including long ones.
# onboard daemon starts hidden; docked full-width along the bottom. The stock
# 700x205 dock gives ~40px keys on this 220 DPI panel - unusable with work
# gloves; 1920x480 is ~96px (11mm) per key.
gsettings set org.onboard start-minimized true 2>/dev/null
gsettings set org.onboard.window docking-enabled true 2>/dev/null
gsettings set org.onboard.window.landscape dock-expand true 2>/dev/null
gsettings set org.onboard.window.landscape dock-height 480 2>/dev/null
gsettings set org.onboard.window.landscape height 480 2>/dev/null
gsettings set org.onboard.window.landscape dock-width 1920 2>/dev/null
gsettings set org.onboard.window.landscape width 1920 2>/dev/null
for ch in blank-on-ac blank-on-battery dpms-on-ac-sleep dpms-on-ac-off dpms-on-battery-sleep dpms-on-battery-off; do
  xfconf-query -c xfce4-power-manager -p /xfce4-power-manager/$ch -n -t int -s 0
done
EOF
rm -f /etc/xdg/autostart/xfce4-screensaver.desktop
chmod +x /usr/local/bin/ag-hidpi
cat > "$AGHOME/.config/autostart/ag-hidpi.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Tablet display scaling
Exec=/usr/local/bin/ag-hidpi
X-GNOME-Autostart-enabled=true
EOF
chmod +x /usr/local/bin/agopenweb-kiosk
cat > "$AGHOME/.config/autostart/agopenweb-kiosk.desktop" << EOF
[Desktop Entry]
Type=Application
Name=AgOpenWeb Kiosk
Exec=/usr/local/bin/agopenweb-kiosk
X-GNOME-Autostart-enabled=true
EOF
chown -R "$AGUSER:$AGUSER" "$AGHOME/.config"

echo "== entertainment split (autosteer passenger mode) =="
apt-get install -y -qq wmctrl xdotool x11-utils >/dev/null
# Spotify web player needs Widevine DRM that Debian chromium lacks — use the
# official Linux client instead.
#
# Spotify rotates the repo signing key and does NOT always publish the current
# one at a stable URL: in Aug 2026 the repo was signed with a key absent from
# every download.spotify.com/debian/pubkey_*.gpg. So try the published keys,
# then fall back to fetching whatever key id apt says is missing from a
# keyserver. Failure here is reported LOUDLY — the old version hid it, and the
# tablet shipped with a Spotify desktop button wired to a program that was
# never installed.
install_spotify() {
  for k in pubkey_6224F9941A8AA6D1 pubkey_5E3C45D7B312C643 pubkey_C85668DF69375001; do
    curl -fsSL "https://download.spotify.com/debian/$k.gpg" \
      | gpg --dearmor > /usr/share/keyrings/spotify.gpg 2>/dev/null && break
  done
  echo "deb [signed-by=/usr/share/keyrings/spotify.gpg] http://repository.spotify.com stable non-free" \
    > /etc/apt/sources.list.d/spotify.list
  if ! apt-get update -qq 2>/tmp/spotify-apt.err; then
    # "Missing key <FPR>, which is needed to verify signature"
    local fpr
    fpr=$(grep -oE 'Missing key [0-9A-F]{40}' /tmp/spotify-apt.err | head -1 | awk '{print $3}') || true
    if [ -n "$fpr" ] \
       && gpg --no-default-keyring --keyring /tmp/spotify-ks.gpg \
              --keyserver hkps://keyserver.ubuntu.com --recv-keys "$fpr" >/dev/null 2>&1; then
      gpg --no-default-keyring --keyring /tmp/spotify-ks.gpg --export "$fpr" \
        > /usr/share/keyrings/spotify.gpg
      apt-get update -qq >/dev/null 2>&1 || true
    fi
  fi
  apt-get install -y -qq spotify-client >/dev/null 2>&1
}
if install_spotify && command -v spotify >/dev/null; then
  echo "  spotify installed"
else
  # Leave no dead launcher behind: drop the repo and say so plainly.
  rm -f /etc/apt/sources.list.d/spotify.list
  apt-get update -qq >/dev/null 2>&1 || true
  SPOTIFY_MISSING=1
  echo "  !! SPOTIFY NOT INSTALLED — its repo key could not be resolved."
  echo "     The 'Split Spotify' desktop button is NOT being created."
  echo "     Retry later:  sudo bash $0  (or install spotify by hand)"
fi

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
    chromium --new-window --force-device-scale-factor=1.5 --app=https://www.youtube.com &
    sleep 3
    YT=$(wmctrl -l | grep -iE "youtube" | head -1 | cut -d" " -f1)
    [ -n "$YT" ] && { wmctrl -i -r "$YT" -b remove,maximized_vert,maximized_horz
                      wmctrl -i -r "$YT" -e "0,$HALF,0,$HALF,$H"; }
    ;;
  spotify)
    [ -n "$AG" ] && { wmctrl -i -r "$AG" -b remove,fullscreen,maximized_vert,maximized_horz
                      wmctrl -i -r "$AG" -e "0,0,0,$HALF,$H"; }
    pgrep -x spotify >/dev/null || spotify --force-device-scale-factor=1.5 &
    sleep 4
    SP=$(wmctrl -l | grep -i "spotify" | head -1 | cut -d" " -f1)
    [ -n "$SP" ] && { wmctrl -i -r "$SP" -b remove,maximized_vert,maximized_horz
                      wmctrl -i -r "$SP" -e "0,$HALF,0,$HALF,$H"; }
    ;;
  full|*)
    # close entertainment windows, guidance back to full screen
    for w in $(wmctrl -l | grep -iE "youtube|spotify" | cut -d" " -f1); do wmctrl -i -c "$w"; done
    if [ -n "$AG" ]; then
      # maximize, don't fullscreen — keeps the panel touch-reachable (no keyboard in cab)
      wmctrl -i -a "$AG"
      wmctrl -i -r "$AG" -b remove,fullscreen
      wmctrl -i -r "$AG" -b add,maximized_vert,maximized_horz
    else
      # Guidance window is gone (operator closed it, or it crashed) and there is
      # otherwise NO way back to it from the tablet — relaunch the kiosk.
      nohup /usr/local/bin/agopenweb-kiosk >/dev/null 2>&1 &
    fi
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
# Module WiFi buttons use ag-modwifi rather than ag-split, so they are written
# directly instead of through mkdesk.
cat > "$AGHOME/Desktop/Module WiFi.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Module WiFi
Comment=Join a rate module's AP to configure it
Exec=/usr/local/bin/ag-modwifi
Icon=network-wireless
Terminal=false
EOF
cat > "$AGHOME/Desktop/Module WiFi Back.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Module WiFi Back
Comment=Leave the module AP and rejoin the normal network
Exec=/usr/local/bin/ag-modwifi back
Icon=network-wireless
Terminal=false
EOF
chmod +x "$AGHOME/Desktop/Module WiFi.desktop" "$AGHOME/Desktop/Module WiFi Back.desktop"
# Only if it actually installed — a launcher for a missing program looks like a
# broken tablet, not a missing package.
[ "${SPOTIFY_MISSING:-0}" = "1" ] || mkdesk "Split Spotify" spotify spotify-client
mkdesk "Guidance Full" full view-fullscreen
chown -R "$AGUSER:$AGUSER" "$AGHOME/Desktop"

echo
echo "== DONE =="
echo "1. Open the tailscale auth link above (if shown)."
echo "2. Paste back the Syncthing device ID below for pairing:"
sudo -u "$AGUSER" syncthing cli show system 2>/dev/null | grep -oE '"myID": *"[^"]*"' | cut -d'"' -f4
echo "3. The AgOpenWeb app itself gets pushed over SSH next (systemd unit is ready)."
