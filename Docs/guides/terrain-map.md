# Terrain — field elevation map

Terrain builds an elevation map of your field for free, just from driving it.

While a field is open, AgOpenWeb quietly samples your GPS altitude as you
drive — roughly one reading every 2 metres, averaged into 5 m squares. Over a
season of passes those squares become a solid picture of how the field sits:
where the high ground is, where water will run, which hollow always stays wet.
Useful when you're planning drainage, surface shaping, or just deciding which
end to start on after rain.

There is nothing to switch on for recording. If a field is open and you're
moving, it's being recorded. The map is saved with the field (`elevation.json`
in the field's folder) and keeps improving every time you work the field.

## Seeing it

Open **Screen & Alerts** from the left menu and look under **Map Background**:

- **Terrain** — shades the field by recorded elevation, from blue (lowest)
  through green and yellow to red (highest). A small legend in the bottom-left
  corner shows the recorded low and high in metres.

The shading sits under your coverage and route lines, so you can leave it on
while working. Turn it off and it costs nothing.

Where you haven't driven yet there's simply no shading — the map only knows
the ground you've actually crossed. Headland laps plus your normal passes will
fill a field in quickly.

## A note on accuracy

The quality of the map is the quality of your altitude fix:

- **RTK fix** — altitude is centimetre-grade. The map is genuinely good enough
  to read fall across a paddock and plan drainage runs.
- **No RTK** (plain GPS/DGPS) — altitude can wander by several metres over a
  day. The broad shape will still show, but don't trust small differences, and
  expect passes driven on different days to disagree.

Each 5 m square stores the average of every sample that ever landed in it, so
repeated passes smooth the noise down over time.

## For the record keepers

When a job's coverage record is exported, an `elevation.geojson` (one point
per 5 m square, with its altitude) is written alongside `coverage.geojson` in
the job folder — so the off-board viewer can use the same elevation data.
