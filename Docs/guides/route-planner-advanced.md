# Route Planner — beyond the basics

**What this is.** Past the [basics](route-planner-basics.md), the planner
has a second layer: cross-drilling, paddocks cut into blocks, obstacles,
plans that survive a rain stop, and switches that keep the route honest on
real ground.

**You will need**

- A field open, with a boundary
- Left bar → **Field Tools** → **Route Planner** (also **Obstacles** and
  **Tank Mix** there for two sections below)

## Choosing a pattern

| Pattern | How it drives |
|---|---|
| **Auto** | Back-and-forth, each pass beside the last. |
| **Skip** | Leap-frog: miss **Skip rows** passes, come back for them — every turn a wide, easy U. |
| **Block** | Works bands **Block skip** passes wide, then moves on. |
| **Cross** | Two complete coverages, the second across the first (drilling). |

**Automatic pass angle.** With **Angle °** at 0, the planner tries the longest
fence, the angle square to it and a sweep between, times each with your
Check-tab speeds, and builds the fastest; the status line under **Plan
Route** shows the angle it chose. Any other value overrides it.

## Cross-drill

1. Pick **Cross**.
2. Set **Row spacing cm** to your drill's row spacing, or leave it **Off**.
   With a spacing set, the headland laps are driven twice, the second set
   shifted half a row so its rows fall between the first set's (steer track
   **Route Headland 2**).
3. Tap **Plan Route**.

**Weave (V-turns).** Normally the whole paddock is drilled one way, then
the other. Turn **Weave (V-turns)** on and both directions are worked
together: one leg one way, a gentle V-turn in the side headland, one leg the
other way — a W down the paddock, then parallel Ws back. Far gentler than
180° keyholes. Paddocks with obstacles fall back to the two-coverage order.

**The darker shade.** The second coverage paints darker because it is a
separate coverage record. Section control on that pass reads only its own
record, so sections stay on over ground the first pass painted instead of
shutting off at every crossing.

## Split lines and blocks

For an L-shaped or wedge paddock, one compromise angle wastes turns:

1. Tap **✂ Draw split**, then tap two points across the paddock; the line
   extends itself to the boundary.
2. Blocks appear under **Blocks (tap to select)** as A, B, C…
3. **Plan Route** gives every block its own best angle, with headland laps
   once around the whole paddock and links between blocks.
4. To redo one block: select it, set **Angle °** (0 = automatic) and tap the
   apply button under the block list. Other blocks keep their paths.

**Clear splits** removes them.

## Planning along a track

Under **Reference track**, pick a saved AB line or curve and tap **⇉ Plan
along it**: an AB line fixes the direction, a curve makes every pass follow
its shape. **✏ Plot A–B** takes two map taps instead — it creates the AB
line, selects it and plans along it. Both bypass split blocks.

## Obstacles and ponds

- **Ponds, dams** — record them as inner boundaries. With **Drive Thru** off
  the pond is a hole: passes stop at its edge and the route goes around.
- **Poles, holes, hoses** — **Field Tools** → **Obstacles**: pick **Pole**,
  **Hole** or **Hose**, set **Width m** / **Length m**, tap **Place obstacle
  on map**, then the spot. **Delete obstacle (tap it)** removes one.
- Anything narrower than your working width is swerved, not routed around,
  using **Physical width (m)** (Tool Configuration → **Hitch**) — the real
  frame width, not the spread.

## Pause, come back, resume

The plan is saved with the field. Rain, refill, breakdown — just stop.
Reopen the field and the same paths come back (**Saved route plan
restored**). On the **Drive** tab, **Steer: Main** or **Steer: Headland**
picks the track again; engage autosteer and carry on. If you swapped
implements meanwhile, the status line warns **REPLAN before drilling**.

**Already-worked laps are skipped.** If you recorded the boundary with the
tool working, that lap is already painted. Any outer lap more than 80%
covered is dropped at plan time; the status line reads, for example,
**1 already worked — skipped**.

## K-turns and trailed implements

A K-turn backs up mid-turn; the planner only uses one where a forward turn
cannot fit, but a trailed drill will jackknife. In Tool Configuration →
**Hitch**, set **K-turns (reverse leg)** to **Never (trailed)** and every
planned turn stays forward-only.

## Slope-corrected spacing

On a side slope, passes a full width apart on the map sit further apart on
the ground, and the slivers add up across a face. With **Terrain** data
recorded for the field, the Check tab's **Slope-corrected spacing** toggle
(on by default) tightens spacing on slopes so ground coverage stays a full
tool width. It does nothing until the field has Terrain data.

## Refill points

In **Field Tools** → **Tank Mix**, with a carrier rate and tank size entered,
turn on **Show refill points on route**. An amber dot marks the planned route
wherever a tank runs dry.

## Things to know

- Steering by hand on a cross-drill job, the two coverages are told apart by
  heading. The shifted second headland set cannot be — steer **Route
  Headland 2** for it.
- Splits, block angles, obstacles and the plan all live in the field folder,
  so they follow the field to another tablet.
