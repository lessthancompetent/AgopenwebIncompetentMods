# Setting up rate control

**What this is.** Rate control lets AgOpenWeb meter product — spray, fert, seed —
through an AOG_RC module (RC15 / Teensy / Nano) with no separate RateController
app. This is the one-off setup; the driving-time bits (switchbox, readout,
alarms) are in
[Rate control: switchbox, readout and alarms](rate-switchbox.md).

**You will need**

- A rate module on the network, showing in **Network IO** (see
  [Connecting your modules](module-connections.md))
- The implement loaded as the active tool
- For a new or reflashed module: only that one board powered

## 1. Turn it on for the implement

Rate control belongs to the **tool** — a sprayer meters, harrows don't.

1. Left bar → **Vehicle & Tool Configuration** → **Configure Tool…** →
   **Machine** → **Rate Control**.
2. Set **Use rate control** to On. **Modules heard** lists the boards answering.
3. Pick an **On-map readout**: **Off / Current / Target / Both**.
4. **Products & rates…** opens the Rate panel, **⚙ Module setup…** the module
   screen. Both are also under **Field Tools** → **Rate Control**.

## 2. Products and channels

The Rate panel has five channel tabs, **A**–**E** — one per flow meter and
valve on *this* implement. A product is a separate, shared thing:

| | Belongs to | Holds |
|---|---|---|
| Catalogue | Everyone | Name, units, usual rate. One "DAP" for spreader *and* drill. |
| Channel | This tool | Module/sensor, **Flow cal**, tank size, control type. Never copied between implements. |
| Job record | This job | What actually went on. Set via **Field Tools** → **Job Product**. |

On the **Rate** tab pick the **Product** — name and usual rate load, the
channel's calibration stays put. To add one, type a **Name**, set **Target
rate** and its unit (**/Ha**, **/Ac**, **/min**, **/hr**), press **+ Cat**.
**Reset** returns a nudged target to the catalogue rate.

On the **Channel** tab set **Enabled**, **Auto**, **Module / Sensor** (sensors
count from 0; a warning appears if you point past the module's sensor count),
**Control type** (**Standard valve**, **Combo close**, **Motor**, **Motor +
weights**, **Fan**, **Combo timed**) and **Flow cal (pulses/unit)**.

## 3. Module setup (⚙)

These live in the module's own memory; AgOpenWeb keeps a copy against the tool
so a swapped board can be recommissioned from the cab. Nothing can read them
*back*, so first time round you type in what the board already has.

Choose the **Module** (**+** prepares an unused ID) and **Sensor**, then:

| Tab | What's there | Send with |
|---|---|---|
| **Commission** | **Board**: **ESP32 (RC15)**, **Teensy 4.1 (RC11-2)**, **Arduino Nano (RC12-3)**; **↺ Load defaults for this board** | **Send everything to this module** |
| **Module** | **Sensor count**, **Work pin**, **Pressure pin**, **Wiring** (3-wire / 2-wire), **Invert direction**, **Onboard relays**, **Remote relays** | **Send module config** |
| **Pins** | **Flow pin**, **Dir pin (IN1)**, **PWM pin (IN2)**, **Bin pin**, 16 **Relay output pins** | **Send pins (module restarts)** |
| **Relay fn** | R1–R16: **Section**, **Master**, **FlowMaster**, **Power**, **Bypass**, **Switch**… **Renumber sections**, **Reset to sections** | goes out with section state |
| **Tuning** | **Min power %**, **Max power %**, **Gain (Kp)**, **Integral (Ki)**, **Deadband**, **Brake point %**, **Slow adjust %**, **Slew rate**, **Flow samples** | **Send valve tuning** |
| **Network** | Wired subnet (address = subnet + 50 + module ID) | **Set wired subnet (module reboots)** |

**New board:** Commission → **Board** → **↺ Load defaults for this board**
(fills the form only) → review → **Send everything to this module** → **Assign
this ID to the connected board**. A module takes the ID unconditionally, so the
button refuses while more than one module is answering — power only the board
being commissioned. 255 in a pin box means not connected.

**Tuning order:** **Min power %** to the least that moves the valve, **Max
power %** 100, **Integral (Ki)** 0. Raise **Gain (Kp)** until the rate
overshoots, back off, then add a little integral. A 2-wire motorised valve
settled within 1% at Kp 20, Ki 0, Deadband 30, Brake point 20, Slow adjust 30,
Slew rate 20. Valve runs the wrong way? Toggle **Invert direction**.

## 4. Calibrate (catch test)

**Channel** tab, **Flow calibration (catch test)**:

1. Set **Manual PWM**, hold a container under the outlet, press
   **▶ Start cal run**.
2. Press **■ Stop**. **Indicated:** shows what the meter thinks went out.
3. Type what you actually caught, press **Apply**. **Flow cal** rescales; the
   test quantity comes back out of the job total.

## 5. Switches and primed start

**Switches** tab:

- **Master controls** — **Master + all sections**, **Master relay only**, or
  **Always on (override)**.
- **Switch type** — **Momentary** or **Maintained** (physical box only;
  maintained disables primed start).
- **Work switch gates master** — master only engages with the implement down;
  **Work switch now** shows the live state.
- **Section → switch allocation** — which switch **S1**–**S8** flips each
  section. Same switch on several sections = one group (native "zones").

**Primed start** tab: **Run time (s)**, **Simulated speed (km/h)** (target is
worked out as if moving at this speed while parked), **Hold delay (s, physical
box)**, **Resume if moving after**. **▶ Prime now** runs it like the PRM
button. Turn on the sections you want primed first.

## Things to know

- Everything here is per tool and changes with the implement.
- Two ID-0 boards (one wired, one WiFi) slip past the assign guard, which counts
  distinct IDs. Change one with the other unplugged.
- A module obeys any host. Two tablets enabled on the same module fight
  silently; the rate parks between their targets. Disable the spare.
- A module must be *heard* before a Send can reach it.
- A reflash wipes the module: set the subnet on **Network**, then **Send
  everything to this module**.
