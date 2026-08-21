# Other devices and who is driving

**What this is.** AgOpenWeb is a web page served by the box in the tractor. The
cab screen is just a browser pointing at it — and so can a phone or tablet on the
same network. Any number of devices can watch, but only one is the **Operator**;
the rest are **Observers**. Only the Operator's device may steer, switch sections
and start turns. Any device can take the seat with two taps.

**You will need**

- A phone, tablet or laptop on the same Wi-Fi or wired network as the tractor box
- The box's address: open the left bar → **Network IO** and read it under **Host IPs**
- A modern browser (Safari, Chrome, Edge)

## Open the app on another device

1. In the browser, type `http://` then the box's address, then `:5174`
   (for example `http://<box address>:5174`).
2. The map appears. Look at the top-left of the status bar: the role badge says
   **Observer** (orange) if the cab is driving, or **No operator** (grey) if the
   seat is free.
3. Use the browser's *Add to home screen* to launch it without address bar or
   tabs. Or tap the **⛶** button at the far right of the status bar
   (*Fullscreen (hide browser bars)*), or turn on **File / Application Menu →
   App Settings → Start Fullscreen**.

## Take the seat

1. Tap the role badge (the coloured dot and word at the top-left).
2. A **Take control** box asks *Take operator control on this device? The
   current operator drops to Observer.* Confirm.
3. The badge turns green and says **Operator**. On every other device it flips to
   **Observer**, and the word *observing* appears under the right-hand buttons.

Nothing stops on the machine when the seat changes hands — only the *device
allowed to change things* moves.

## What an Observer can and cannot do

Live actions are dropped unless they come from the Operator — the box checks,
not just the screen.

| Observers CAN | Observers CANNOT (Operator only) |
|---|---|
| Pan and zoom the map, open any panel | **AutoSteer**, **Sections**, **Manual**, **U-Turn**, **Contour** (right-hand buttons) |
| Open or close a field, record a boundary, drop flags | Tap the section buttons along the bottom |
| Change vehicle, tool and app settings | Select, activate, swap or delete a track; start a new AB line |
| Plan routes, draw split lines, build tramlines | Turn headland mode on/off |
| Run the simulator | Play a recorded path or drive a planned route |
| Use **Scan for Modules**, set up **Serial (USB)** and **NTRIP Profiles** | **Send Subnet to Modules**, steer-wizard and calibration steps, rate-control changes |

Greyed buttons are the tell. If you tap one anyway, a hint says *Observer — tap
the role badge (top) to take control*.

## Using the full screen on a phone

There is deliberately **no cut-down mobile page** — the phone shows the same
layout as the cab, so nothing has to be re-learned.

- Pinching the **map** zooms the map. Pinching elsewhere is ignored on purpose,
  so the page itself can't run off the edge of the screen.
- Panels open over the map and close with **✕**; the floating rate and switchbox
  panels can be dragged out of the way.
- Turn the phone sideways for the planner and settings panels.

## Modules over USB instead of the network

If a module is plugged into the box by USB instead of the network:

1. Left bar → **Network IO** → under **Module connection** tap **Serial (USB)**.
2. Choose the **Port** and **Baud** (38400 is the usual AgOpenGPS rate).
3. The line below reads *Open — N frames in, N out* when it is working, or
   *Not open* with the reason if not.
4. Tap **UDP (network)** to go back to the network.

## Finding modules and moving them to a new subnet

- **Scan for Modules** asks every module on the network to answer. The dots next
  to GPS, AutoSteer, Machine and IMU go green as they reply, with their address
  alongside. Tick a module's box to say *it should be here* — then a missing one
  shows red instead of grey.
- **Subnet** lets you type the first three numbers of a new address range and
  **Send Subnet to Modules**. As the hint warns, it *Changes ALL modules at once,
  then restarts them* — Operator-only, confirmed first. Do it at a standstill
  with every module powered up.

## Things to know

- The Operator's device must stay in touch. If it goes quiet for about five
  seconds — Wi-Fi dropped, browser killed, phone locked — the box treats it as
  a lost driver: AutoSteer disengages, sections turn off, and any path or
  route being driven stops. The badge on other devices goes to **No operator**;
  tap it to carry on from there.
- Refreshing the Operator's page does not lose the seat for long — it is
  reclaimed automatically once the old connection clears.
- The badge says **Disconnected** in red when the device can't reach the box at
  all. Check the network before blaming the app.
- Tapping a switch on the on-screen switchbox from an Observer device takes the
  seat for you first — treat that as taking control, because it is.
- Settings you change on a phone are real settings on the box, not a local copy.
  Only things like which panels are open and the headland style stay per-device.
