# Rate control: switchbox, readout and alarms

**What this is.** Rate control brings AgOpenGPS's separate RateController app into
AgOpenWeb. This guide covers the bits you touch while driving: the floating
switchbox, the rate readout, the physical switchbox, and the drop-off alarm.

**You will need**

- A tool with **Rate control** turned on (Tool setup) and at least one product
  enabled in the **Rate** panel
- An AOG_RC rate module on the network (an RC15 or similar), optionally the
  physical AOG_RC switchbox

## The floating switchbox

A strip of big buttons you can drag anywhere by its **⋮⋮** grip (it remembers
where you put it). Left to right:

| Button | What it does |
|---|---|
| **MST** | Master valve on/off |
| **PRM** | Primed start — run the metering before you move |
| **AUTO RATE** | Hold the target rate automatically |
| **AUTO SECT** | Sections follow coverage automatically (same as the right-hand section button) |
| **S1 S2 S3…** | Section groups — one press flips the whole group |
| **+ / −** | Nudge the target rate 5% per press (vertical rocker, + on top) |

Show or hide it with the **SW** button on the rate readout, or
*Rate → Switches → Show on main screen*.

## The rate readout

A small draggable box showing **current / target** for every enabled product, in
the product's units. Its header has **SW** (show/hide the switchbox) and **☰**
(open the Rate panel). A reading more than 10% off target while flowing turns
amber; an empty bin shows **BIN EMPTY**.

## The physical switchbox

Plug in an AOG_RC switchbox and it just works alongside the on-screen one: master,
auto/manual, rate up/down and the section toggles do exactly what their on-screen
twins do. Its connection shows in **Network IO → Rate control**, along with every
rate module the app can hear. A box your tool normally carries is marked
*expected* automatically the first time it talks — untick that if you remove the
box for good.

## Drop-off alarm

If a rate module an enabled product depends on — or the switchbox — stops
answering, a red banner flashes top-centre and the tablet beeps every few
seconds. **Tap the banner** to silence it; it stays dim until the module comes
back or something else drops. Turn the screen flash and the beep on or off
separately with the **Screen** / **Sound** buttons under *Network IO → Rate
control → Drop-off alarm*.

The alarm also catches a module that is dead at power-on (after a 30 s grace for
boards that boot slower than the app), not just one that drops mid-field.
