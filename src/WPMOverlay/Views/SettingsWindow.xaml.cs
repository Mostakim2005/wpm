using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WPMOverlay.Models;
using WPMOverlay.Services;

namespace WPMOverlay.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _tempSettings;
        private bool _isInitializing = true;

        /// <summary>
        /// Raised when the user previews settings via "Apply Now" or reverts on cancel/close.
        /// Never persists to config.json or Windows Registry until Save &amp; Close.
        /// </summary>
        public event Action<AppSettings>? PreviewSettingsRequested;

        public SettingsWindow(SettingsService settingsService)
        {
            _settingsService = settingsService;

            // TRAP 1: Clone settings to temp BEFORE calling InitializeComponent()
            // to prevent NullReferenceException during XAML slider ValueChanged deserialization
            _tempSettings = _settingsService.CurrentSettings.Clone();

            InitializeComponent();
            _isInitializing = false;

            Loaded += SettingsWindow_Loaded;
            Closing += SettingsWindow_Closing;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _tempSettings.StartWithWindows = StartupManager.IsStartupEnabled();
            PopulateUiFromSettings();
        }

        private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (DialogResult != true)
            {
                // Revert any unconfirmed live theme/overlay preview when closing via Cancel, X, or Alt+F4
                ThemeManager.ApplyTheme(_settingsService.CurrentSettings);
                PreviewSettingsRequested?.Invoke(_settingsService.CurrentSettings);
            }
        }

        private void PopulateUiFromSettings()
        {
            _isInitializing = true;
            try
            {
                // Select Preset item
                foreach (ComboBoxItem item in CmbPreset.Items)
                {
                    if ((string)item.Tag == _tempSettings.ThemePreset)
                    {
                        CmbPreset.SelectedItem = item;
                        break;
                    }
                }

                SliderOpacity.Value = _tempSettings.WindowOpacity;
                TxtOpacityVal.Text = $"{Math.Round(_tempSettings.WindowOpacity * 100)}%";

                SliderScale.Value = _tempSettings.ScaleFactor;
                TxtScaleVal.Text = $"{_tempSettings.ScaleFactor:F1}x";

                ChkAutoHide.IsChecked = _tempSettings.AutoHideEnabled;
                SliderHideDelay.Value = Math.Round(_tempSettings.AutoHideDelaySeconds);
                TxtDelayVal.Text = $"{Math.Round(_tempSettings.AutoHideDelaySeconds)}s";

                Chk15s.IsChecked = _tempSettings.Show15s;
                Chk30s.IsChecked = _tempSettings.Show30s;
                Chk60s.IsChecked = _tempSettings.Show60s;
                Chk5m.IsChecked = _tempSettings.Show5m;
                ChkTop.IsChecked = _tempSettings.ShowTop;
                ChkAvg.IsChecked = _tempSettings.ShowAvg;
                ChkLast.IsChecked = _tempSettings.ShowLast;

                ChkAlwaysOnTop.IsChecked = _tempSettings.AlwaysOnTop;
                ChkStartWindows.IsChecked = _tempSettings.StartWithWindows;

                SliderPenalty.Value = _tempSettings.ErrorPenaltyMultiplier;
                TxtPenaltyVal.Text = $"{_tempSettings.ErrorPenaltyMultiplier:F1}x";

                ThemeManager.ApplyTheme(_tempSettings);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _tempSettings == null) return;

            if (CmbPreset.SelectedItem is ComboBoxItem selected)
            {
                string preset = (string)selected.Tag;
                _tempSettings.ApplyThemePreset(preset);
                ThemeManager.ApplyTheme(_tempSettings);
            }
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacityVal != null)
                TxtOpacityVal.Text = $"{Math.Round(e.NewValue * 100)}%";

            if (_isInitializing || _tempSettings == null) return;
            _tempSettings.WindowOpacity = e.NewValue;
        }

        private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtScaleVal != null)
                TxtScaleVal.Text = $"{e.NewValue:F1}x";

            if (_isInitializing || _tempSettings == null) return;
            _tempSettings.ScaleFactor = e.NewValue;
        }

        private void SliderHideDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelayVal != null)
                TxtDelayVal.Text = $"{Math.Round(e.NewValue)}s";

            if (_isInitializing || _tempSettings == null) return;
            _tempSettings.AutoHideDelaySeconds = Math.Round(e.NewValue);
        }

        private void SliderPenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPenaltyVal != null)
                TxtPenaltyVal.Text = $"{e.NewValue:F1}x";

            if (_isInitializing || _tempSettings == null) return;
            _tempSettings.ErrorPenaltyMultiplier = e.NewValue;
        }

        private void CommitToSettings(AppSettings s)
        {
            if (CmbPreset.SelectedItem is ComboBoxItem item)
            {
                s.ThemePreset = (string)item.Tag;
                s.ApplyThemePreset(s.ThemePreset);
            }

            s.WindowOpacity = SliderOpacity.Value;
            s.ScaleFactor = SliderScale.Value;

            s.AutoHideEnabled = ChkAutoHide.IsChecked == true;
            s.AutoHideDelaySeconds = Math.Round(SliderHideDelay.Value);

            s.Show15s = Chk15s.IsChecked == true;
            s.Show30s = Chk30s.IsChecked == true;
            s.Show60s = Chk60s.IsChecked == true;
            s.Show5m = Chk5m.IsChecked == true;
            s.ShowTop = ChkTop.IsChecked == true;
            s.ShowAvg = ChkAvg.IsChecked == true;
            s.ShowLast = ChkLast.IsChecked == true;

            s.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
            s.StartWithWindows = ChkStartWindows.IsChecked == true;
            s.ErrorPenaltyMultiplier = SliderPenalty.Value;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Preview live on the overlay without writing config.json or Windows Startup Registry
            CommitToSettings(_tempSettings);
            ThemeManager.ApplyTheme(_tempSettings);
            PreviewSettingsRequested?.Invoke(_tempSettings);
        }

        private void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.UpdateSettings(s => CommitToSettings(s));
            ThemeManager.ApplyTheme(_settingsService.CurrentSettings);
            StartupManager.SetStartup(_settingsService.CurrentSettings.StartWithWindows);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var def = AppSettings.CreateDefault();
            _tempSettings.ThemePreset = def.ThemePreset;
            _tempSettings.ApplyThemePreset(def.ThemePreset);
            _tempSettings.WindowOpacity = def.WindowOpacity;
            _tempSettings.ScaleFactor = def.ScaleFactor;
            _tempSettings.AutoHideEnabled = def.AutoHideEnabled;
            _tempSettings.AutoHideDelaySeconds = def.AutoHideDelaySeconds;
            _tempSettings.Show15s = def.Show15s;
            _tempSettings.Show30s = def.Show30s;
            _tempSettings.Show60s = def.Show60s;
            _tempSettings.Show5m = def.Show5m;
            _tempSettings.ShowTop = def.ShowTop;
            _tempSettings.ShowAvg = def.ShowAvg;
            _tempSettings.ShowLast = def.ShowLast;
            _tempSettings.AlwaysOnTop = def.AlwaysOnTop;
            _tempSettings.StartWithWindows = def.StartWithWindows;
            _tempSettings.ErrorPenaltyMultiplier = def.ErrorPenaltyMultiplier;

            PopulateUiFromSettings();
            PreviewSettingsRequested?.Invoke(_tempSettings);
        }
    }
}
