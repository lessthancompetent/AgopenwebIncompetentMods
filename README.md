# AgOpenWeb

Cross-platform agricultural GPS guidance — a rewrite of [AgOpenGPS](https://github.com/AgOpenGPS-Official/AgOpenGPS) using HTML/JS, Avalonia, .NET 10, and C#. The UI is a web page served up by the backend host.

> ### 🧪 Trying this test build?
> This release is a **preview for testers** — it carries the whole route-planning feature set
> and lots more on top of stock AgOpenGPS. Fastest way in:
>
> 1. **Download** the bundle for your OS from [**Releases**](../../releases), unzip, and run it
>    (self-contained — no .NET needed).
> 2. **Open `http://localhost:5174`** in a browser (the app also serves this on the LAN, so a
>    cab phone/tablet can connect to `http://<host-ip>:5174`).
> 3. **No hardware? Turn on the simulator** (the green **SIM** button, bottom bar) and you can
>    drive, build fields, plan routes, and try everything with no modules connected.
> 4. **What's new / how things work:** see **[Docs/FEATURES.md](Docs/FEATURES.md)** (an index of
>    everything beyond stock AgOpenGPS) and the plain-language guides in **[Docs/guides/](Docs/guides/README.md)**.
> 5. **Feedback / bugs:** please open an [issue](../../issues) — say your OS, what you did, and
>    what happened. Screenshots help.
>
> Android is a sideload APK (allow "install from unknown sources"). Your tablet/PC needs to be
> on the same network as your AgOpenGPS modules — or just use SIM mode to try the UI.

## Download & run

Grab the latest build from the [**Releases**](../../releases) page. Everything is a
self-contained app (no .NET install needed). Pick the set that matches how you'll run it — in
every mode the host also serves `http://<host>:5174` on the LAN, so cab phones/tablets connect
in a browser.

**🖥️ Desktop** — a window with the guidance UI (host runs in-process):

| OS | Download | Run |
|---|---|---|
| Windows x64 | `agopenweb-win-x64.zip` | run `AgOpenWeb.Desktop.exe` |
| macOS (Apple silicon) | `agopenweb-macos-arm64.dmg` | drag to Applications, launch |
| Linux x64 / arm64 | `agopenweb-launcher-linux-<arch>.tar.gz` | `./run.sh` |

**🛰️ Headless** (Linux + Windows) — no window; UI in a browser at `http://<host>:5174`, auto-starts
on boot (for an in-cab mini-PC / SBC):

| OS | Download | Install |
|---|---|---|
| Linux x64 / arm64 | `agopenweb-linux-<arch>.tar.gz` | `sudo ./install.sh --from app` (systemd) |
| Windows x64 | `agopenweb-win-x64.zip` | `install-service.cmd` → Run as administrator (Windows Service) |

**📱 Mobile** — all-in-one (host + UI on the device):

| Device | Download | Install |
|---|---|---|
| Android | `agopenweb-android.apk` | sideload |

Each download's bundled `README` lists the prerequisites (e.g. the WebView2 Runtime on Windows,
WebKitGTK on Linux).

## Platforms

- Windows x64
- macOS x64 / ARM64
- Linux x64 / ARM64
- Android
- iOS (sideload)

## Tech stack

- **UI:** Avalonia 12
- **MVVM:** CommunityToolkit.Mvvm
- **Runtime:** .NET 10
- **Architecture:** MVVM with dependency injection; ~92% shared cross-platform code
- **Testing:** NUnit, Avalonia.Headless

## Building from source

Prerequisites: .NET 10 SDK

```bash
# Desktop (Windows/macOS/Linux)
dotnet build Platforms/AgOpenWeb.Desktop
dotnet run --project Platforms/AgOpenWeb.Desktop

# Android
dotnet build Platforms/AgOpenWeb.Android

# Run tests
dotnet test
```

See [BUILD.md](BUILD.md) for platform-specific setup, and `deploy/{linux,windows,macos}/` for the
packaging scripts that produce the release bundles above.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the architecture overview, feature list, and how to get
started.

## License

[GNU General Public License v3.0](LICENSE.md)
