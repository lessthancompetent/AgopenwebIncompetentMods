# Rate control — handoff

**Written 2026-08-16.** Two problems were reported: the *Use rate control* toggle
did nothing, and a bench module would not connect. The toggle is **fixed and
deployed**. The module problem is **diagnosed but not fixed** — it needs hands on
the hardware, not a code change.

Repo: `C:\Users\OEM\.claude\sessions\Agvalonia Rework\AgOpenWeb`, branch
`feature/route-planning`. Relevant commits, newest last:

| Commit | What |
|---|---|
| `528929a3` | Module setup: valve wiring, board defaults, relay pins, colour |
| `8ca29bbc` | Relay functions (16 outputs per module) |
| `357c978b` | **Rate control toggle fix** (this document) |

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

## 2. The bench module — DIAGNOSED, needs hardware work

Symptom: Module Setup shows *no modules heard*; products show not connected.

### How discovery actually works (verified against source + firmware)

- The host is a **pure passive listener**. `modulesHeard` is populated only by
  inbound **PGN 32400** (sensor) or **32401** (module status) arriving on
  `0.0.0.0:29999`. There is **no handshake** — the module transmits unprompted at
  ~5 Hz. So *no modules heard* means literally zero valid frames reached the
  socket. It is a network problem, never an app-config problem.
- A product shows `connected: true` only if **Enabled** AND `ModuleId` matches
  AND `SensorId` matches AND a 32400 arrived in the last 4 s. PGN 32401 alone
  never marks a product connected.
- Ports: host listens **29999**, modules listen **28888** (`RcPgn.cs:140-141`).

### What I found on this PC (2026-08-16, ~19:00)

| Check | Result |
|---|---|
| Ethernet adapter | **Disconnected, 0 bps** — the `192.168.1.10` address is a dead static |
| Wi-Fi | Up, `192.168.5.28`, SSID *HOUSE_SSID* |
| Module MAC `ec:e3:34:aa:e9:90` on 192.168.5.x | **absent** from ARP; full ping sweep found no unexplained host |
| `RateModule_*` access point | **not visible** in a Wi-Fi scan |
| Ping 192.168.1.50 / .51 / .52 | no reply (and no link anyway) |
| **COM12 `USB-SERIAL CH340`** | **present** — the module is powered and plugged into this PC by USB |
| UDP 29999 | held by **`RateController.exe`** (the WinForms app), PID 50156, started 18:03 |
| `AgOpenWeb.Desktop` | **not running on this PC** |

**Conclusion: the module is powered (USB) but is not on any IP network this PC
can reach.** It is almost certainly still configured for the `rtkwifi` phone
hotspot from the earlier session, which is not currently broadcasting — so it
sits retrying forever.

### Two firmware behaviours that bite here

Both confirmed in `Modules/ESP32 Rate/RC_ESP32/`:

1. **Ethernet is exclusive.** If a W5500 is fitted and the link is UP, every
   report goes out Ethernet only and the Wi-Fi transmit is skipped entirely
   (`Send.ino:72-92, 190-210, 229-249, 340-360` all follow
   `if (ChipFound) { if (linkStatus()==LinkON) {...; Sent=true; } } if (!Sent) { wifi }`).
   **Plugging a cable in kills Wi-Fi reporting.**
2. **The module broadcasts to its OWN saved subnet, not yours.**
   `Begin.ino:147`: `Ethernet_DestinationIP = IPAddress(IP0, IP1, IP2, 255)` —
   factory `192.168.1.255`. Windows silently discards a `192.168.1.255` datagram
   arriving on a `192.168.5.x` NIC *before any socket sees it*. This is exactly
   the failure seen earlier with the module at `192.168.1.51`.

### Recovery, in order

1. **Free the serial port and read the console.** `COM12` is currently
   `Access denied` — something holds it, almost certainly the running
   `RateController.exe`. Close it, then:
   ```powershell
   $sp = New-Object System.IO.Ports.SerialPort 'COM12',38400,'None',8,'One'
   $sp.Open(); Start-Sleep 10; $sp.ReadExisting(); $sp.Close()
   ```
   App serial is **38400**. The boot log says which SSID it is trying and whether
   it got an address. *Do this first — everything else is guesswork without it.*
2. **Bring up whatever it is looking for**, or put it back in AP mode. Its AP is
   `RateModule_<MAC>`, open by default, at `192.168.(200+ModuleID).1`.
   Remember `#define ModStringLengths 15` → **SSID and password max 14 chars**
   ("HOUSE_SSID" is 18 and silently truncates; `rtkwifi` fits).
3. **If using Ethernet:** the PC's Ethernet link must actually be up, and the
   module's saved subnet must match the PC's. Set it from Module Setup →
   Network → *Set wired subnet* (PGN 32503; the module reboots onto it). Wired
   address is **static**: `ip0.ip1.ip2.(50 + moduleId)`, gateway `.1`.
4. **Watch for frames** once it is on a network — with AgOpenWeb running:
   ```powershell
   Get-NetUDPEndpoint -LocalPort 29999
   ```
   and to see actual traffic you will need Wireshark (Windows ships nothing that
   dumps UDP payloads). Filter: `udp.port==28888 || udp.port==29999`.

### Watch out: `RateController.exe` competes

The WinForms app binds **29999** and **17777** and appears to hold **COM12**.
Our service sets `SO_REUSEADDR` so both can bind 29999, but on Windows a
**unicast** datagram is delivered to only one of them. Module broadcasts reach
both; anything unicast may be eaten by whichever app bound last. **Close
RateController before testing AgOpenWeb**, and vice versa. It is also the best
cross-check available: if RateController cannot see the module either, the
problem is definitively the module/network.

### A configuration trap worth knowing (not the current cause)

`p.Enabled` gates **all** transmission — both PGN 32500 (rate settings) and
32501 (relays) sit inside `if (!p.Enabled) continue;` loops in `SendTick()`.
`RateProduct.Enabled` defaults to **false**. So a freshly created tool profile
sends **nothing at all** to any module.

This does not stop *discovery* (that is inbound-only), but it does mean the
module never gets set up and never runs auto PID. Worse, the Rate panel gives no
hint: `app.js:2671` renders a green dot for connected, red for enabled, and
**nothing at all** for a disabled channel — so a disabled channel looks
identical to a channel that does not exist.

**Suggested improvement (not done):** show a grey dot plus "channel disabled"
for disabled channels, so silence is explained rather than invisible.

---

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
