# Product Intent & Feature Map (`PRODUCT.md`)
> **1-Line Summary:** The permanent source of truth for what WPM Overlay should do and feel like, written for humans and AI agents before any code is changed.

## 1. Core Vision & User Experience Intent

* **What This App Does (Plain English):** A tiny floating Windows overlay that measures how fast you type in any application, without interrupting what you are doing.
* **How It Must Feel to Use:** Instant, silent, and unobtrusive. It appears the moment you type, stays out of the way, keeps working offline, and never sends keystrokes or statistics anywhere.
* **Non-Negotiable Product Rules:**
  * Keystroke capture must add no noticeable typing latency.
  * All history stays on the local machine in SQLite.
  * Every metric must be honest: backspaces and accidental key mashing must not inflate speed.
  * Every visual surface must remain readable in all nine themes, including light themes.

## 2. Active Features (Do Not Break)

### [FEATURE: System-Wide-Keystroke-Tracking] | Aliases: global hook, WH_KEYBOARD_LL, input capture, keyboard listener
> **1-Line Summary:** Captures typing in every Windows application through a native low-level keyboard hook.
* **User Intention (Non-Negotiable):** The overlay must work in browsers, editors, chat apps, and games without per-app integration.
* **Visual / UI Behavior:** No visible input field is required; typing anywhere updates the floating HUD.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/KeyboardHook.cs` -> `KeyboardHook.Start`, `KeyboardHook.HookCallback`, `KeyboardHook.IsIgnoredModifier`

### [FEATURE: Rolling-WPM-Metrics] | Aliases: 15s, 30s, 60s, live speed, sliding window
> **1-Line Summary:** Shows live typing speed over rolling 15-second, 30-second, and 60-second windows.
* **User Intention (Non-Negotiable):** The number must react quickly to bursts while also showing a steadier medium-term pace.
* **Visual / UI Behavior:** Appears in the left column of the HUD as `15s`, `30s`, and `60s` rows.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/TypingEngine.cs` -> `TypingEngine.OnCalcTimerElapsed`, `TypingEngine.CalculateWindowWpm`
  * `src/WPMOverlay/Views/MainWindow.xaml` -> `Row15s`, `Row30s`, `Row60s`

### [FEATURE: Five-Minute-Sustained-WPM] | Aliases: 5m, sustained typing, burst stitching
> **1-Line Summary:** Measures a five-minute sustained pace by stitching active typing bursts together and ignoring reading pauses.
* **User Intention (Non-Negotiable):** A coffee break or a pause to think must not destroy an otherwise strong five-minute result.
* **Visual / UI Behavior:** Appears as the `5m` row in the left column and can be hidden independently.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/TypingEngine.cs` -> `TypingEngine.CalculateSustained5mWpm`
  * `src/WPMOverlay/Views/MainWindow.xaml` -> `Row5m`

### [FEATURE: Net-WPM-Accuracy-Penalty] | Aliases: backspace penalty, error correction, accuracy, net speed
> **1-Line Summary:** Deducts erased characters and wasted correction keystrokes so only correct typing counts toward speed.
* **User Intention (Non-Negotiable):** Typing garbage quickly must never look faster than typing carefully.
* **Visual / UI Behavior:** The HUD shows a live accuracy percentage; the Settings dialog exposes a `0.5x` to `2.0x` penalty slider with an explanation tooltip.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/TypingEngine.cs` -> `TypingEngine.CalculateWindowWpm`, `TypingEngine.EndSession`
  * `src/WPMOverlay/Models/AppSettings.cs` -> `AppSettings.ErrorPenaltyMultiplier`
  * `src/WPMOverlay/Views/SettingsWindow.xaml` -> `SliderPenalty`

### [FEATURE: Smart-Keystroke-Anti-Spam-Filter] | Aliases: key hold, autorepeat, key mash, chatter, oscillation, palm smash
> **1-Line Summary:** Rejects hardware chatter, held keys, unbroken mashing, and two-key oscillation before they reach the metrics.
* **User Intention (Non-Negotiable):** Accidental key holds and keyboard-smash tests must not create fake record scores.
* **Visual / UI Behavior:** Invisible to the user; rejected keystrokes simply do not count.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/TypingEngine.cs` -> `TypingEngine.IsValidTypingKeystroke`

### [FEATURE: Compact-Two-Column-HUD] | Aliases: overlay card, floating HUD, card layout
> **1-Line Summary:** A small two-column card shows live windows on the left and personal records on the right.
* **User Intention (Non-Negotiable):** It must remain glanceable and compact enough to sit in a screen corner without covering work.
* **Visual / UI Behavior:** Left column: `15s`, `30s`, `60s`, `5m`. Right column: `Top`, `Avg`, `Last`. A status pill and accuracy sit in the footer.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/MainWindow.xaml` -> `CardBorder`, `Row15s`, `RowTop`, `StatusDot`, `AccuracyText`
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.UpdateVisuals`

### [FEATURE: Idle-Retention] | Aliases: last session, no flat zero, idle metrics
> **1-Line Summary:** When typing stops, the HUD keeps showing the last session's speed and peaks instead of dropping to zero.
* **User Intention (Non-Negotiable):** The user must still be able to read the result of the burst they just finished.
* **Visual / UI Behavior:** Status changes from green `Live` to blue `Last`; values remain readable.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/TypingEngine.cs` -> `TypingEngine.EndSession`, `LastFinishedAvgWpm`
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.UpdateVisuals`

### [FEATURE: Auto-Hide-And-Pop-In] | Aliases: fade, timeout, grace period, auto show
> **1-Line Summary:** The HUD fades in as soon as typing starts and fades out after the configured idle delay.
* **User Intention (Non-Negotiable):** Moving the mouse away or closing Settings must wait the full configured delay, not hide immediately.
* **Visual / UI Behavior:** Settings exposes a whole-second slider from 1 to 15 seconds (default 4 seconds).
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.StartFadeIn`, `MainWindow.StartFadeOut`, `MainWindow.TriggerAutoHideGracePeriod`, `_autoHideGraceTimer`
  * `src/WPMOverlay/Models/AppSettings.cs` -> `AppSettings.AutoHideDelaySeconds`, `AppSettings.AutoHideEnabled`

### [FEATURE: Drag-Position-And-Always-On-Top] | Aliases: move overlay, topmost, placement, pin
> **1-Line Summary:** The HUD can be dragged anywhere, remembers its position, and can stay above other windows.
* **User Intention (Non-Negotiable):** Position must survive restarts, and the overlay must never get lost behind other windows when pinned.
* **Visual / UI Behavior:** Left-drag moves the card; right-click opens the context menu with `Always on Top` and `Reset Session`.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.Window_MouseLeftButtonDown`, `MainWindow.RestorePosition`, `MainWindow.SavePosition`, `MainWindow.MenuTopmost_Click`
  * `src/WPMOverlay/Models/AppSettings.cs` -> `AppSettings.PositionX`, `AppSettings.PositionY`, `AppSettings.AlwaysOnTop`

### [FEATURE: SQLite-History-Persistence] | Aliases: database, sessions table, typing_history.db
> **1-Line Summary:** Saves completed typing sessions locally so records and averages survive restarts.
* **User Intention (Non-Negotiable):** History must be private, offline, and durable without a server or account.
* **Visual / UI Behavior:** No direct UI; feeds the `Top`, `Avg`, `Last` values and the History dashboard.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/DatabaseService.cs` -> `DatabaseService.InitializeDatabase`, `DatabaseService.SaveSession`, `DatabaseService.GetStatsSummary`, `DatabaseService.GetRecentSessions`

### [FEATURE: History-Analytics-Dashboard] | Aliases: KPI cards, DataGrid, analytics, session list
> **1-Line Summary:** A dedicated window shows five summary cards and a sortable table of recent sessions.
* **User Intention (Non-Negotiable):** The user can inspect and understand their typing history without reading raw database rows.
* **Visual / UI Behavior:** Cards show All-Time Top, Today's Top, Lifetime Average, Total Words, and Total Sessions; the grid lists the 250 most recent sessions.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/HistoryWindow.xaml` -> `GridSessions`, `TxtAllTimeTop`, `TxtTodayTop`, `TxtLifetimeAvg`
  * `src/WPMOverlay/Views/HistoryWindow.xaml.cs` -> `HistoryWindow.LoadData`

### [FEATURE: CSV-History-Export] | Aliases: export csv, backup, spreadsheet
> **1-Line Summary:** Exports up to 10,000 recent sessions to a CSV file for spreadsheets or archiving.
* **User Intention (Non-Negotiable):** The user owns their data and can take it elsewhere.
* **Visual / UI Behavior:** `Export CSV` button in the History window opens a save dialog.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/DatabaseService.cs` -> `DatabaseService.ExportToCsv`
  * `src/WPMOverlay/Views/HistoryWindow.xaml.cs` -> `HistoryWindow.ExportCsv_Click`

### [FEATURE: Selective-History-Deletion] | Aliases: delete sessions, clear all, purge, remove entry
> **1-Line Summary:** Deletes one or many selected sessions, or clears all history, with confirmation and instant metric recalculation.
* **User Intention (Non-Negotiable):** Accidental key-mash sessions must be removable so they stop polluting records.
* **Visual / UI Behavior:** Delete button, context menu, and Delete key all work on the selected rows; `Clear All History` wipes everything after a warning.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/HistoryWindow.xaml.cs` -> `HistoryWindow.DeleteSelected_Click`, `HistoryWindow.GridSessions_PreviewKeyDown`, `HistoryWindow.ClearHistory_Click`
  * `src/WPMOverlay/Services/DatabaseService.cs` -> `DatabaseService.DeleteSession`, `DatabaseService.DeleteSessions`, `DatabaseService.ClearHistory`

### [FEATURE: Adaptive-Theme-Presets] | Aliases: themes, colors, dark mode, light mode, custom hex
> **1-Line Summary:** Nine color presets plus automatic light/dark contrast tokens keep every window readable.
* **User Intention (Non-Negotiable):** No white-on-white dropdowns, invisible labels, or theme-specific hardcoded colors.
* **Visual / UI Behavior:** Settings offers CyberDark, LightBlue, Glassmorphism, Obsidian, Material3Cyan, CyberpunkNeon, EmeraldTerminal, MidnightBlue, and OLEDMinimal, with a live preview swatch.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Models/AppSettings.cs` -> `AppSettings.ApplyThemePreset`
  * `src/WPMOverlay/Services/ThemeManager.cs` -> `ThemeManager.ApplyTheme`
  * `src/WPMOverlay/Views/SettingsWindow.xaml` -> `CmbPreset`, `PreviewBorder`

### [FEATURE: Live-Settings-Preview-And-Metric-Toggles] | Aliases: apply now, visible chips, opacity, scale
> **1-Line Summary:** Settings changes can be previewed on the real overlay before saving, and each metric row can be hidden.
* **User Intention (Non-Negotiable):** The user can tune appearance without restarting the app or guessing.
* **Visual / UI Behavior:** `Apply Now` previews live; opacity, scale, theme, penalty, and seven metric toggles are adjustable.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/SettingsWindow.xaml.cs` -> `SettingsWindow.Apply_Click`, `SettingsWindow.CommitToSettings`
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.ApplySettings`
  * `src/WPMOverlay/Views/SettingsWindow.xaml` -> `Chk15s`, `Chk30s`, `Chk60s`, `Chk5m`, `SliderOpacity`, `SliderScale`

### [FEATURE: System-Tray-Control] | Aliases: tray icon, notify icon, context menu
> **1-Line Summary:** A tray icon keeps the app reachable when the overlay is hidden and shows Top/Last speed in its tooltip.
* **User Intention (Non-Negotiable):** Closing the overlay must not kill the background tracker; the tray is the control center.
* **Visual / UI Behavior:** Tray menu offers Show/Hide, History, Settings, and Exit; double-click toggles visibility.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Views/MainWindow.xaml.cs` -> `MainWindow.SetupTrayIcon`, `MainWindow.ToggleOverlayVisibility`

### [FEATURE: Windows-Boot-Startup] | Aliases: auto start, registry run key, launch on login
> **1-Line Summary:** Optionally starts WPM Overlay automatically when the user signs in to Windows.
* **User Intention (Non-Negotiable):** Turning the option off must remove the startup entry cleanly.
* **Visual / UI Behavior:** `Start automatically with Windows` checkbox in Settings.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/Services/StartupManager.cs` -> `StartupManager.SetStartup`, `StartupManager.IsStartupEnabled`
  * `src/WPMOverlay/Views/SettingsWindow.xaml.cs` -> `SettingsWindow.Apply_Click`, `SettingsWindow.SaveClose_Click`

### [FEATURE: Portable-Single-File-Executable] | Aliases: exe, no install, release, portable
> **1-Line Summary:** Ships as one self-contained Windows executable with no installer and no runtime download.
* **User Intention (Non-Negotiable):** A user can download one file, double-click it, and start typing immediately.
* **Visual / UI Behavior:** The executable appears in GitHub Releases and runs from any folder.
* **Code Anchors (Grep Targets):**
  * `src/WPMOverlay/WPMOverlay.csproj` -> `TargetFramework`, `UseWPF`, `ApplicationIcon`
  * `.github/workflows/release.yml` -> `publish-portable`

## 3. Planned Features & App-Merge Wishlist

> **1-Line Summary:** No additional features have been approved yet; record new intentions here before building them.
* **Current Wishlist:** Empty. Add a `### [WISHLIST: ...]` block only when the user explicitly asks for a new capability.
