# What AgOpenWeb does beyond AgOpenGPS

AgOpenWeb runs the same modules, the same PGNs and the same fields as stock
AgOpenGPS, but it is served as a web page from the box in the tractor and has
grown a layer of features of its own: a whole-field route planner, jobs and
application records, built-in rate control, a multi-device operator seat, and a
pile of smaller guidance and field-building tools.

This page is the index. One line per feature, grouped by what you are doing, with
a link to the plain-language guide section that explains it. The guides
themselves live in [guides/](guides/README.md).

## Guidance & turns

| Feature | What it does | Guide |
|---|---|---|
| Turn countdown readout | Plots the next headland turn well ahead, draws it green on the map and counts the metres down to where it fires | [Turning at the headland](guides/turns.md#automatic-turns) |
| Tap to swap turn direction | Tapping the arrow/distance readout flips the pending turn left-right and redraws the path | [Turning at the headland](guides/turns.md#direction-and-turn-type) |
| U / K turn toggle on screen | The glyph beside the readout switches between a round U (Sagitta) turn and a three-point K turn for the next turn | [Turning at the headland](guides/turns.md#direction-and-turn-type) |
| K turn with reverse-leg handover | Autosteer drives the forward arc; the moment you reverse, guidance hands over to the next pass while you are still backing | [Turning at the headland](guides/turns.md#k-turns--for-mounted-implements) |
| Trailed-implement K-turn lockout | **K-turns (reverse leg): Never (trailed)** on the Hitch tab keeps every live and planned turn forward-only so a drawbar implement can't jackknife | [Turning at the headland](guides/turns.md#k-turns--for-mounted-implements), [Route Planner advanced](guides/route-planner-advanced.md#k-turns-and-trailed-implements) |
| Snake skip-row pattern | **U-Turn skip rows on/off** plans the pass order from where you are, works out one way and comes back for the skipped passes so nothing is left | [Turning at the headland](guides/turns.md#skipping-rows) |
| Hard-fence implement clearance | On a boundary marked **Hard** the app traces the whole implement's swing (length, physical width, margin), pulls the turn inside, or refuses a turn that won't fit | [Turning at the headland](guides/turns.md#hard-fences-and-long-implements) |
| Headland width rule of thumb | A worked example of how much headland a U or K turn really needs for your radius, margin and implement | [Turning at the headland](guides/turns.md#how-wide-a-headland) |
| Offset implement model | One **Tool offset** setting; the tractor steers a line beside the pass so the implement lands on it, and coverage paints on the pass, not the wheel marks | [Offset implements](guides/tool-offset.md#what-youll-see) |
| Asymmetric U-turns for offset tools | Turns alternate tight and wide so an offset implement still steps exactly one working width | [Offset implements](guides/tool-offset.md#what-youll-see) |
| Offset-aware plans with replan warning | Route plans are shifted for the offset they were made with; opening one with a different implement shows **REPLAN before drilling** | [Offset implements](guides/tool-offset.md#with-the-route-planner) |
| Offset follows reversing | Reversing flips which side the implement sits on, so a K turn's reverse leg lands on the right line | [Offset implements](guides/tool-offset.md#things-to-know) |

## Route planning

| Feature | What it does | Guide |
|---|---|---|
| Whole-field Route Planner | Draws a complete route (headland laps, every pass, every turn) for a paddock before you start, then lets you check, simulate and steer it | [Route Planner basics](guides/route-planner-basics.md) |
| Five patterns | **Auto**, **Skip**, **Cross**, **Spiral** and **Block** pass orders, with skip/block steppers | [Route Planner basics](guides/route-planner-basics.md#pattern-tab--what-shape-to-drive) |
| Automatic pass angle | With **Angle °** at 0 the planner times the longest fence, the square angle and a sweep between using your speed model, and builds the fastest | [Route Planner advanced](guides/route-planner-advanced.md#choosing-a-pattern) |
| Live pass-direction preview | While the panel is open a dashed line on the map shows the pass direction as you change it | [Route Planner basics](guides/route-planner-basics.md#pattern-tab--what-shape-to-drive) |
| Split lines and blocks | Two taps cut an L-shaped or wedge paddock into blocks; each block gets its own best heading, one set of laps round the whole, links between | [Route Planner advanced](guides/route-planner-advanced.md#split-lines-and-blocks) |
| Plan along a track / Plot A-B | Plan along a saved AB line or curve (passes follow the curve's shape), or tap two points to create the AB and plan in one go | [Route Planner advanced](guides/route-planner-advanced.md#planning-along-a-track) |
| Cross-drill with row-spaced second headland | **Cross** drills the field twice at right angles; **Row spacing cm** shifts a second set of headland laps half a row (**Route Headland 2**) | [Route Planner advanced](guides/route-planner-advanced.md#cross-drill) |
| Weave (V-turns) | Works both cross-drill directions together as a W with gentle V-turns in the side headland instead of 180° keyholes | [Route Planner advanced](guides/route-planner-advanced.md#cross-drill) |
| Headland styles | **Classic laps**, **Spiral in**, **Spiral out** or **None**, with lap count sized automatically from your U-turn radius | [Headland styles](guides/headland-styles.md#the-controls) |
| Headland first / last | Laps before the middle (spraying, seeding) or after it (mowing) | [Headland styles](guides/headland-styles.md#the-controls) |
| Back-cut finish lap | One extra fence-tight lap driven the opposite way so a one-sided tool dresses the fence strip | [Headland styles](guides/headland-styles.md#the-controls) |
| Per-device headland choices | Style, order and back-cut are remembered on the tablet that set them, so the mower's and the sprayer's screens don't fight | [Headland styles](guides/headland-styles.md#notes) |
| Plan saved as steerable tracks | Planning writes **Route Main**, **Route Headland** and one track per block into the Tracks manager; **Steer: Main / Headland** picks one and normal autosteer follows it, turns included | [Route Planner basics](guides/route-planner-basics.md#drive-tab--following-the-plan) |
| Simulated drive of the plan | **▶ Drive (sim)** runs a simulated tractor round the whole route with sections working so you can watch order and coverage first | [Route Planner basics](guides/route-planner-basics.md#drive-tab--following-the-plan) |
| Live time remaining | An ETA learned from your actual work and turn speeds as you drive | [Route Planner basics](guides/route-planner-basics.md#drive-tab--following-the-plan) |
| Speed model and edge offset | **Spd W/T · s/turn · edge m** feed the route comparison and ETA; edge pulls the first pass in where the fence sits on the mapped line | [Route Planner basics](guides/route-planner-basics.md#check-tab--is-the-plan-honest) |
| Show missed spots | Tints unworked ground red under your coverage so gaps stand out | [Route Planner basics](guides/route-planner-basics.md#check-tab--is-the-plan-honest) |
| Ponds routed around | Inner boundaries with **Drive Thru** off are holes: passes stop at the edge and the route goes round | [Route Planner advanced](guides/route-planner-advanced.md#obstacles-and-ponds) |
| Obstacles: pole, hole, hose | Place sized point obstacles on the map; anything narrower than the working width is swerved using the real **Physical width** rather than routed around | [Route Planner advanced](guides/route-planner-advanced.md#obstacles-and-ponds) |
| Pause, come back, resume | The plan is saved with the field and restored on reopen (**Saved route plan restored**); pick the track again and carry on | [Route Planner advanced](guides/route-planner-advanced.md#pause-come-back-resume) |
| Already-worked laps skipped | Any outer lap more than 80 % covered (e.g. the boundary lap you recorded with the tool working) is dropped at plan time | [Route Planner advanced](guides/route-planner-advanced.md#pause-come-back-resume) |
| Slope-corrected spacing | With Terrain data the Check tab tightens pass spacing on side slopes so ground coverage stays a full tool width | [Route Planner advanced](guides/route-planner-advanced.md#slope-corrected-spacing) |
| Refill points on the route | Amber dots mark where each tank runs dry along the planned path, from your carrier rate and tank size | [Route Planner advanced](guides/route-planner-advanced.md#refill-points), [Tank mix](guides/tank-mix.md#refill-points-on-the-map) |
| Plan data travels with the field | Splits, block angles, obstacles and the plan live in the field folder, so they follow the field to another tablet | [Route Planner advanced](guides/route-planner-advanced.md#things-to-know) |

## Field building

| Feature | What it does | Guide |
|---|---|---|
| Draw boundary on satellite imagery | Tap the corners on the aerial instead of driving the fence (outer boundary only) | [Building a field](guides/field-builder.md#record-the-boundary-by-driving-it) |
| Drive Thru / Hard flags on inner boundaries | Two tap-to-toggle columns decide whether a ring is keep-out or just a marker, and whether turns must keep the implement body clear of it | [Building a field](guides/field-builder.md#ponds-vs-wet-patches-inner-boundaries) |
| Headland edge validity dots | Building a headland edge by edge, a green dot shows the edge reaches the boundary at both ends; red means it has no effect | [Building a field](guides/field-builder.md#headland) |
| Quick AB Line dialog | One dialog for **A+**, **Drive AB**, **Curve**, **Longest Edge**, **Bnd. Curve**, **All Edges**, and straight or curved lines drawn on the map | [Building a field](guides/field-builder.md#tracks) |
| Boundary curve with A++ / B−− trim | **Bnd. Curve** follows the fence between two taps, half a tool width in; **A++ / A−− / B−− / B++** walk each end 5 m along the fence | [Building a field](guides/field-builder.md#tracks) |
| Fine nudge and half-width jumps | Quarter-step fine nudge, **<< / >>** for half a tool width, **0** to reset, plus snap to left/right track | [Building a field](guides/field-builder.md#tracks) |
| Track point editor | **Field Builder → Tracks → ✎ Edit** lets you drag a track's points on the map and save | [Building a field](guides/field-builder.md#tracks) |
| Flags placed on the map | Drop a flag at the tractor or tap the spot on the map; rename, recolour, locate and delete from the list | [Building a field](guides/field-builder.md#flags) |

## Coverage, fields & jobs

| Feature | What it does | Guide |
|---|---|---|
| Fields and Jobs | A field is the permanent paddock; each job (drilling, urea, mowing) keeps its own coverage, product, rate and total side by side | [Fields, jobs and coverage](guides/fields-jobs-coverage.md) |
| Pick on Map | Tap the paddock on the aerial to open it instead of picking from the list | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#start-a-job) |
| Job work type, notes and resume | Type or pick the work type, add notes (**Use Last** copies the previous job's), then **Resume Job** or **Resume Last Job** to carry on | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#start-a-job) |
| Open Field Only + unsaved-coverage prompt | Open a paddock with no job; anything painted triggers **Save to Job / Discard / Cancel** on close | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#start-a-job) |
| Job Product | Product name, target rate and unit stored on the job record | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#record-what-went-on) |
| Applied Total and actual rate | Enter the measured total used (scale, tank, counter) and the record shows actual rate; a running rate controller's flowmeter fills it in for you | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#record-what-went-on) |
| Delete Applied Area per job | Wipes this job's paint only; other jobs in the paddock are untouched | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#record-what-went-on) |
| Coverage channels | Cross-drill's second direction paints a separate channel, so auto sections fire properly over the first pass instead of seeing "already covered" | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#two-coverages-in-one-job-cross-drill) |
| coverage.geojson export | On close (or **Export Coverage** now) the worked area is written as one shape tagged with field, job, product, rate, hectares, width, total and times | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#export-and-the-farm-server-viewer) |
| Farm-server application viewer | Syncthing carries the Fields folder to the server, an indexer picks up records, and a web map shows every application by product with actual-vs-target flags | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#export-and-the-farm-server-viewer) |
| Delete Job with server versioning | Removes a job's coverage and record from the tablet; synced copies are kept by the server's versioning for a year | [Fields, jobs and coverage](guides/fields-jobs-coverage.md#things-to-know) |
| Terrain elevation recording | GPS altitude is sampled automatically while you drive, averaged into 5 m squares and saved with the field, improving every pass | [Terrain](guides/terrain-map.md) |
| Terrain map background | **Screen & Alerts → Map Background → Terrain** shades the field blue-to-red by height with a low/high legend, under your coverage and routes | [Terrain](guides/terrain-map.md#seeing-it) |
| elevation.geojson export | The Terrain grid goes out alongside `coverage.geojson` on every export so the off-board viewer can use it | [Terrain](guides/terrain-map.md#for-the-record-keepers) |
| Tank mix calculator | Area × carrier rate, number of fills, and per-tank chemical amounts with the part-filled last tank worked out separately | [Tank mix](guides/tank-mix.md#using-it) |
| "Left" area button | Uses field minus painted coverage as the area, for a mid-job refill | [Tank mix](guides/tank-mix.md#using-it) |
| Remembered chemical list | Products you have used come back as one-tap buttons with their usual rate; bases **L/ha, mL/ha, kg/ha, g/ha, mL/100L, L/100L** | [Tank mix](guides/tank-mix.md#using-it) |
| Water buffer handling | Extra litres scale per-100 L products but not per-hectare ones, and the output says so | [Tank mix](guides/tank-mix.md#using-it) |
| Mix saved with the job | The tank mix is stored on the job so a refill reopens it | [Tank mix](guides/tank-mix.md#what-this-is) |

## Rate control

| Feature | What it does | Guide |
|---|---|---|
| Built-in rate control | Meters spray, fert or seed through an AOG_RC module (RC15 / Teensy / Nano) with no separate RateController app; switched on per tool | [Setting up rate control](guides/rate-control-setup.md#1-turn-it-on-for-the-implement) |
| Shared product catalogue, per-tool channels | Products (name, units, usual rate) are shared by every implement; channels **A–E** with module, flow cal and tank size belong to this tool | [Setting up rate control](guides/rate-control-setup.md#2-products-and-channels) |
| Module commissioning from the cab | Board defaults, module config, pins, relay functions and tuning are kept against the tool and sent in one go; **Assign this ID** refuses while more than one board answers | [Setting up rate control](guides/rate-control-setup.md#3-module-setup-) |
| Valve tuning from the cab | Min/max power, Kp, Ki, deadband, brake point, slow adjust, slew rate and flow samples, with a tuning order that works | [Setting up rate control](guides/rate-control-setup.md#3-module-setup-) |
| Catch-test calibration | Run the valve at a manual PWM, type what you caught, and **Flow cal** rescales; the test quantity comes back out of the job total | [Setting up rate control](guides/rate-control-setup.md#4-calibrate-catch-test) |
| Section-to-switch groups | Allocate each section to a switch **S1–S8**; the same switch on several sections makes one group | [Setting up rate control](guides/rate-control-setup.md#5-switches-and-primed-start) |
| Primed start | Runs the metering before you move at a simulated speed, with hold delay and resume-if-moving | [Setting up rate control](guides/rate-control-setup.md#5-switches-and-primed-start) |
| Work switch gates master | Product only flows while the work switch says the implement is down; live state shown as **Work switch now** | [The work switch](guides/work-switch.md#what-the-work-switch-gates) |
| Floating switchbox | A draggable strip of big buttons: **MST**, **PRM**, **AUTO RATE**, **AUTO SECT**, section groups and a **+ / −** rate rocker | [Rate switchbox](guides/rate-switchbox.md#the-floating-switchbox) |
| Rate readout | A draggable current/target box per product; amber when more than 10 % off while flowing, **BIN EMPTY** when the bin is out; on-map readout option too | [Rate switchbox](guides/rate-switchbox.md#the-rate-readout) |
| Physical AOG_RC switchbox | Plug it in and its buttons mirror the on-screen ones; auto-marked *expected* the first time it talks | [Rate switchbox](guides/rate-switchbox.md#the-physical-switchbox) |
| Rate rows in Network IO | Every rate module and the switchbox show under **Network IO → Rate control**, plus a **Rate** row in the status-bar Modules popup | [Rate switchbox](guides/rate-switchbox.md#the-physical-switchbox) |
| Drop-off alarm | A red banner and beep when a needed rate module or the switchbox stops answering (or is dead at power-on after a 30 s grace); tap to silence, screen and sound switchable | [Rate switchbox](guides/rate-switchbox.md#drop-off-alarm) |

## Modules & hardware

| Feature | What it does | Guide |
|---|---|---|
| Expected-module dots | Tick a module you always carry; green = heard, red = expected but silent, grey = not expected, with a quick **Modules** popup in the status bar | [Connecting your modules](guides/module-connections.md#the-network-io-panel-top-to-bottom) |
| USB serial bridge | **Serial (USB)** mode runs a module over a USB lead; the choice is saved and the module behaves like a network one | [Connecting your modules](guides/module-connections.md#usb-serial-fallback) |
| Settings re-sent on module hello | Every time a module comes online the app pushes its settings within a second, so a blank or stale board always runs what the screen shows | [Connecting your modules](guides/module-connections.md#module-settings-are-pushed-for-you) |
| Momentary work-switch mode | Besides a maintained switch, a push button can toggle work on/off per press, with active-low/high and Auto/Manual section choices | [The work switch](guides/work-switch.md#the-settings) |
| Work-switch bench test | A five-minute nothing-moving check of wiring, polarity and gating | [The work switch](guides/work-switch.md#bench-test-5-minutes-nothing-moving) |

## Using it from other devices

| Feature | What it does | Guide |
|---|---|---|
| Any browser, any device | The app is a web page on port **5174**; a phone, tablet or laptop on the same network gets the same screen as the cab | [Other devices and who is driving](guides/remote-and-seats.md#open-the-app-on-another-device) |
| Fullscreen and home-screen launch | The **⛶** button, **Start Fullscreen** setting or *Add to home screen* hide the browser bars | [Other devices and who is driving](guides/remote-and-seats.md#open-the-app-on-another-device) |
| Operator / Observer seat | One device drives; the rest watch. Tap the role badge to take the seat in two taps | [Other devices and who is driving](guides/remote-and-seats.md#take-the-seat) |
| Server-enforced operator actions | Steering, sections, turns, track changes and module subnet changes are dropped by the box unless they come from the Operator | [Other devices and who is driving](guides/remote-and-seats.md#what-an-observer-can-and-cannot-do) |
| Lost-operator safety stop | If the Operator's device goes quiet for about five seconds, autosteer disengages, sections turn off and any driven path stops; the seat is reclaimed after a simple refresh | [Other devices and who is driving](guides/remote-and-seats.md#things-to-know) |
| Full layout on a phone | No cut-down mobile page; only the map pinch-zooms, panels open over the map, floating panels drag | [Other devices and who is driving](guides/remote-and-seats.md#using-the-full-screen-on-a-phone) |
| Shared vs per-device settings | Settings changed on a phone are the box's real settings; only panel state and headland style stay per device | [Other devices and who is driving](guides/remote-and-seats.md#things-to-know) |

## Data

| Feature | What it does | Guide |
|---|---|---|
| One data folder, relocatable | Everything lives in **Documents → AgOpenWeb**, outside the program folder; `AGOPENWEB_DATA` moves it off cloud-synced drives | [Your data and profiles](guides/data-and-profiles.md#where-the-folder-is) |
| Plain-file formats | `field.geojson`, `coverage.geojson`, `routeplan.json`, JSON vehicle/tool/NTRIP/rate files you can read and copy | [Your data and profiles](guides/data-and-profiles.md#what-is-inside) |
| Last-known-good profiles | Profiles are written with a `.bak`; a file damaged in a power cut falls back to it and tells you | [Your data and profiles](guides/data-and-profiles.md#what-is-inside) |
| Separate vehicle and tool profiles | Tractor and implement are paired, not merged: offset, hitch, K-turn rule and rate control follow the tool; steering and turn radius follow the vehicle | [Your data and profiles](guides/data-and-profiles.md#vehicle-profile-vs-tool-profile) |
| One-way import from AgOpenGPS | Copy old field folders or vehicle `.XML` files in and they are read and re-saved in the new formats | [Your data and profiles](guides/data-and-profiles.md#bringing-things-across-from-agopengps) |
| KML import | Drop `.kml` / `.kmz` in **Import** and use **Fields and Jobs → From KML** | [Your data and profiles](guides/data-and-profiles.md#bringing-things-across-from-agopengps) |
| Syncthing-friendly layout | Share **Fields** only, receive-only with versioning at home; keep vehicle and tool profiles per machine | [Your data and profiles](guides/data-and-profiles.md#backing-up) |

## Not connected yet

Two stock AgOpenGPS features are present on screen but not wired through:
**Contour** steering and **Tram lines** (a drawing aid only). See
[Building a field](guides/field-builder.md#contour--not-working-yet).
