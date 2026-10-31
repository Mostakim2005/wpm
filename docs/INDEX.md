# WPM Overlay Documentation Index

Welcome to the **WPM Overlay** documentation. This project follows the Token-Preserving Progressive Information Architecture (PIA) to provide immediate, targeted context for human developers and AI coding agents.

---

## Documentation Layers

| Layer | Document | Purpose & Scope |
| :--- | :--- | :--- |
| **Level 0** | [INDEX.md](file:///docs/INDEX.md) | Navigation map and directory overview. |
| **Level 1–2** | [ARCHITECTURE.md](file:///docs/ARCHITECTURE.md) | High-level system architecture, Win32 hooks, sliding-window calculations, SQLite schema, and UX token systems. |
| **Level 2–3** | [FEATURES.md](file:///docs/FEATURES.md) | Complete feature inventory with exact source file and line anchors. |
| **Level 2–3** | [BUGS.md](file:///docs/BUGS.md) | Defect catalog, root cause analyses (RCA), and regression prevention records. |

---

## Quick Navigation Anchors

- **HUD Overlay Window**: [`MainWindow.xaml`](file:///src/WPMOverlay/Views/MainWindow.xaml), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs)
- **Typing Engine & Metrics Calculation**: [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs)
- **Low-level Win32 Keyboard Hook**: [`KeyboardHook.cs`](file:///src/WPMOverlay/Services/KeyboardHook.cs)
- **SQLite Database Persistence**: [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs)
- **Configuration & Theme System**: [`AppSettings.cs`](file:///src/WPMOverlay/Models/AppSettings.cs), [`SettingsService.cs`](file:///src/WPMOverlay/Services/SettingsService.cs)
- **History & Analytics Dashboard**: [`HistoryWindow.xaml`](file:///src/WPMOverlay/Views/HistoryWindow.xaml)
- **Settings & Personalization Dialog**: [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml)
- **Standalone Portable Binary**: [`WPMOverlay-v1.0.0-Portable.exe`](file:///releases/WPMOverlay-v1.0.0-Portable.exe)
