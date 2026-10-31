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

## 4. Overlay Fade-out During Settings Dialog & Static Settings Dialog Surface

- **Issue Symptom**: 
  1. Changing themes in Settings seemed to do nothing because the floating HUD on screen was at `Opacity = 0.0` (faded out due to user inactivity while navigating dialogs).
  2. The `SettingsWindow` itself had a hardcoded `#0F1117` background, remaining dark even when `LightBlue` or other themes were chosen.
  3. System tray icon contrast at small scale (16x16 / 24x24) was blurry when downscaled by standard anti-aliasing.
- **Root Cause Analysis (RCA)**:
  1. The overlay's auto-hide timer continued running and completed a fade-out animation while the modal Settings dialog was open, leaving the target HUD completely invisible.
  2. `SettingsWindow` lacked tokenized dynamic styling for its own window surface, group boxes, and labels.
  3. Small 16x16 Windows notification icons require manual pixel grid alignment rather than high-radius curves and downscaled fonts.
- **Resolution Strategy**:
  1. Introduced `_isSettingsOpen` state flag in `MainWindow.xaml.cs` that suppresses auto-hide and halts fade-out animations, holding the overlay 100% visible while settings are adjusted.
  2. Wired `CmbPreset_SelectionChanged` to immediately update both the `SettingsWindow` surface (turning `#F0F6FC` ice-blue for `LightBlue`, etc.) and the live desktop HUD simultaneously.
  3. Crafted pixel-perfect bitmap glyphs for 16x16 with pure white high-contrast block lettering for "WPM" and an emerald speed line.
  4. Enabled `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;` in SQLite to ensure concurrent read/write stability during burst typing.
- **Code Anchors**: [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L22-L25), [`MainWindow.xaml.cs`](file:///src/WPMOverlay/Views/MainWindow.xaml.cs#L396-L415), [`SettingsWindow.xaml.cs`](file:///src/WPMOverlay/Views/SettingsWindow.xaml.cs#L100-L165), [`DatabaseService.cs`](file:///src/WPMOverlay/Services/DatabaseService.cs#L35-L42).


