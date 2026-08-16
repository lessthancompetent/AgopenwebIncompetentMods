# Rate control — handoff

**Written 2026-08-16, updated the same evening.** Two problems were reported:
the *Use rate control* toggle does nothing, and a bench module would not
connect. **Both are now resolved and deployed.** The bench module is connected
end to end — module → house router → tablet's AgOpenWeb, PastureBoss channel
green, module id 1 at `192.168.5.51` — the first hardware-confirmed module link
this port has had. Section 2 records the diagnosis and the two code defects it
uncovered.

Repo: `C:\Users\OEM\.claude\sessions\Agvalonia Rework\AgOpenWeb`, branch
`feature/route-planning`. Relevant commits, newest last:

| Commit | What |
|---|---|
| `528929a3` | Module setup: valve wiring, board defaults, relay pins, colour |
| `8ca29bbc` | Relay functions (16 outputs per module) |
| `357c978b` | **Rate control toggle fix** |
| `3a4bbf36` | **Module discovery: ghost-id expiry + ID-assign by unicast** |

---

## 1. The toggle — FIXED (`357c978b`, deployed to the tablet)

### What was wrong

Three separate breaks, all on the **read-back** path. The write path was always
fine — `config.set|tool.useRateControl:1` reached the server and was stored.

1. `Shared/AgOpenWeb.RemoteServer/WireCodec.cs` — the tool block stopped at
   `TotalWidth`. The DTO's last two fields (`PhysicalWidth`, `UseRateControl`)
   were declared in `Contracts.cs:332` and populated by `SceneProjector.cs:687`
   but never encoded.
2. `Shared/AgOpenWeb.RemoteServer/wwwroot/transport.js` — the positional decode
   stopped at the same point. Encoder and decoder *agreed with each other*; they
   both just stopped two fields early.
3. `SceneProjector.ConfigFingerprint()` — hashed neither field, so changing them
   did not even mark the config frame for re-send.

Because the value never reached the browser, `cfgGet('tool.useRateControl')` was
permanently `undefined`, so the button rendered `Off` forever. The click handler
derives what it sends from the rendered state:

```js
cfgSend(b.dataset.key, b.classList.contains('active') ? '0' : '1')   // app.js:3109
```

`active` was never set, so **every press sent `:1`**. Rate control could be
switched on and then never off from the web UI. There is no optimistic local
update, which is why the button showed no flicker at all.

### ⚠️ The rule to remember

`WireCodec.cs` and `wwwroot/transport.js` **must always change together, in the
same commit, in the same field order.** The CONFIG frame is a bare positional
byte stream — no length prefixes, no per-section framing, no version
negotiation. Adding a field on one side only shifts everything after it and
turns `uturn`, `tram`, `machine`, `display` and the whole 9-tab `autosteer`
surface into garbage.

### Verified

- `config.tool` now decodes `{totalWidth: 2.9, physicalWidth: 3.2, useRateControl: true}`.
- Everything after the tool block still decodes sanely (uturn, tram, machine,
  display, autosteer all checked) — no desync.
- After a forced repaint the button reads `On`, and a press then sends
  `config.set|tool.useRateControl:0`, and the value goes to `false`. **The
  toggle can now be turned off.**

### Not verified — please check on the tablet

I could not confirm the button **repaints by itself** after a press. The repaint
runs from `renderSettings()`, which lives in the `requestAnimationFrame` loop
(`app.js:6409`), and the headless preview tab I test in **never composites**, so
`rAF` fires zero times there and `configDirty` stays stuck `true`. On a real
screen the loop runs at 60 Hz.

This is the same repaint path every other config control already uses (vehicle
config, autosteer, display), so if it were broken nothing in settings would ever
update — but I did not see it work with my own eyes. **First thing to check:
open Tool config → Machine → Rate Control on the tablet and press the toggle. It
should flip On/Off each press.** If it does not, the bug is in
`renderSettings()`/`configDirty` (`app.js:4125-4133`), not in the wire.

### Same defect, still unfixed elsewhere

The audit found more keys accepted by `config.set` but never encoded, so their
controls are also stuck at a default. Each needs the same three-file lockstep
treatment:

| Key | Symptom |
|---|---|
| `tool.physicalWidth` | **fixed in `357c978b`** (was: Hitch tab input always blank) |
| `uturn.routeTurnSpeed`, `routeWorkSpeed`, `routeTurnPenalty`, `routeHeadlandSpeed` | route planner speed inputs render blank; client time estimates silently use hardcoded defaults instead of the saved profile |
| `display.obstacleAlarmEnabled`, `display.obstacleAlarmDistanceM` | proximity alarm button always reads off, distance always reads 10 after reload |
| `tool.width` | fully one-way — no DTO field, no encoder, no decoder, no UI control anywhere. Dead key; either wire it or delete the case. |

> The user previously reported "a rate control toggle that doesn't do anything in
> tool config → hitch". That was almost certainly `tool.physicalWidth` on the
> Hitch tab (same tab, adjacent control), now fixed.

---

## 2. The bench module — RESOLVED (connected end to end)

Symptom was *no modules heard*. Root cause: **subnet mismatch on a shared
wire**, plus two real code defects the recovery exposed (both fixed in
`3a4bbf36`).

### What it was

tcpdump on the tablet's wired NIC showed both sides transmitting on the same
house L2, addressing past each other:

```
192.168.1.50.28888  > 192.168.1.255.29999   # module, factory subnet
192.168.5.135.29999 > 192.168.5.255.28888   # tablet, house subnet
```

The module's cable goes to the house router; its EEPROM had been wiped back to
factory by a fresh firmware flash (v2026.07.17, built from the user's modified
sketch in `Downloads\AOG_RC-main (2)`), so it sat on `192.168.1.50`
broadcasting to `192.168.1.255` — which every 192.168.5.x host drops before any
socket sees it. Each side's frames were on the wire the whole time; nobody's IP
stack would deliver them.

### How it was recovered (repeatable recipe)

1. **Serial console first** — COM12 (CH340) at **38400**, reset via RTS pulse
   (not DTR alone). The boot log names the firmware, module id, IP, and pin
   config; everything else is guesswork without it. Note the port was held
   first by RateController.exe and then by the Arduino IDE's serial monitor —
   both had to be closed.
2. **Temporary foot in the module's subnet** on any Linux box on the same L2:
   `sudo ip addr add 192.168.1.201/24 dev enp0s25` on the tablet → module 0
   heard within seconds. (Windows needs admin for the same trick, which the
   house PC shell doesn't have.)
3. **Push the wired subnet** (Module Setup → Network → Set wired subnet, PGN
   32503) → module reboots onto `192.168.5.50`, temp address removed, heard
   natively from then on.
4. **Restore its intended id 1** (it had been wiped to 0): stage ESP32 board
   defaults into the module-1 record first so the assign frame doesn't carry a
   zeroed config, then assign.

### The two code defects this exposed (fixed, `3a4bbf36`)

- **ID-assign never delivered on Ethernet.** The W5500 on these boards drops
  directed broadcasts (`x.x.x.255`), which is exactly why `SendToModule` also
  unicasts to the learned address — but *assign* targets an id that has no
  learned address yet, so the frame went out broadcast-only and never arrived.
  Bench-proven: the board only took its id from a direct unicast. Assign now
  also unicasts to **every** learned module address (same semantics — every
  listening module adopts the id).
- **Heard list never forgot.** `_moduleAddresses` had no expiry, so after the
  re-brand the list showed `[0, 1]` with one physical board. The ghost both
  misleads the exact "is it heard?" diagnosis and trips the assign guard's
  "more than one module answering" refusal. Entries now expire from the heard
  list after 10 s (the address is still used for unicast while stale, which is
  harmless).

### Firmware facts worth keeping (from the flashed source, `AOG_RC-main (2)`)

- **32700 is gated**: `GoodCRC && (AssignID || data[2] == MDL.ID)` — byte 4
  bit 64 is the assign flag; without it the frame is a filtered per-module
  update. On receipt: `SaveData(); ESP.restart();`.
- This build **validates pins** (`PinAllowed`/`OutputPinAllowed`, NC=255
  allowed) before accepting a config.
- **Ethernet is exclusive**: W5500 link up ⇒ Wi-Fi transmit skipped entirely.
- The module broadcasts to **its own saved subnet's** `.255`, not yours.
- No station-mode Wi-Fi attempt appears in the boot log unless credentials are
  saved *and* station mode was ticked.

### Still true / traps

- `p.Enabled` gates ALL host transmission (32500/32501); a fresh tool profile
  sends nothing. Disabled channels show no dot at all in the Rate panel —
  indistinguishable from channels that don't exist. (Improvement still open:
  grey dot + "channel disabled".)
- The WinForms RateController and the Arduino serial monitor both compete for
  the module: 29999 (SO_REUSEADDR means unicasts go to ONE of the two apps)
  and COM12 respectively. Close them before testing AgOpenWeb.
- hz/upm stay 0 until the flow sensor actually pulses — bench "connected, hz 0"
  is the correct idle state.

## 3. State of play

**Shipped and on the tablet:** per-tool rate channels + global product
catalogue; module setup (config / pins / relay pins / valve tuning / subnet /
commissioning) over PGNs 32502 / 32507 / 32700 / 32503; per-board factory
defaults (RC15 ESP32, RC11-2 Teensy, RC12-3 Nano); relay **functions** with the
power/inverted/flow-master words of PGN 32501; on-map rate readout; virtual
switchbox master + rate bump.

**Not built** (all present in the native app):

- Master-switch mode (Control All / Master Relay Only / Master Override)
- Switch type (Momentary / Maintained), work-switch gating, Primed Start
- Product **Mode** (Controlled UPM / Constant UPM / Document Applied / Document Target)
- Off-rate alarm; the virtual switchbox has no switches of its own, so relay
  type *Switch* currently reads the section bits as a stand-in

**Never tested against hardware:** the module-setup **push** path (32502 / 32507
/ 32700 have been built and sent but never confirmed received by a module), the
rate readout with real flow, and the Nano bench rig.

### Test suite caveat

`Tests/AgOpenWeb.Services.Tests` fails roughly **1 run in 8**, always in
`VirtualModuleTests`, on a rotating cast of tests. **This is pre-existing** —
measured at 1 failure in 12 runs on a clean tree. Re-run before blaming a
change. Two theories were investigated and **both disproved**: it is not a lost
datagram (the module binds its socket in the constructor, before `Start()`), and
it is not a too-short timeout (polling with resends and a 20 s timeout still
failed). `GetEphemeralPort()` releasing its probe socket before the caller binds
is a real hazard but de-duplicating ports did not fix it either. That
investigation was reverted rather than committed half-done.

### Reference

The native app's full user manual is the best spec available:
`C:\Users\OEM\Downloads\AOG_RC-main\AOG_RC-main\RateAppSource\RateController\Help\*.pdf`
(17 pages). `pdftoppm` is not installed — extract with a zlib+regex script and
truncate each page at `glyf`/`fpgm`/`Adobe UCS` to drop the embedded font tables.

### Deploying to the tablet

```bash
dotnet publish Platforms/AgOpenWeb.Desktop/AgOpenWeb.Desktop.csproj -c Release -r linux-x64 --self-contained true -o /tmp/pub-linux-x64
```

Then tar `/tmp/pub-linux-x64`, `scp` it to `agopenweb@TABLET_TAILSCALE_IP` (key
`~/.ssh/agpc_ed25519`), unpack over `/opt/agopenweb` and
`sudo systemctl restart agopenweb`. **wwwroot is embedded in the binary — a
change to `index.html`, `app.js` or `transport.js` does nothing until you
rebuild.**
