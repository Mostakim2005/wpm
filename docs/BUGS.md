# Bug Log & Root Cause Analysis (RCA): WPM Overlay

This document records defects identified during development, compilation, and testing, documenting their symptoms, root cause analyses, and permanent resolution strategies.

---

## 1. Ambiguous Reference between WPF and Windows Forms Types

- **Issue Symptom**: Build failure reporting:
  - `error CS0104: 'Application' is an ambiguous reference between 'System.Windows.Forms.Application' and 'System.Windows.Application'`
  - `error CS0104: 'MouseEventArgs' is an ambiguous reference between 'System.Windows.Forms.MouseEventArgs' and 'System.Windows.Input.MouseEventArgs'`
  - `error CS0104: 'Color' is an ambiguous reference between 'System.Drawing.Color' and 'System.Windows.Media.Color'`
  - `error CS0104: 'MessageBox' is an ambiguous reference between 'System.Windows.Forms.MessageBox' and 'System.Windows.MessageBox'`
- **Root Cause Analysis (RCA)**: Enabling `<UseWindowsForms>true</UseWindowsForms>` alongside `<UseWPF>true</UseWPF>` (to utilize `System.Windows.Forms.NotifyIcon` for system tray support) imports implicit usings for both WinForms and WPF namespaces. Identically named types in both libraries created compilation collisions.
- **Resolution Strategy**:
  - In `App.xaml.cs`, explicitly inherit from `System.Windows.Application`.
  - In `MainWindow.xaml.cs` and `HistoryWindow.xaml.cs`, explicitly alias WPF types:
    ```csharp
    using WpfColor = System.Windows.Media.Color;
    using WpfColorConverter = System.Windows.Media.ColorConverter;
    using WpfMessageBox = System.Windows.MessageBox;
    using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
    ```
  - Disambiguated `System.Windows.Input.MouseEventArgs` in mouse enter/leave event handlers.
- **Code Anchors**: [`App.xaml.cs`](file:///src/WPMOverlay/App.xaml.cs#L10), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L6-L10), [`HistoryWindow.xaml.cs`](file:///src/WPMOverlay/Views/HistoryWindow.xaml.cs#L3-L4).

---

## 2. UTF-8 Byte Order Mark (BOM) in Configuration File

- **Issue Symptom**: Python script encountered `json.decoder.JSONDecodeError: Unexpected UTF-8 BOM (decode using utf-8-sig)` when reading `C:\Users\kazir\.gemini\config\config.json`.
- **Root Cause Analysis (RCA)**: The JSON configuration file was saved with a UTF-8 Byte Order Mark (EF BB BF), which standard Python `utf-8` decoding rejects.
- **Resolution Strategy**: Switched file reader encoding to `utf-8-sig`, which transparently strips the BOM.
- **Code Anchors**: Automation update scripts for `config.json`.

---

## 3. Contrast Inversion on ComboBox Dropdown Popups & Missing Tray Icon

- **Issue Symptom**: 
  1. The theme preset ComboBox dropdown in `SettingsWindow` rendered light/white text on a white/light dropdown popup surface (contrast inversion / WCAG 1.4.3 violation).
  2. The application in the Windows system tray displayed a generic OS fallback placeholder icon instead of a branded keyboard speedometer icon.
  3. The auto-hide delay slider displayed arbitrary decimal values (`4.0s`, `7.2s`) rather than snapping cleanly to whole 1-second intervals.
- **Root Cause Analysis (RCA)**:
  1. Standard WPF `ComboBox` embeds an unstyled native Win32 `Popup` container that inherits `SystemColors.WindowBrush` (pure `#FFFFFF` white) unless its `ItemContainerStyle` and `Popup` template are explicitly tokenized.
  2. The project lacked an explicit `<ApplicationIcon>` resource in its `.csproj` and `MainWindow.xaml.cs` fell back to `System.Drawing.SystemIcons.Application`.
  3. The `Slider` lacked `IsSnapToTickEnabled="True"` and `TickFrequency="1"`.
- **Resolution Strategy**:
  1. Implemented `DarkComboBoxStyle` in `SettingsWindow.xaml.Resources` featuring an explicit dark `#161B26` popup canvas, illuminated `#38BDF8` borders, and high-contrast `#F8FAFC` item text with `#2563EB` hover backgrounds.
  2. Generated a multi-resolution `app_icon.ico` (16x16 to 256x256), embedded it into the `.csproj`, and wired it to `MainWindow`, `HistoryWindow`, `SettingsWindow`, and the `NotifyIcon` system tray instance.
  3. Replaced floating-point slider increments with strict 1-second snaps (`Minimum="1"`, `Maximum="15"`, `TickFrequency="1"`) formatted purely as integers (`$"{val}s"`).
- **Code Anchors**: [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L12-L95), [`SettingsWindow.xaml.cs`](file:///src/WPMOverlay/Views/SettingsWindow.xaml.cs#L75-L125), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L71-L90), [`WPMOverlay.csproj`](file:///src/WPMOverlay/WPMOverlay.csproj#L9-L15).

---

## 5. Auto-Hide Delay Invisibility on Light Themes, Premature Auto-Hide on Interaction, and Accidental Key Mash / Spam Noise

- **Issue Symptom**:
  1. The Auto-Hide delay indicator (`TxtDelayVal`) was not visible when switching to light themes (`LightBlue`), appearing blank or unselected.
  2. The auto-hide delay (e.g. 10s) felt ignored; closing settings or moving the mouse off the overlay caused the HUD to fade out immediately in 0ms without waiting the 10 seconds.
  3. Accidentally holding down keys (auto-repeat) or rapid mashing of keys inflated the real-time and peak WPM metrics.
  4. Visual elements across windows contained residual hardcoded color values, risking contrast inversions across custom themes.
- **Root Cause Analysis (RCA)**:
  1. `TxtDelayVal` and slider labels in `SettingsWindow.xaml` had hardcoded `Foreground="#F8FAFC"` (white) which rendered invisibly against light backgrounds (`#F0F6FC`).
  2. `MainWindow.xaml.cs` previously invoked `OnTypingIdle()` directly in `Window_MouseLeave` and immediately following `settingsWin.ShowDialog()`, instantly aborting the visible state instead of running an inactivity countdown.
  3. `TypingEngine.cs` processed raw Win32 `WM_KEYDOWN` messages indiscriminately, allowing 30Hz key-repeat events and palm-mashing chatter to be counted as net words.
- **Resolution Strategy**:
  1. **Dynamic Resource Architecture (`ThemeManager.cs`)**: Replaced all hardcoded colors across `MainWindow.xaml`, `SettingsWindow.xaml`, and `HistoryWindow.xaml` with `{DynamicResource}` token brushes (`AppBackgroundBrush`, `AppSurfaceBrush`, `AppSurfaceElevatedBrush`, `AppBorderBrush`, `AppAccentBrush`, `AppHighlightBrush`, `AppTextPrimaryBrush`, `AppTextSecondaryBrush`, `AppTextOnAccentBrush`). All 9 themes now adapt with guaranteed WCAG AAA contrast in both dark and light modes.
  2. **Smart Keystroke & Anti-Spam Filter (`TypingEngine.IsValidTypingKeystroke`)**:
     - *Anti-Autorepeat / Key Hold*: Caps consecutive identical non-backspace characters to at most 2; 3rd+ repeats are blocked.
     - *Anti-Chatter / Mash*: Rejects keystrokes arriving $<30$ms apart as hardware bounce or simultaneous palm hits.
     - *Anti-Keyboard Roll*: Caps unbroken non-delimited character sequences to 25 characters until a word boundary (Space, Enter, Tab, Backspace) is struck.
     - *Anti-Oscillation*: Detects and suppresses 2-key rapid alternation spam ($>4$ cycles).
  3. **Inactivity Grace Period Timer (`_autoHideGraceTimer`)**: Configured a `DispatcherTimer` in `MainWindow.xaml.cs` respecting `AutoHideDelaySeconds` so moving the mouse away or closing settings waits the exact configured interval before initiating the fade-out animation.
- **Code Anchors**: [`ThemeManager.cs`](file:///src/WPMOverlay/Services/ThemeManager.cs#L12-L85), [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L190-L205), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L36-L45), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L295-L315), [`TypingEngine.cs`](file:///src/WPMOverlay/Services/TypingEngine.cs#L105-L180).


---

## 6. NullReferenceException during SettingsWindow Initialization & Cross-Assembly Pack URI Resolution

- **Issue Symptom**:
  1. Opening Settings from either the HUD right-click menu or the system tray menu crashed or threw `System.NullReferenceException: Object reference not set to an instance of an object at WPMOverlay.Views.SettingsWindow.SliderOpacity_ValueChanged`.
  2. The floating overlay hid or disappeared upon right-clicking Settings because the unhandled exception aborted execution.
  3. Unit test runners crashed when loading XAML components with `System.IO.IOException: Cannot locate resource 'app_icon.ico'`.
- **Root Cause Analysis (RCA)**:
  1. In WPF, during `InitializeComponent()`, setting `Minimum="0.4"` on `SliderOpacity` in XAML coerces `Value` and immediately raises the `ValueChanged` routed event before constructor execution finishes. `_tempSettings` was being instantiated *after* `InitializeComponent()`, leaving `_tempSettings` null when `SliderOpacity_ValueChanged` executed `_tempSettings.WindowOpacity = e.NewValue;`.
  2. `pack://application:,,,/app_icon.ico` assumes the active executing entry assembly is `WPMOverlay.exe`. When instantiated from secondary assemblies or test runners (`testhost.exe`), WPF fails to locate the resource unless explicitly qualified with the component assembly name (`/WPMOverlay;component/...`).
- **Resolution Strategy**:
  1. **Pre-Initialization Cloning & Guard Flags**: Instantiated `_tempSettings` *before* `InitializeComponent()` in [`SettingsWindow.xaml.cs`](file:///src/WPMOverlay/Views/SettingsWindow.xaml.cs#L18-L45) and introduced `_isInitializing = true;` flag alongside null guards (`if (_isInitializing || _tempSettings == null) return;`) across all slider and combobox event handlers.
  2. **Try/Catch/Finally Safeguards**: Wrapped dialog openings in [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L375-L420) within structured error handlers, ensuring `_isSettingsOpen = false;` executes in `finally` and never leaves the overlay in a locked/hidden state.
  3. **Universal Pack URIs**: Updated all window icons to `pack://application:,,,/WPMOverlay;component/app_icon.ico`.
  4. **Automated Unit Testing**: Added [`SettingsWindow_Instantiation_DoesNotThrowNullReferenceException`](file:///tests/WPMOverlay.Tests/TypingEngineTests.cs#L185-L205) running on an STA thread to continuously guard against regression.
- **Code Anchors**: [`SettingsWindow.xaml.cs`](file:///src/WPMOverlay/Views/SettingsWindow.xaml.cs#L18-L50), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L380-L415), [`SettingsWindow.xaml`](file:///src/WPMOverlay/Views/SettingsWindow.xaml#L5), [`HistoryWindow.xaml`](file:///src/WPMOverlay/Views/HistoryWindow.xaml#L5).


