# The work switch

**What this is.** A work switch is a wire from your implement to the steer or
machine board that tells AgOpenWeb "the tool is in the ground / spraying now".
With it set up, sections (and rate control, if fitted) follow the implement
instead of you tapping the screen.

**You will need**

- A switch or button wired to the WORK input of your steer or machine board
- The module connected and green in Network IO
- The settings under **Vehicle & Tool Configuration** (tractor icon in the left
  bar) → **Tool Configuration** → **Tool** tab → **Switches** sub-tab

If both boards have a work input wired, the machine board's switch wins.

## The settings

1. **Work switch** — turn it **On**. The rest only appears when this is on.
2. **Switch type**
   - **Maintained switch** — a lever or implement-mounted switch that *stays*
     on while working (e.g. closes when the drill lowers). The switch position
     IS the answer.
   - **Momentary button** — a push button. Each press flips work on/off, like
     a light switch you tap. The screen hint says the same: "Momentary: each
     press toggles work on/off".
3. **Active state**
   - **Active low (closed)** — working when the switch is *closed* (wire pulled
     to ground). This is the usual wiring.
   - **Active high (open)** — working when the switch is *open*.
   If your sections come on when the tool is UP, this setting is backwards —
   flip it.
4. **Controls**
   - **Auto sections** — the switch arms the automatic section master. Sections
     then still respect boundaries and already-covered ground.
   - **Manual sections** — the switch forces all sections on, no questions
     asked. For tools with no coverage logic wanted.

There is a separate **Steer switch** block below for a switch that follows the
autosteer engage instead — that is not the work switch.

## What the work switch gates

- **Sections** — switch on = section master on (Auto or Manual per the setting
  above); switch off = sections off.
- **Rate control** — in **Rate Control** → **Switches** tab there is
  **Work switch gates master**. Turned on, product only flows while the work
  switch says the implement is down. The same tab shows the live state as
  **Work switch now: ON (implement down)** or **off**.

## Bench test (5 minutes, nothing moving)

1. Connect the board as usual; check its dot is green in **Network IO**.
2. Open a field (or the simulator) so the section buttons appear on the right
   edge of the screen.
3. Set **Work switch: On**, type **Maintained switch**, **Active low (closed)**,
   **Auto sections**.
4. Short the WORK input to ground (or flip the real switch). The **Sections**
   button on the right edge should light up on its own.
5. Open the circuit. Sections should drop out.
6. If it's inverted, change **Active state** and repeat.
7. Using a push button? Set **Momentary button** and check each press toggles
   work on, then off, then on.
8. If you run rate control: open **Rate Control** → **Switches** and watch
   **Work switch now** flip between **ON (implement down)** and **off** as you
   operate the switch.

Nothing happens at all? The app ignores switch inputs until it is receiving
live data from the module — check the module dot first, not the wiring.
