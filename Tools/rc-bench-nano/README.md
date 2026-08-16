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

The module is a **12 V board**: its sensor inputs are optocoupled and its valve
outputs swing to 12 V, so neither side connects to the Nano directly.

### Pulse output — imitate the flow sensor

The firmware sets the flow pin `INPUT_PULLUP` and counts **rising** edges, so
the input is a *sinking* type: a real hall flow meter pulls it to ground and
releases it. Do the same with a transistor.

```
D9 --[1k]--> B (2N2222 / BC547)
             E --> module GND
             C --> module FLOW terminal
```

A 2N7000 MOSFET works identically (gate / source / drain). One pull-and-release
per cycle = one counted edge, so the frequency arrives intact. **Never** wire
the Nano pin straight to the terminal — with the board's pull-up it may sit at
12 V.

### PWM sense — divide and filter in one go

```
valve output --[10k]--+--> A1
                      |
                     [3.3k]  and  [10uF] to GND
```

12 V in gives ~2.98 V at A1 (that's the `v` setting) and about 25 ms of
smoothing, so the duty cycle arrives as a steady voltage. Measure yours with a
meter at full command and set `v` to what you actually see — the whole closed
loop is scaled off that number. If the output is **low-side switched** it reads
backwards (high with no command): flip it with `i1`.

### The rest

| Nano | | Purpose |
|---|---|---|
| D4 | → same NPN arrangement → module BIN terminal | bin-empty simulation |
| A0 | ← 10k pot wiper (ends to 5 V / GND) | manual flow dial |
| D2 | ← momentary button to GND | cycles mode |
| GND | ↔ module GND | **required** — both the divider and the transistor emitter reference it |

### Check before connecting

With the module powered and nothing on the flow terminal, measure it against
ground. Sitting near 12 V (or 3.3/5 V) confirms the pull-up and the sinking
arrangement above. Sitting at 0 V means it expects to be *driven* instead — stop
and tell me, because the drive circuit is then different.

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
v<val>    volts at A1 when the valve is FULL (10k/3.3k on 12V = 2.98)
i0/i1     PWM sense normal / inverted (low-side switched output)
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
