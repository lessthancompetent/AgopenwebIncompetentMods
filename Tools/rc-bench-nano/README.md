# RC bench rig — a fake flow meter for an AOG_RC module

A dry bench gives a rate module nothing to measure, so most of the rate stack
can't be exercised: no pulses means no measured rate, no quantity totals, no
honest catch-test calibration, and no way to tune the valve PID before you're in
the paddock with no Windows machine to fix it. This Nano supplies the missing
half — and in closed-loop mode, a whole fake sprayer.

## Flash

```bash
arduino-cli compile --fqbn arduino:avr:nano tools/rc-bench-nano
arduino-cli upload  --fqbn arduino:avr:nano -p COM7 tools/rc-bench-nano
```

Older Nanos need the old bootloader: `--fqbn arduino:avr:nano:cpu=atmega328old`.
Then open the serial console at **115200** and press `?`.

## Wiring

| Nano | | RC module | Purpose |
|---|---|---|---|
| D9 | → level shift → | FLOW pin | pulse output (the fake meter) |
| A1 | ← RC filter ← | PWM / IN2 pin | senses the valve command |
| D4 | → level shift → | BIN pin | optional bin-empty simulation |
| GND | ↔ | GND | **required** common ground |
| A0 | ← 10k pot wiper (ends to 5V / GND) | | manual flow dial |
| D2 | ← momentary button to GND | | cycles mode |

**3.3 V warning.** ESP32-based RC modules are 3.3 V. Do not drive their inputs
from a 5 V Nano directly — use a 3.3 V board (Pro Mini 8 MHz) or divide D9 and
D4 down (1.8k series, 3.3k to ground ≈ 3.2 V). Reading the module's 3.3 V PWM
into a 5 V Nano is fine; the analog sense wants an RC filter (10k series, 10 µF
to ground) so the duty cycle arrives as a steady voltage.

## Modes

- **OFF** — no pulses.
- **MANUAL** — a flow you dial in on the pot or set with `f`. Proves the chain
  end to end: pulses → module → AgOpenWeb readout → quantity totals → job record.
- **LOOP** — a virtual sprayer. Reads the module's valve PWM and returns the
  flow that valve would give, with a first-order lag for the plumbing, so the
  module's PID has something real to control. This is the mode that makes bench
  tuning of Kp / Ki / deadband possible.

## Serial commands

```
m0/m1/m2  mode: off / manual / closed-loop
c<val>    meter calibration, pulses per unit  (MATCH the module's setting)
f<val>    manual flow, units/min (takes over from the pot)
p         hand control back to the pot
x<val>    max flow at full PWM (also the pot's full scale)
t<val>    plumbing lag, seconds
v<val>    module logic volts for the PWM sense (3.3 or 5.0)
b0/b1     bin-empty simulation off / on
s         status        ?  help
```

## The maths

A module counts pulses and divides by its meter calibration, so to imitate a
flow of `R` units/min at calibration `C` pulses/unit:

```
Hz = R × C / 60          e.g. 25 L/min at cal 180 → 75 Hz
```

Keep the result inside the module's own pulse window (its defaults gate roughly
1–1500 Hz) or it discards the readings. `c` here must match the calibration the
module is running, otherwise you are testing two different assumptions against
each other.

## Suggested bench sequence

1. `m1`, pot to mid — confirm the rate readout in AgOpenWeb shows flow and the
   quantity total climbs.
2. `b1` — confirm **BIN EMPTY** appears on the readout, then `b0`.
3. Run a catch test from the Rate panel: `f` a known flow for a known time and
   check the calibration it computes matches.
4. `m2` and set the product to Auto — the module now controls a fake valve.
   Tune Kp / Ki in Module Setup and watch actual chase target on the readout.
   Raise `t` to imitate slower plumbing and see the tuning that survives it.
