# WPM Overlay

> A modern, hardware-accelerated, lightweight Windows desktop application that tracks real-time typing speed (WPM) across all applications.

![WPM Overlay](https://img.shields.io/badge/.NET-8.0-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)
![Architecture](https://img.shields.io/badge/Architecture-x64-brightgreen.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

---

## Key Features

- **System-Wide Input Tracking**: Seamlessly tracks keystrokes across any application (browsers, IDEs, Discord, Word) using a native Win32 low-level hook with zero keystroke delay.
- **Two-Column Compact HUD**: Designed after your sketch:
  - **Left**: Real-time rolling metrics (**15s**, **30s**, **60s WPM**).
  - **Right**: Persistent records & session markers (**Top**, **Avg**, **Last**).
- **Idle Retention**: When you pause or stop typing, the overlay does not wipe to a flat zero. It retains and displays the peak speeds and average of your last active typing burst.
- **Auto-Hide & Auto-Pop**: Pops into view the instant you start typing; smoothly fades out when idle (after a configurable timeout).
- **Net WPM & Accuracy Penalty**: Automatically accounts for backspaces and errors, deducting mistyped characters so only net correct words count toward your speed.
- **SQLite Database Persistence**: All typing bursts and historical metrics are stored in a local SQLite database (`%APPDATA%\WPMOverlay\typing_history.db`).
- **Interactive Analytics Dashboard**: Full history window with KPI summary cards, filterable session DataGrid, and one-click CSV export.
- **On-the-Fly Customization**: Dynamic theme presets (CyberDark, OLEDMinimal, MidnightBlue, EmeraldTerminal, SunsetViolet), custom hex color inputs, scale slider, and opacity slider.
- **Windows System Tray & Auto-Start**: Minimizable to system tray with quick-action context menu and optional Windows boot startup.
- **Single Portable Executable**: Standalone `.exe` with zero installation required.

---

## Quick Start

1. Download [`WPMOverlay-v1.0.0-Portable.exe`](file:///releases/WPMOverlay-v1.0.0-Portable.exe).
2. Double-click to run. The floating overlay appears in the top-right corner.
3. Start typing anywhere to see your live 15s, 30s, and 60s WPM.
4. **Right-click the overlay** to open Settings, view History, or toggle Always on Top.
5. Drag with your left mouse button to reposition the overlay anywhere on screen.

---

## Project Structure

```
├── docs/
│   ├── INDEX.md            # PIA Navigation Map
│   ├── ARCHITECTURE.md     # System Architecture & Diagrams
│   ├── FEATURES.md         # Feature Map & Code Anchors
│   └── BUGS.md             # Defect Log & RCA
├── src/
│   └── WPMOverlay/         # WPF .NET 8 Source Code
├── tests/
│   └── WPMOverlay.Tests/   # Automated Unit & Integration Tests
└── releases/
    └── WPMOverlay-v1.0.0-Portable.exe
```
