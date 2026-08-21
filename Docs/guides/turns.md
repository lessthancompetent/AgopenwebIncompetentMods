# Turning at the headland

**What this is.** With a track active and autosteer on, the app plots the turn
for the end of each pass well ahead, counts you down to it, and drives it. You
choose a round **U** turn or a three-point **K** turn, which way to fold, and
how many rows to skip. Near a hard fence it checks that the *implement* — not
just the tractor — will clear, and refuses a turn that won't.

**You will need**

- A field with a boundary and a headland (left bar → **Field Tools** →
  **Field Builder** → **Headland** tab → **＋ Add**)
- An active track and autosteer engaged
- Turn settings: left bar → **Vehicle & Tool** → **Tool Configuration** →
  **U-Turn** tab — **Radius (m)**, **Extension (m)**, **Smoothing**,
  **Distance from bndry (m)**

## Automatic turns

1. Tap the **U-Turn** button on the right-hand bar. Its icon shows whether auto
   turns are on.
2. Drive the pass. As soon as the app can see the headland crossing ahead it
   draws the turn in green on the map, and a readout appears top-right, just
   left of the right-hand bar: a green arrow showing which way you will fold,
   and a distance counting down along your track.
3. At **0 m** the turn fires, autosteer drives the arc and lands you on the
   next pass.

Pass the plotted start without it firing — too fast, or more than about 20°
off the line — and the turn is dropped and re-plotted for the next pass.

## Direction and turn type

The readout is two buttons:

| Tap | What happens |
|---|---|
| The **arrow / distance** | Swaps the pending turn left ↔ right; the green path redraws. Only before the turn fires — mid-arc it does nothing. |
| The **U / K glyph** beside it | Switches between a **U** (Sagitta) turn and a **K** turn. Same setting as **Turn style** on the **U-Turn** tab; the next plotted turn uses it. |

## K turns — for mounted implements

A U turn needs roughly two turn radii of headland; a mounted drill, mower or
power harrow on a narrow headland often hasn't got that. A K turn is a
three-point turn:

1. Autosteer drives the forward arc as far round as it can.
2. Stop, select reverse and back up. The moment the app sees you reversing it
   counts the turn done and hands guidance to the next pass **while you are
   still backing** — the nose swings onto the new line and you settle backing
   straight along it.
3. Select forward and carry on. Sections come on at the headland line as usual.

Before you rely on it:

- **AutoSteer Configuration** → **Algorithm** tab → **Steer in reverse**: **On**.
- **Tool Configuration** → **Tool** → **Hitch** → **K-turns (reverse leg)**: leave
  **Allowed** for mounted gear. Set **Never (trailed)** for anything on a drawbar
  — trailed implements jackknife in reverse (this also keeps Route Planner turns
  forward-only).

## Skipping rows

Two buttons in the bottom bar (shown once a track is active):

- The numbered button — **U-Turn skip rows (tap to cycle 0–9)**. 0 turns onto
  the next pass; 1 leaves one pass between, and so on — easier turns with a
  long implement.
- Beside it, **U-Turn skip rows on/off**. **On** starts the *snake* pattern:
  the app plans the pass order from where you are, works out one way, then
  comes back and fills the passes it skipped, so nothing is left unworked.
  **Off** is a plain fixed skip.

## Manual turn

Turn on **Screen & Alerts** → **On-Screen Buttons** → **U-Turn**. Two yellow
arrows appear over the map: **Manual U-turn left** and **Manual U-turn right**.
Tap one at the end of a pass to turn from where you are (autosteer must be on).

## Hard fences and long implements

Mark a boundary **Hard** in the boundary list (**Start or Delete Boundary**, the
**Hard** column) where a fence or drain must not be crossed. The app then traces
where the whole implement will swing — from **Implement length (m)** and
**Physical width (m)** on the **Hitch** tab, plus the **Distance from bndry (m)**
margin — and:

- pulls the turn further inside until the implement clears; or
- if no forward turn clears, plots **no turn at all**. The readout stays hidden
  and the status line reads *U-turn blocked: implement would swing into a hard
  boundary — take over manually*. A manual turn that won't fit is refused too:
  *Not enough room — turn would put the tool past the boundary*.

So an empty readout at the end of a pass means no turn is plotted — turn by
hand, widen the headland, or switch to **K**.

## How wide a headland?

Start with **twice the Radius plus the Distance from bndry margin**, plus the
implement length for mounted gear — a 6 m radius and 1 m margin is about 13 m,
two laps of a 6 m tool. A K turn gets away with roughly one radius plus the
margin.

## Things to know

- Turns need a headland line; with none built, nothing is plotted.
- **Radius (m)** is the tightest arc the app will draw — set it to what the
  tractor can really steer with the implement on. **Extension (m)** adds
  straight run either side of the arc.
- The K turn's reverse leg is not clearance-checked against a hard fence; only
  the forward arc is. Back over the boundary and autosteer drops out.
- **U-Turn Compensation** on the **Algorithm** tab nudges every turn wider or
  tighter if the tractor consistently lands inside or outside the next pass.
