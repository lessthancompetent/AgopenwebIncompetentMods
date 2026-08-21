# Building a field

**What this is.** A field is a boundary plus what hangs off it: inner
boundaries for ponds and wet patches, a headland, flags, and the tracks you
steer on. This walks through building one from an empty paddock, using the
**Boundary** and **Field Builder** tools and the **Quick AB Line** dialog.

**You will need**

- A field open (left bar → **Field Operations**)
- Left bar → **Field Tools** — **Boundary** and **Field Builder** live there
- The bottom bar: the flag button (**Flags**) and the track button
  (**AB line options**) at the right-hand end

## Record the boundary by driving it

1. **Field Tools** → **Boundary** opens *Start or Delete Boundary*. Tap the
   steering-wheel icon (*Drive around field to record boundary*).
2. In *Stop Record Pause Boundary*, set **Recording Offset** in cm — how far
   past the tool edge the fence really is.
3. Tap the left/right icon (*Record on left / right side*) so the recorded
   edge is on the fence side, and the antenna/tool icon (*Record at antenna /
   tool*) to record at the implement, not the receiver.
4. Tap *Section control while recording* if you want sections running.
5. Tap the red **Record / Pause** button and drive. **Points** and **Area**
   count up. Pause at a gateway, resume after.
6. Tap the green tick (*Stop and save*).

Or tap the satellite icon (*Draw boundary on satellite imagery*) and tap the
corners, then **Finish**. Drawn boundaries are outer only.

## Ponds vs wet patches: inner boundaries

An inner boundary is a ring inside the field. Two flags in the list decide
what it does:

| Column | Shows | Meaning |
|---|---|---|
| **Drive Thru** | `--` / `Yes` | `--` = keep out: sections shut off inside it and the planner routes round it. `Yes` = just a marker; work straight through. |
| **Hard** | `Soft` / `Hard` | `Hard` = something solid at the edge. U-turns and planned passes keep the **implement body** clear. `Soft` = the implement may swing close. |

- **Pond, trough, power pole:** `--` and **Hard**.
- **Wet patch to skip today:** `--` and Soft. Flip to `Yes` when it dries.

Add one with the boundary-plus icon (*Drive around obstacle (inner
boundary)*) and drive round it, or for a quick pole or hole use **Field
Tools** → **Obstacles** → **Place obstacle on map** (always hard). Tap a flag
cell to toggle it; edits save at once.

## Headland

1. **Field Builder** → **Headland** tab → **＋ Add**.
2. Choose **Inset**: 1 to 6 tool widths, or **Custom** metres.
3. **Whole Boundary** gives one inset ring. Or draw it edge by edge with
   **Line** / **Curve**: tap the start and end of each edge on the boundary,
   repeat, then **Finish**. A green dot in the list means that edge reaches
   the boundary at both ends; red means it has no effect.
4. On the bottom bar, *Headland on/off* turns it on; *Section control in
   headland* beside it decides whether sections run in there.

## Flags

Bottom bar → **Flags** → **Place Flag Here** drops one at the tractor;
**Place Flag on Map** lets you tap the spot. **Flag List** renames, recolours,
locates, deletes.

## Tracks

Tap *AB line options* at the bottom right, then the **Quick AB** icon to open
**Quick AB Line**:

| Section | Button | What you do |
|---|---|---|
| By Driving | **A+** | One tap: a line from where you are, on your heading. |
| | **Drive AB** | **Set Point A**, drive, **Set Point B**. |
| | **Curve** | **Set Point A**, drive the shape, **Set Point B**. |
| From Field Boundary | **Longest Edge** | AB line along the longest fence. |
| | **Bnd. Curve** | Tap two points on the fence; a curve follows it between them, half a tool width inside. |
| | **All Edges** | One AB line per fence edge. |
| Draw on Map | **Straight** | Tap A, tap B. |
| | **Curve** | Tap points, **Undo** if needed, **Finish**. |

**Extending a boundary curve.** After **Bnd. Curve** the toolbar stays up with
**A++**, **A−−**, **B−−**, **B++**: each tap walks that end 5 m along the fence,
longer or shorter. **Done** when it is right.

**Nudge and snap.** With a track active, the track fly-out has *Nudge left
(A+)* / *Nudge right (B+)* (one step — set **Nudge distance (cm)** under
AutoSteer → **Display**), *Fine nudge* at a quarter step, **<<** / **>>** for
half a tool width, and **0** to reset. On the bar, *Snap to left track* /
*Snap to right track* jump a whole pass and *Snap to pivot* pulls the line
under the tractor.

To tidy a track: **Field Builder** → **Tracks** → **✎ Edit**, drag its points
on the map, **Save**. **Rename**, **Delete** and **Delete All** sit beside it.

## Contour — not working yet

The **Contour** button on the right-hand bar only flips a flag. The
follow-the-previous-pass steering behind it is not connected, and nothing on
the web screen records a contour. Use a driven **Curve** track instead.

## Tram lines — partly working

**Field Builder** → **Tram** → **＋ Add** makes a tram system off the active
track; the editor (**Reference**, **Tram width**, **Wheel Track** / **Edge**,
**Left** / **Both** / **Right**, **Passes**) redraws the lines as you change
it. Not connected: the lines drive nothing — no tram relay output, no pass
counting, no manual override — and the **Tram** tab in Tool Configuration is
saved but never read. A drawing aid for now.

## Things to know

- Driven boundaries start as keep-out and Soft; set the flags after saving.
- A **Bnd. Curve** steers the fence line itself, not a pass stepped in from
  it — deliberate, for the lap against the fence.
- Deletes cannot be undone. Buttons that change the field only work from the
  screen that holds control of the tractor.
