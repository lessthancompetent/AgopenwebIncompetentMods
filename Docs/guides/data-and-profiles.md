# Your data and profiles

**What this is.** Everything AgOpenWeb remembers — paddocks, coverage, route
plans, tractor and implement setups, NTRIP logins, rate-control calibrations —
lives in one folder on the device running the app. This guide says where that
folder is, what is inside it, which settings belong to the *vehicle* and which
to the *tool*, how to bring things across from AgOpenGPS, and how to keep it
all safe.

**You will need**

- A file browser on the device (Explorer, Finder, or a file app on the tablet)
- For backups: a USB stick, a second computer, or a sync tool such as Syncthing

## Where the folder is

On every platform the data folder is **Documents → AgOpenWeb**. The app creates
it on first start and never keeps data inside its own program folder, so an
update cannot wipe your fields.

To put it somewhere else — a bigger drive, a server, or simply *out of a
cloud-synced Documents folder* — set the environment variable `AGOPENWEB_DATA`
to the folder that should **contain** `AgOpenWeb`, then restart the app. The
Linux service install does this for you.

**Keep it out of OneDrive, iCloud Drive, Google Drive and Dropbox.** They
rewrite files behind the app's back while it is still writing them. Add
Syncthing on top and the two fight over the same file — a freshly saved route
plan has been eaten exactly this way. If your Documents folder is cloud-backed,
move the data with `AGOPENWEB_DATA`.

## What is inside

| Folder or file | Holds |
|---|---|
| **Fields** | One folder per paddock: `field.geojson` (boundary, headland, tracks, flags), `coverage.geojson` (the painted record of each job), `routeplan.json`, tram lines, Terrain elevation data, and any legacy AgOpenGPS text files you copied in |
| **Vehicles** | One `.json` per tractor, plus a `.AutoSteer.json` beside it with that tractor's steer settings |
| **Tools** | One `.json` per implement |
| **NtripProfiles** | One `.json` per RTK caster login |
| **RateController** | `products-catalog.json` (products, shared by every implement) and per-implement `channels-<tool>.json`, `modules-<tool>.json`, `switchbox-<tool>.json` |
| **Import** | Drop `.kml` / `.kmz` files here for **Fields and Jobs → From KML** |
| `appsettings.json` | Units, display choices, which vehicle and tool were last loaded |

Profiles are written with a `.bak` last-known-good copy. If a file is damaged
(power cut mid-save) the app falls back to the `.bak` and tells you.

## Vehicle profile vs tool profile

The tractor and the implement are separate profiles, paired in the **Vehicle &
Tool** panel (left bar, the button titled **Vehicle & Tool Configuration**).

| Lives with the **vehicle** | Lives with the **tool** |
|---|---|
| Antenna height, pivot, wheelbase, track width, steer angle | Working width, sections and their widths |
| Steering settings (the **AutoSteer Configuration** panel) | **Tool offset (m)** and **Direction** (**Tool → Offset**) |
| Guidance look-ahead, Pure Pursuit / Stanley gains | **Physical width (m)** and **Implement length (m)** (**Tool → Hitch**) |
| U-turn radius, style, skip width; tram settings | **K-turns (reverse leg)**: **Allowed** or **Never (trailed)** (**Tool → Hitch**) |
| | **Use rate control** and everything under **Machine → Rate Control**, plus the RateController files above |

Rule of thumb: if it changes when you hook on a different implement, it is on
the tool; if it changes when you climb into a different tractor, it is on the
vehicle.

To manage them:

1. Open **Vehicle & Tool**. The left column is **Vehicle**, the right is **Tool**.
2. Type a name, then use **New**, **Rename**, **Delete** or **Reset** under each list.
3. **Configure Vehicle…** / **Configure Tool…** open the detail panels. Tap
   **Save Profile** there — edits are not on disk until you do.
4. Select one of each and tap **Load Selected** to make the pair active.

Rate-control calibrations follow the *tool name*: rename a tool and its
`channels-`, `modules-` and `switchbox-` files need the matching name too.

## Bringing things across from AgOpenGPS

Import is one-way: AgOpenWeb reads the old formats and saves in its own. It
does not write files AgOpenGPS can read back.

- **Fields** — copy the whole paddock folder (`Field.txt`, `Boundary.txt` and
  friends) into **Fields**. It appears in **Fields and Jobs**; the first save
  writes `field.geojson` alongside the old files.
- **Vehicle profiles** — copy the `.XML` (and any `.tool.xml` / `.env.xml`
  siblings) into **Vehicles**. Load it once and save to get the split
  vehicle + tool JSON pair.
- **Boundaries from Google Earth** — put the `.kml` in **Import** and use
  **Fields and Jobs → From KML**.

## Backing up

The whole job is one folder. Copy **Documents → AgOpenWeb** to a USB stick or
another computer now and then, and always before an app update or tablet
rebuild.

If you run Syncthing between the tractor and the house, share the **Fields**
folder only — that is what changes every day and what you want on more than
one screen. Do **not** sync **Vehicles**, **Tools** or steer settings: they
belong to one machine, and two tractors sharing them overwrite each other's
calibration. On the receiving computer set the folder to *receive only* with
file versioning on, so a mistake in the cab can be undone at home.

## Things to know

- **Reset All Settings** (File / Application Menu) resets `appsettings.json`;
  it does not delete fields or profiles.
- Profiles are per device. A new tablet starts empty until you copy
  **Vehicles**, **Tools** and **NtripProfiles** across — or set them up again.
- `AGOPENWEB_DATA` is set on each device separately; it is not carried inside
  the data folder.
