#!/bin/bash
# Make the FZ-G1's A1 / A2 bezel buttons work on Debian.
#
# Out of the box they do NOTHING on Linux: no input event, no kernel message,
# nothing on the ACPI event layer. The firmware queues a code into a ring
# buffer on the \_SB.TBTN ACPI device (hardware id MAT002A) and raises
# Notify(TBTN, 0x80); only that device's HINF method drains the ring, and no
# in-tree driver claims MAT002A - so the codes pile up unread. (The rotation
# button is a separate path and works without this driver.)
#
# The out-of-tree panasonic-hbtn driver already speaks that exact protocol for
# the Toughbook CF-18/19, so we add this tablet's id and scancodes to it -
# see panasonic-hbtn-fzg1.patch for the details.
#
# Installs via DKMS so a kernel upgrade rebuilds and re-signs it automatically;
# without that, the next apt upgrade silently kills the buttons.
#
# Secure Boot: this tablet ships with it enabled (lockdown=integrity), which
# rejects unsigned modules. We generate a local signing key and sign with it;
# enrolling that key needs YOU at the machine once (instructions printed at the
# end). Secure Boot stays ON - we are not disabling it.
set -euo pipefail

REF="${1:-feature/route-planning}"
RAW="https://raw.githubusercontent.com/lessthancompetent/Agopenwebpick-from-map/$REF/deploy/server"
SRC=/usr/src/panasonic-hbtn-1.0
MOKDIR=/root/mok

[ "$(id -u)" -eq 0 ] || { echo "run with sudo" >&2; exit 1; }

echo "== build dependencies =="
apt-get update -qq
apt-get install -y -qq build-essential "linux-headers-$(uname -r)" git dkms mokutil openssl >/dev/null

echo "== fetch + patch the driver =="
tmp=$(mktemp -d)
git clone -q https://github.com/nagyrobi/panasonic-hbtn.git "$tmp/hbtn"
curl -fsSL "$RAW/panasonic-hbtn-fzg1.patch" -o "$tmp/fzg1.patch"
( cd "$tmp/hbtn" && patch -p1 --forward < "$tmp/fzg1.patch" )

echo "== stage for DKMS =="
mkdir -p "$SRC"
cp "$tmp/hbtn/panasonic-hbtn.c" "$SRC/"
cat > "$SRC/Makefile" << 'EOF'
obj-m := panasonic-hbtn.o

KVER ?= $(shell uname -r)
KDIR ?= /lib/modules/$(KVER)/build

all:
	$(MAKE) -C $(KDIR) M=$(CURDIR) modules

clean:
	$(MAKE) -C $(KDIR) M=$(CURDIR) clean
EOF
cat > "$SRC/dkms.conf" << 'EOF'
PACKAGE_NAME="panasonic-hbtn"
PACKAGE_VERSION="1.0"
MAKE[0]="make KVER=${kernelver} KDIR=/lib/modules/${kernelver}/build"
CLEAN="make clean"
BUILT_MODULE_NAME[0]="panasonic-hbtn"
DEST_MODULE_LOCATION[0]="/updates"
AUTOINSTALL="yes"
EOF

echo "== module signing key =="
if [ ! -f "$MOKDIR/MOK.der" ]; then
  mkdir -p "$MOKDIR"
  openssl req -new -x509 -newkey rsa:2048 -keyout "$MOKDIR/MOK.priv" \
    -outform DER -out "$MOKDIR/MOK.der" -nodes -days 36500 \
    -subj "/CN=FZ-G1 local module signing/" 2>/dev/null
  chmod 600 "$MOKDIR/MOK.priv"
  echo "  generated $MOKDIR/MOK.der"
else
  echo "  reusing existing $MOKDIR/MOK.der"
fi
if ! grep -q mok_signing_key /etc/dkms/framework.conf 2>/dev/null; then
  cat >> /etc/dkms/framework.conf << EOF

# Sign rebuilt modules with the locally enrolled MOK key (Secure Boot is on).
mok_signing_key="$MOKDIR/MOK.priv"
mok_certificate="$MOKDIR/MOK.der"
EOF
fi

echo "== build + install =="
dkms add -m panasonic-hbtn -v 1.0 2>/dev/null || true
dkms build -m panasonic-hbtn -v 1.0
dkms install -m panasonic-hbtn -v 1.0 --force
rm -rf "$tmp"
dkms status

echo
if mokutil --test-key "$MOKDIR/MOK.der" 2>&1 | grep -q "already enrolled"; then
  modprobe panasonic-hbtn || true
  echo "== DONE - key already trusted, module loaded =="
  grep -q "Panasonic Tablet Button Support" /proc/bus/input/devices \
    && echo "A1/A2 are live (KEY_PROG2 / KEY_PROG3)."
else
  echo "== ONE MANUAL STEP LEFT (Secure Boot) =="
  echo "1. sudo mokutil --import $MOKDIR/MOK.der     # choose a one-time password"
  echo "2. sudo reboot"
  echo "3. At the blue MOK Manager screen use a USB KEYBOARD (touch does not work):"
  echo "   Enroll MOK -> Continue -> Yes -> type that password."
  echo "Then A1/A2 appear as KEY_PROG2 / KEY_PROG3 and fzg1-buttonsd picks them up."
fi
