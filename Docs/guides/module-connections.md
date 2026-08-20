# Connecting your modules

**What this is.** AgOpenWeb talks to the same boards as AgOpenGPS: steer, machine,
GPS, IMU and rate modules. This guide shows the three ways to hook them up —
Ethernet cable (best), WiFi, or USB — and what to check when a module won't appear.

**You will need**

- Your module boards, powered up
- An Ethernet cable or WiFi link to the tablet (or a USB lead for serial)
- The **Network IO** panel: tap the WiFi-shaped icon at the bottom of the
  left button bar

## How the app and modules find each other

- Modules live on the **192.168.5.x** network (the AgOpenGPS convention).
- The app listens on port **9999**; modules listen on port **8888**. You never
  type these anywhere — it just helps to know when troubleshooting.
- The app broadcasts a "hello" and modules answer with their address. No pairing,
  no passwords.

## Ethernet (preferred)

1. Plug the module network into the tablet (directly or through a switch).
2. Give the tablet a fixed address on the same network, e.g. 192.168.5.10.
3. Open **Network IO**. Each module that answers gets a **green dot** and shows
   its IP address next to its name (GPS, AutoSteer, Machine, IMU).
4. Tap **Scan for Modules** if a dot hasn't come up yet.

Tick the checkbox beside a module you always carry. A ticked module that goes
quiet turns its dot **red** so you notice; an unticked, absent one stays **grey**.

## WiFi

Same as Ethernet — the modules just reach the tablet over a WiFi bridge or
access point instead of a cable. Keep everything on 192.168.5.x. WiFi adds
dropouts and lag, so use a cable for the steer module if you can.

## USB serial (fallback)

A module wired over USB speaks the same messages down the lead. In
**Network IO**, under **Module connection**:

1. Tap **Serial (USB)** (the default is **UDP (network)**).
2. Pick the **Port** and **Baud** (38400 is the usual for AgOpenGPS boards).
3. The status line should read **Open — … frames in, … out**. Frames counting
   up means the module is talking.

The choice is saved in a small file (`serial-bridge.json`) and comes back after
a restart. Tap **UDP (network)** to go back to Ethernet/WiFi. To the rest of the
app a serial module behaves exactly like a network one.

## The Network IO panel, top to bottom

- **Modules** — dot + IP per module. Green = heard from, red = expected but
  silent, grey = not expected and not heard.
- **Scan for Modules** — sends a fresh hello.
- **Module connection** — UDP (network) or Serial (USB), see above.
- **Subnet** — the three numbers of the module network (normally 192.168.5).
  **Send Subnet to Modules** re-addresses **every** module at once and restarts
  them. Only use it when deliberately moving to a different subnet.
- **Host IPs** — the addresses your tablet has right now. One of them should
  start with the same three numbers as the subnet above.
- **NTRIP** — RTK correction status; not a module, just shown here for convenience.

There is also a quick **Modules** button in the status bar (top of the screen)
with the same dots, so you can glance without opening the panel.

Rate control modules use their own channel and do **not** appear in the scan
list with the other four — the **Rate** row in the status-bar Modules popup
shows up only when the current tool uses rate control.

## When a module doesn't appear

Work down this list:

1. **Power and link lights** on the module and the switch.
2. **Cable** — try a different one; try a different switch port.
3. **Tablet address** — in Host IPs, is there a 192.168.5.x address? If not,
   the tablet isn't on the module network. Fix the adapter settings.
4. **Scan for Modules** again after fixing anything.
5. Firewall on the tablet — allow the app on private networks.

> **Do not diagnose with ping.** The nano machine boards (EtherCard-based)
> never answer ping even when they are working perfectly. A dead ping proves
> nothing; a green dot in Network IO proves everything.
