# Project Operating Instructions (`AGENTS.md`)
> **Notice:** This project inherits user persona, confirmation gates, and documentation blueprints from the global master `AGENTS.md`. Do not duplicate those rules here.

## 1. Project Tech Stack & Environment
* **Runtime / Language:** C# / .NET 8 (`net8.0-windows`)
* **Framework / Host:** WPF (`<UseWPF>true</UseWPF>`) + Windows Forms (`<UseWindowsForms>true</UseWindowsForms>` strictly for system tray `NotifyIcon`)
* **Target OS / Build Output:** Windows 10/11 x64 portable single-file `.exe` (`artifacts/publish/WPMOverlay.exe`)

## 2. Mandatory Verification & Build Commands
* **Build Solution:** `dotnet build WPMOverlay.sln -c Release`
* **Test Suite:** `dotnet test WPMOverlay.sln -c Release`
* **Run App:** `dotnet run --project src/WPMOverlay/WPMOverlay.csproj`
* **Portable Exe Publish:** `dotnet publish src/WPMOverlay/WPMOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish`

## 3. Repository Directory Structure & Conventions
* `src/WPMOverlay/` — C# .NET 8 WPF application:
  * `Models/` — `AppSettings.cs`, `TypingSession.cs`, `StatsSummary.cs`, `KeystrokeEvent.cs`.
  * `Services/` — `KeyboardHook.cs` (`WH_KEYBOARD_LL`), `TypingEngine.cs` (rolling buffers & anti-spam), `DatabaseService.cs` (SQLite local storage), `ThemeManager.cs` (dynamic token engine), `SettingsService.cs`, `StartupManager.cs`.
  * `Views/` — `MainWindow.xaml` (floating HUD overlay), `SettingsWindow.xaml`, `HistoryWindow.xaml`.
* `tests/WPMOverlay.Tests/` — xUnit regression test suite (12 tests covering rolling WPM, sustained bursts, accuracy penalties, anti-spam, token engine, and BAML safety).
* `docs/` — Core Search-First documentation suite:
  * `PRODUCT.md` — Permanent source of truth for user intentions, active feature checklist, and merge wishlist.
  * `ARCHITECTURE.md` — Visual Mermaid diagrams, tech stack rationale, subsystem mechanics, and extension recipes.
  * `DECISIONS_AND_TRAPS.md` — Architectural pivots and engineering landmines.
  * `VISUAL_GUIDE.html` — Interactive browser dashboard rendering intentions, Mermaid diagrams, decisions, and landmines visually.
* `.github/workflows/` — `ci.yml` (automated testing) and `release.yml` (tag-triggered portable `.exe` releases).

## 4. Repository-Specific Engineering Traps
* **TRAP 1: SettingsWindow BAML Initialization Order** — Never construct `_tempSettings` after `InitializeComponent()`, and never remove `_isInitializing` / null checks from slider handlers in `SettingsWindow.xaml.cs`.
* **TRAP 2: DynamicResource for All Theme Tokens** — Never hardcode colors or use `StaticResource` for brushes. All nine themes morph dynamically; static resources cause contrast inversions on theme changes.
* **TRAP 3: Zero-Latency Low-Level Keyboard Hook** — `KeyboardHook.HookCallback` must remain minimal, non-blocking, and always call `CallNextHookEx`. Offload all calculations and SQLite writes to background timers.
* **TRAP 4: Assembly-Qualified Pack URIs** — Always use `pack://application:,,,/WPMOverlay;component/Assets/...` for embedded icons to avoid test runner crashes (`testhost.exe`).
* **TRAP 5: WPF-WinForms Type Collisions** — Keep explicit type aliases (`WpfApplication`, `WpfColor`, `WpfMessageBox`, `WpfSaveFileDialog`) whenever referencing shared types across WPF and WinForms.
* **TRAP 6: ComboBox Popup Tokenization** — Always use `DynamicComboBoxStyle` for dropdowns; native WPF combo popups render hardcoded white surfaces that fail contrast in dark themes.
* **TRAP 7: Tag-Triggered Releases Only** — Never commit compiled binary executables into git. The portable executable is built by GitHub Actions on version tags (`v*`).
