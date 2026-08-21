# Fields, jobs and coverage records

**What this is.** A **field** is the paddock itself — boundary, tracks,
headland, obstacles, route plans. It doesn't change from season to season.
A **job** is one piece of work done in that paddock: drilling in April,
urea in May, mowing in November. Each job keeps its own painted coverage,
product, rate and applied total, so a paddock that was drilled and then
fertilised holds *both* records side by side. When a job closes, its
coverage is written out as a map file that syncs off the tablet to the farm
server, where a web viewer shows every application by product.

**You will need**

- Left bar → **Field Operations** → **Fields and Jobs**
- Left bar → **Field Tools** for **Job Product**, **Applied Total**,
  **Export Coverage** and **Delete Applied Area**
- For the off-board viewer: Syncthing on the tablet, paired to the farm
  server (already set up)

## Start a job

1. Open **Field Operations** → **Fields and Jobs**. The left column lists
   your fields with distance and area; **Pick on Map** lets you tap the
   paddock on the aerial instead.
2. Tap a field. The middle column shows its existing **Jobs**.
3. Under **New Job — Work Type**, type or pick the kind of work. Add
   **Notes** if you like — **Use Last** copies the previous job's notes.
4. Give it a **Job name**; **Date** and **Time** stamp it for you.
5. Tap **Start New Job**. The field opens with a clean coverage map.

To carry on where you left off, pick the job and tap **Resume Job**, or use
**Resume Last Job** straight from **Field Operations**.

**Open Field Only** opens the paddock with no job. Anything you paint then
has nowhere to live: on close you get the **Unsaved Coverage** prompt with
**Save to Job**, **Discard** or **Cancel**.

## Record what went on

| Button (Field Tools) | What it stores |
|---|---|
| **Job Product** | Product name, target rate and unit (e.g. urea, 70 kg/ha). |
| **Applied Total** | The measured total actually used — loader-scale weight, tank total, drill counter. The record then shows *actual* rate = amount ÷ covered area. Leave it blank and a running rate controller's flowmeter total fills it in. |
| **Tank Mix** | The sprayer mix, saved with the job so a refill reopens it. |
| **Delete Applied Area** | Wipes this job's paint only; other jobs are untouched. |

Set the product before closing the field — the record is tagged with
whatever the job holds at export time.

## Two coverages in one job (cross-drill)

The Route Planner's **Cross** pattern (Pattern tab) drills the field twice at
right angles. Both passes belong to the same job but sit on two separate
coverage *channels*: the second direction neither reads nor trips the first
direction's paint, so auto sections fire properly on the cross pass instead
of seeing "already covered". On screen the two draw as one map, the second
a shade darker. **Row spacing cm** adds a second set of headland laps
shifted half a row; **Weave (V-turns)** alternates the two directions.

## Where it all lives

Plain files under your data folder, one folder per field:

| File | What it is |
|---|---|
| `<field>/` boundary, tracks, `routeplan.json`, `elevation.json` | The permanent paddock |
| `<field>/jobs/<job name>/job.json` | Work type, notes, product, rate, applied total, tank mix |
| `…/coverage_detect.bin` (+ `coverage_detect2.bin`) | The paint (second file = cross-drill channel) |
| `…/coverage.geojson` | The exported application record |
| `…/elevation.geojson` | Terrain grid copied alongside |

## Export and the farm-server viewer

`coverage.geojson` holds the worked area as one shape, tagged with field,
job, product, rate, hectares, tool width, applied total and times. It is
written when you **Close** the field (or on next open if the tablet died
first). **Export Coverage** in **Field Tools** writes it right now.

From there nothing is needed from you:

1. Syncthing copies the Fields folder from the tablet to the farm server.
2. An indexer on the server picks up every new or changed
   `coverage.geojson` and field boundary.
3. The map viewer (the map-records icon on the shed screen, or port 8082 on
   the server from any device that can reach it) shows all paddock boundaries with
   each application shaded by product. Filter by field, product or date; the
   summary gives hectares and applied totals per product, and each
   application's popup shows actual-versus-target rate, red when more than
   ten percent out.

## Things to know

- Closing the field is what finishes the record. Use **Field Operations** →
  **Close** at the end of the day rather than just shutting the tablet.
- Re-running **Export Coverage** or entering an **Applied Total** later
  overwrites the record; the viewer updates on the next sync.
- **Terrain**: while you drive with RTK, the app builds an elevation grid
  and saves it with the *field*. Turn it on under **Screen & Alerts** →
  **Map Background** → **Terrain** to shade the map by height; the same grid
  goes out as `elevation.geojson` with each export so the viewer can show it.
  It is not the "contour" guidance mode — that is the follow-the-previous-
  pass track tool. See the Terrain guide.
- **Delete Job** removes that job's coverage and record from the tablet.
  Copies already synced to the server are kept by its versioning for a year.
