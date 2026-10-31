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
