# Offset implements

**What this is.** Some implements don't sit centred behind the tractor — an offset
drill, a side-mounted mower, a spreader hung to one side. AgOpenWeb models that
offset everywhere: the tractor steers a line *beside* the pass so the **implement**
lands on it, U-turns size themselves to the real spacing, and the Route Planner
plans drive lines that keep the worked bands seam-tight.

**You will need**

- **Tool setup → Offset**: the distance from the tractor centreline to the
  implement's working centre, in metres. **Positive = implement to the RIGHT** of
  the direction of travel. Limited to ±5 m.
- That's it — nothing else to set. Leave it at 0 for a centred implement.

## What you'll see

- The magenta guidance line sits to one side of the pass, and swaps sides when
  you drive the other way. That is correct: the line is where the *tractor*
  goes; the implement is on the pass.
- U-turns alternate between a tight one and a wide one. With a 6 m implement
  offset 1 m, turns are 4 m one way and 8 m the other — the implement still
  steps exactly 6 m.
- Painted coverage lands on the pass, not on the tractor's wheel marks.

## With the Route Planner

Plan as normal. The plan's drive lines are already shifted for your offset, and
the plan remembers the offset it was made with. If you later open that plan with a
different implement, the status line warns **"REPLAN before drilling"** — do that,
or the bands will be off by the difference.

## Things to know

- Reversing flips which side the implement is on, and the app follows that — the
  K-style turn's reverse leg lands on the right line.
- The planner keeps the implement **body** clear of a hard fence (see
  *Tool setup → Physical width* and *Length*): a long mounted implement swings
  wider than the tractor in a turn, and planned turns make room for it.
- Headland laps shift with the lap direction; the outermost lap still dresses
  the fence.
