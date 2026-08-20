# Route Planner basics

**What this is.** The Route Planner draws a complete driving route for a whole
paddock — headland laps, every pass, every turn — before you start. You can
check it, simulate it, and then steer it with the normal autosteer button.

**You will need**

- A field open, with a boundary
- Left bar → **Field Tools** → **Route Planner**

The panel has four tabs: **Pattern**, **Headland**, **Drive**, **Check**.

## Pattern tab — what shape to drive

Five patterns, one line each:

- **Auto** — straight passes; the planner tries several directions and picks
  the fastest.
- **Skip** — leap-frog row order, so every turn is a wide, easy U-turn.
- **Cross** — two full coverages at right angles (seed drilling).
- **Spiral** — one continuous spiral over the whole field, fence to centre.
- **Block** — works rows in blocks a set number apart.

Below the patterns: **Skip rows** / **Block skip** steppers (only for those
patterns), and **Angle °** to force a pass direction — 0 keeps the automatic
choice. While the panel is open a dashed line on the map previews the pass
direction live. **⇉ Along selected track** plans along the AB line or curve
currently selected in the Tracks manager instead.

### Split lines

For L-shaped or awkward paddocks, cut the field into simpler blocks that each
get their own pass direction:

1. Tap **✂ Draw split**.
2. Tap the FIRST point of the divider on the map, then the SECOND. The line
   extends itself to the boundary.
3. Blocks appear as buttons (A, B, C…). Tap one to select it, set an Angle if
   you want, and apply — each block remembers its own heading.

**Clear splits** removes them all.

## Headland tab

Laps count, style (Classic laps / Spiral in / Spiral out / None), order
(Headland first / Headland last) and the back-cut lap. See the separate
[Headland styles](headland-styles.md) guide.

## Plan Route / Clear

**Plan Route** (bottom of the panel) builds the route and draws it on the map;
the line under it shows the route's numbers. **Clear** removes the plan.

## Drive tab — following the plan

Planning also saves the route as ordinary tracks: **Route Main** (the whole
body of the field) and **Route Headland** (the laps), plus one per split block.
They show up in the Tracks manager like any AB line.

- **Steer: Main** / **Steer: Headland** — selects that track. Then engage
  autosteer with the normal button (or your engage switch) and the tractor
  follows the plan, turns included. You can swap between them any time.
- **▶ Drive (sim)** — a simulated tractor drives the whole route on screen with
  sections working, so you can watch the order and coverage before burning
  diesel. **■ Stop** ends it.
- The line below shows the live **time remaining**, learned from your actual
  work and turn speeds as you drive.

## Check tab — is the plan honest?

- **Spd W/T · s/turn · edge m** — the speed model used for comparing routes and
  for the ETA: working speed, turning speed (km/h), seconds lost per turn, and
  an edge offset that pulls the first pass in from the boundary (use it where
  the fence sits right on the mapped line; 0 if you drove the boundary as your
  first working pass).
- **Show missed spots** — tints unworked ground red underneath your coverage,
  so gaps stand out at a glance.
