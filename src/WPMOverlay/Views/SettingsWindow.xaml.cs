using System;
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

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;

            // Clone settings to temp
            var current = _settingsService.CurrentSettings;
            _tempSettings = new AppSettings
            {
                ThemePreset = current.ThemePreset,
                BackgroundHex = current.BackgroundHex,
                BorderHex = current.BorderHex,
                AccentHex = current.AccentHex,
                TextPrimaryHex = current.TextPrimaryHex,
                TextSecondaryHex = current.TextSecondaryHex,
                HighlightHex = current.HighlightHex,
                WindowOpacity = current.WindowOpacity,
                ScaleFactor = current.ScaleFactor,
                AutoHideEnabled = current.AutoHideEnabled,
                AutoHideDelaySeconds = current.AutoHideDelaySeconds,
                IdleDisplayMode = current.IdleDisplayMode,
                Show15s = current.Show15s,
                Show30s = current.Show30s,
                Show60s = current.Show60s,
                ShowTop = current.ShowTop,
                ShowAvg = current.ShowAvg,
                ShowLast = current.ShowLast,
                AlwaysOnTop = current.AlwaysOnTop,
                StartWithWindows = current.StartWithWindows,
                ErrorPenaltyMultiplier = current.ErrorPenaltyMultiplier
            };

            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateUiFromSettings();
        }

        private void PopulateUiFromSettings()
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

            TxtBgHex.Text = _tempSettings.BackgroundHex;
            TxtBorderHex.Text = _tempSettings.BorderHex;
            TxtAccentHex.Text = _tempSettings.AccentHex;
            TxtTextHex.Text = _tempSettings.TextPrimaryHex;

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
            ChkTop.IsChecked = _tempSettings.ShowTop;
            ChkAvg.IsChecked = _tempSettings.ShowAvg;
            ChkLast.IsChecked = _tempSettings.ShowLast;

            ChkAlwaysOnTop.IsChecked = _tempSettings.AlwaysOnTop;
            ChkStartWindows.IsChecked = StartupManager.IsStartupEnabled();

            SliderPenalty.Value = _tempSettings.ErrorPenaltyMultiplier;
            TxtPenaltyVal.Text = $"{_tempSettings.ErrorPenaltyMultiplier:F1}x";
        }

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPreset.SelectedItem is ComboBoxItem selected)
            {
                string preset = (string)selected.Tag;
                _tempSettings.ApplyThemePreset(preset);

                TxtBgHex.Text = _tempSettings.BackgroundHex;
                TxtBorderHex.Text = _tempSettings.BorderHex;
                TxtAccentHex.Text = _tempSettings.AccentHex;
                TxtTextHex.Text = _tempSettings.TextPrimaryHex;
            }
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacityVal != null)
                TxtOpacityVal.Text = $"{Math.Round(e.NewValue * 100)}%";
        }

        private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtScaleVal != null)
                TxtScaleVal.Text = $"{e.NewValue:F1}x";
        }

        private void SliderHideDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelayVal != null)
                TxtDelayVal.Text = $"{Math.Round(e.NewValue)}s";
        }

        private void SliderPenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPenaltyVal != null)
                TxtPenaltyVal.Text = $"{e.NewValue:F1}x";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.UpdateSettings(s =>
            {
                if (CmbPreset.SelectedItem is ComboBoxItem item)
                {
                    s.ThemePreset = (string)item.Tag;
                }

                s.BackgroundHex = TxtBgHex.Text.Trim();
                s.BorderHex = TxtBorderHex.Text.Trim();
                s.AccentHex = TxtAccentHex.Text.Trim();
                s.TextPrimaryHex = TxtTextHex.Text.Trim();

                s.WindowOpacity = SliderOpacity.Value;
                s.ScaleFactor = SliderScale.Value;

                s.AutoHideEnabled = ChkAutoHide.IsChecked == true;
                s.AutoHideDelaySeconds = Math.Round(SliderHideDelay.Value);

                s.Show15s = Chk15s.IsChecked == true;
                s.Show30s = Chk30s.IsChecked == true;
                s.Show60s = Chk60s.IsChecked == true;
                s.ShowTop = ChkTop.IsChecked == true;
                s.ShowAvg = ChkAvg.IsChecked == true;
                s.ShowLast = ChkLast.IsChecked == true;

                s.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
                s.StartWithWindows = ChkStartWindows.IsChecked == true;
                s.ErrorPenaltyMultiplier = SliderPenalty.Value;
            });

            // Update Windows startup registry
            StartupManager.SetStartup(ChkStartWindows.IsChecked == true);

            DialogResult = true;
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
            _tempSettings.Show15s = true;
            _tempSettings.Show30s = true;
            _tempSettings.Show60s = true;
            _tempSettings.ShowTop = true;
            _tempSettings.ShowAvg = true;
            _tempSettings.ShowLast = true;
            _tempSettings.AlwaysOnTop = true;
            _tempSettings.ErrorPenaltyMultiplier = 1.0;

            PopulateUiFromSettings();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
