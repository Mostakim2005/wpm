# Feature Map & Code Anchors: WPM Overlay

This document inventories every feature in **WPM Overlay** with direct code anchors (file path, class, method, or line range) to allow instant semantic lookup.

---

## 1. Core Engine & Input Tracking

| Feature | Description | File Path & Code Anchor |
| :--- | :--- | :--- |
| **Win32 Global Hook** | Low-level hook (`WH_KEYBOARD_LL`) for listening to keystrokes across all Windows apps. | [`KeyboardHook.cs`](file:///src/WPMOverlay/Services/KeyboardHook.cs#L45-L65) (`EnableHook`, `HookCallback`) |
| **Modifier Filtering** | Ignores Shift, Ctrl, Alt, CapsLock, Win key from triggering typing count. | [`KeyboardHook.cs`](file:///src/WPMOverlay/Services/KeyboardHook.cs#L85-L105) (`IsIgnoredModifier`) |
| **Rolling Windows** | Real-time sliding calculations for 15s, 30s, and 60s windows. | [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs#L190-L215) (`CalculateWindowWpm`) |
| **5m Sustained WPM** | Burst-stitching algorithm calculating 5-minute sustained typing speed across active sessions without pause penalties. | [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs#L220-L270) (`CalculateSustained5mWpm`) |
| **Net WPM & Accuracy** | Net characters formula deducting backspaces and applying error penalties. | [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs#L170-L185) (`OnCalcTimerElapsed`) |
| **Idle Retention** | Holds and shows the last session's active metrics when idle rather than flatlining to zero. | [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs#L220-L270) (`EndSession`), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L220-L245) (`UpdateVisuals`) |

---

## 2. User Interface & Overlay HUD

| Feature | Description | File Path & Code Anchor |
| :--- | :--- | :--- |
| **Compact 2-Column HUD** | Floating transparent card matching the user's sketch layout. | [`MainWindow.xaml`](file:///src/WPMOverlay/Views/MainWindow.xaml#L40-L125) (`CardBorder`, `Grid`) |
| **Smooth Fade In/Out** | Quadratic ease double animations on opacity for pop-in and auto-hide. | [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L250-L280) (`OnTypingStarted`, `OnTypingIdle`) |
| **Draggable & Positioning** | Left-click anywhere on the HUD to move, with automatic coordinate saving. | [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L285-L295) (`Window_MouseLeftButtonDown`) |
| **System Tray Icon** | Background tray icon with live tooltip stats and right-click management menu. | [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L65-L85) (`SetupTrayIcon`) |
| **Theme & Color Tokens** | Non-hardcoded color bindings supporting custom hex and presets. | [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L120-L180) (`ApplySettings`) |

---

## 3. Database, History & Analytics

| Feature | Description | File Path & Code Anchor |
| :--- | :--- | :--- |
| **SQLite Schema & DDL** | Automated creation of `sessions` table and indexes. | [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs#L35-L60) (`InitializeDatabase`) |
| **Session Insertion** | Records timestamped session duration, keys, backspaces, and peaks. | [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs#L62-L105) (`SaveSession`) |
| **Summary Queries** | Calculates all-time peak, today's top, and lifetime average WPM. | [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs#L107-L165) (`GetStatsSummary`) |
| **CSV Export** | Exports full history log to `.csv` format. | [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs#L230-L245) (`ExportToCsv`) |
| **History Dashboard UI** | KPI summary cards, filterable DataGrid, and history management. | [`HistoryWindow.xaml`](file:///src/WPMOverlay/Views/HistoryWindow.xaml#L1-L100), [`HistoryWindow.xaml.cs`](file:///src/WPMOverlay/Views/HistoryWindow.xaml.cs#L25-L55) |

---

## 4. Personalization & Settings

| Feature | Description | File Path & Code Anchor |
| :--- | :--- | :--- |
| **Theme Presets** | 9 adaptive color theory presets: CyberDark, LightBlue, Glassmorphism, Obsidian, Material3Cyan, CyberpunkNeon, EmeraldTerminal, MidnightBlue, OLEDMinimal. | [`AppSettings.cs`](file:///src/WPMOverlay/Models/AppSettings.cs#L45-L135) (`ApplyThemePreset`) |
| **Live Apply & Preview** | Separate "Apply Now" button to see theme live on overlay before closing, plus in-dialog swatch preview. | [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L140-L155), [`SettingsWindow.xaml.cs`](file:///src/WPMOverlay/Views/SettingsWindow.xaml.cs#L180-L195) (`Apply_Click`) |
| **Error Penalty Tooltip** | Interactive `[?]` help explaining error penalty weights (0.5x, 1.0x, 2.0x) and exact formulas. | [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L238-L255) |
| **Metric Visibility** | Toggleable chips for 15s, 30s, 60s, 5m, Top, Avg, Last individually. | [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L210-L225), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L190-L205) |
| **Windows Startup** | Registry integration for starting automatically on Windows boot. | [`StartupManager.cs`](file:///src/WPMOverlay/Services/StartupManager.cs#L12-L45) (`SetStartup`, `IsStartupEnabled`) |
| **Live Dynamic Updates** | Immediate reflection of modified settings in the running HUD without restarting. | [`SettingsService.cs`](file:///src/WPMOverlay/Services/SettingsService.cs#L60-L75) (`UpdateSettings`, `SettingsUpdated`) |
