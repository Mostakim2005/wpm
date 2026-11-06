# WPM Overlay

> A tiny, privacy-first Windows overlay that shows your real typing speed in any application.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6.svg)](#requirements)
[![CI](https://github.com/Mostakim2005/wpm/actions/workflows/ci.yml/badge.svg)](https://github.com/Mostakim2005/wpm/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-orange.svg)](LICENSE)

WPM Overlay runs quietly in the background, watches your keystrokes through a native Win32 hook, and displays live 15-second, 30-second, 60-second, and 5-minute typing speeds in a compact floating card. Everything stays on your machine: no account, no cloud, no telemetry.

## Features

- **Works everywhere** — tracks typing in browsers, IDEs, chat apps, games, and documents through a system-wide `WH_KEYBOARD_LL` hook with no perceptible latency.
- **Honest speed** — net WPM deducts backspaces and mistyped characters, with an adjustable `0.5x` to `2.0x` error penalty.
- **Four live windows** — 15s, 30s, and 60s rolling speed plus a 5-minute sustained figure that stitches active bursts and ignores reading pauses.
- **Records and retention** — Top, Avg, and Last values stay on screen when you stop typing instead of resetting to zero.
- **Anti-spam filter** — held keys, hardware chatter, keyboard mashing, and two-key oscillation never inflate your records.
- **Auto-hide with a real grace period** — pops in when you type and fades out after your configured 1–15 second delay.
- **Local history dashboard** — five KPI cards, a 250-session table, selective deletion, clear-all, and CSV export.
- **Nine adaptive themes** — every surface, popup, and label stays readable in both dark and light presets.
- **Live customization** — preview themes on the real overlay, adjust opacity and scale, and hide any metric row.
- **Tray control and optional auto-start** — manage the app from the system tray and launch it with Windows.
- **One portable file** — self-contained `.exe`, no installer and no .NET runtime download.

## Download

Grab the latest portable build from **[GitHub Releases](https://github.com/Mostakim2005/wpm/releases/latest)**.

1. Download `WPMOverlay-v*-Portable.exe`.
2. Double-click it. The overlay appears in the top-right corner.
3. Type anywhere to see live speeds.
4. Right-click the overlay for Settings, History, Always on Top, and Reset Session.

> Windows SmartScreen may warn about an unsigned executable. Choose **More info → Run anyway** if you trust the source.

## Requirements

- Windows 10 or Windows 11, x64
- No installation required for the portable release
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) only if building from source

## Build From Source

```powershell
git clone https://github.com/Mostakim2005/wpm.git
cd wpm

dotnet restore WPMOverlay.sln
dotnet build WPMOverlay.sln -c Release
dotnet test WPMOverlay.sln -c Release --no-build
dotnet run --project src/WPMOverlay/WPMOverlay.csproj
```

To produce the portable executable locally:

```powershell
dotnet publish src/WPMOverlay/WPMOverlay.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o artifacts/publish
```

The result is `artifacts/publish/WPMOverlay.exe`.

## Where Your Data Lives

| Item | Location |
| --- | --- |
| Settings | `%APPDATA%\WPMOverlay\config.json` |
| Typing history | `%APPDATA%\WPMOverlay\typing_history.db` |
| Portable executable | Anywhere you choose; it is not installed |

WPM Overlay makes no network requests. History is stored in a local SQLite database using WAL journaling, and can be exported to CSV or deleted from the History window.

## Documentation

| Document | What it covers |
| --- | --- |
| [PRODUCT.md](docs/PRODUCT.md) | User intent, active features, and the approved wishlist. |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | System map, tech-stack rationale, subsystems, verification, and extension recipes. |
| [DECISIONS_AND_TRAPS.md](docs/DECISIONS_AND_TRAPS.md) | Why choices were made, what failed, and code that must not be touched. |
| [VISUAL_GUIDE.html](docs/VISUAL_GUIDE.html) | Interactive browser dashboard of intent, architecture, decisions, and landmines. |

Open `docs/VISUAL_GUIDE.html` in any browser for the visual version.

## Project Structure

```text
.
├── docs/                       # Product, architecture, decisions, visual guide
├── src/WPMOverlay/             # C# / .NET 8 WPF application
│   ├── Models/                 # Settings, sessions, summaries, keystrokes
│   ├── Services/               # Hook, engine, database, theme, startup
│   └── Views/                  # HUD, settings, and history windows
├── tests/WPMOverlay.Tests/     # xUnit regression tests
├── .github/workflows/          # CI and tag-triggered portable releases
├── WPMOverlay.sln
├── LICENSE
└── THIRD_PARTY_NOTICES.md
```

## How Releases Work

Pushing a tag such as `v1.3.1` runs the release workflow on `windows-latest`. It restores, tests, publishes a self-contained single-file `win-x64` executable, and attaches it to the GitHub Release. Compiled binaries are intentionally kept out of git.

## Contributing

1. Read `docs/DECISIONS_AND_TRAPS.md` before changing input handling, theming, settings initialization, or database code.
2. Keep every visible color on a `DynamicResource` token so all nine themes stay readable.
3. Run the build and test commands above before opening a pull request.
4. Describe user-visible behavior changes in plain English.

## License

Source code is licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE). Personal, educational, research, and other noncommercial use is permitted; commercial use requires a separate license.

Third-party components and their licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
