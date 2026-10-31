using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WPMOverlay.Models;
using WPMOverlay.Services;
using Forms = System.Windows.Forms;

namespace WPMOverlay.Views
{
    public partial class MainWindow : Window
    {
        private readonly KeyboardHook _keyboardHook;
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private readonly TypingEngine _typingEngine;

        private Forms.NotifyIcon? _trayIcon;
        private bool _isHovered = false;
        private bool _isExplicitlyHidden = false;
        private bool _isSettingsOpen = false;

        public MainWindow()
        {
            InitializeComponent();

            _keyboardHook = new KeyboardHook();
            _databaseService = new DatabaseService();
            _settingsService = new SettingsService();
            _typingEngine = new TypingEngine(_keyboardHook, _databaseService, _settingsService);

            ApplySettings(_settingsService.CurrentSettings);

            _settingsService.SettingsUpdated += OnSettingsUpdated;
            _typingEngine.MetricsUpdated += OnMetricsUpdated;
            _typingEngine.TypingStarted += OnTypingStarted;
            _typingEngine.TypingIdle += OnTypingIdle;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupTrayIcon();
            RestorePosition();
            _typingEngine.Start();

            // Initial visual update
            UpdateVisuals();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _settingsService.SettingsUpdated -= OnSettingsUpdated;
            _typingEngine.MetricsUpdated -= OnMetricsUpdated;
            _typingEngine.TypingStarted -= OnTypingStarted;
            _typingEngine.TypingIdle -= OnTypingIdle;

            _typingEngine.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private void SetupTrayIcon()
        {
            System.Drawing.Icon? customIcon = null;
            try
            {
                var iconUri = new Uri("pack://application:,,,/app_icon.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (streamInfo.Stream)
                    {
                        customIcon = new System.Drawing.Icon(streamInfo.Stream);
                    }
                }
            }
            catch
            {
                customIcon = System.Drawing.SystemIcons.Application;
            }

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = customIcon ?? System.Drawing.SystemIcons.Application,
                Text = "WPM Overlay - Typing Speed Tracker",
                Visible = true
            };

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Show / Hide Overlay", null, (s, e) => ToggleOverlayVisibility());
            contextMenu.Items.Add("History & Analytics", null, (s, e) => Dispatcher.Invoke(OpenHistoryWindow));
            contextMenu.Items.Add("Settings", null, (s, e) => Dispatcher.Invoke(OpenSettingsWindow));
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => Dispatcher.Invoke(ExitApplication));

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(ToggleOverlayVisibility);
        }

        private void RestorePosition()
        {
            var settings = _settingsService.CurrentSettings;
            if (settings.PositionX >= 0 && settings.PositionY >= 0)
            {
                Left = settings.PositionX;
                Top = settings.PositionY;
            }
            else
            {
                // Default position: Top Right corner
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                Left = screenWidth - ActualWidth - 40;
                Top = 40;
            }
        }

        private void SavePosition()
        {
            _settingsService.UpdateSettings(s =>
            {
                s.PositionX = Left;
                s.PositionY = Top;
            });
        }

        private void OnSettingsUpdated(AppSettings settings)
        {
            Dispatcher.Invoke(() => ApplySettings(settings));
        }

        private void ApplySettings(AppSettings s)
        {
            // Apply scale
            HudScaleTransform.ScaleX = s.ScaleFactor;
            HudScaleTransform.ScaleY = s.ScaleFactor;

            // Apply colors using ColorConverter
            try
            {
                CardBorder.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.BackgroundHex));
                CardBorder.BorderBrush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.BorderHex));
                VerticalDivider.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.BorderHex));

                var textPrimary = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.TextPrimaryHex));
                var textSecondary = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.TextSecondaryHex));
                var accent = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.AccentHex));
                var highlight = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(s.HighlightHex));

                // Left labels & values
                Label15s.Foreground = textSecondary;
                Value15s.Foreground = accent;
                Unit15s.Foreground = textSecondary;

                Label30s.Foreground = textSecondary;
                Value30s.Foreground = textPrimary;
                Unit30s.Foreground = textSecondary;

                Label60s.Foreground = textSecondary;
                Value60s.Foreground = textPrimary;
                Unit60s.Foreground = textSecondary;

                Label5m.Foreground = textSecondary;
                Value5m.Foreground = accent;
                Unit5m.Foreground = textSecondary;

                // Right labels & values
                LabelTop.Foreground = textSecondary;
                ValueTop.Foreground = highlight;
                UnitTop.Foreground = textSecondary;

                LabelAvg.Foreground = textSecondary;
                ValueAvg.Foreground = textPrimary;
                UnitAvg.Foreground = textSecondary;

                LabelLast.Foreground = textSecondary;
                ValueLast.Foreground = accent;
                UnitLast.Foreground = textSecondary;

                AccuracyText.Foreground = textSecondary;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying color tokens: {ex.Message}");
            }

            // Metric visibility
            Row15s.Visibility = s.Show15s ? Visibility.Visible : Visibility.Collapsed;
            Row30s.Visibility = s.Show30s ? Visibility.Visible : Visibility.Collapsed;
            Row60s.Visibility = s.Show60s ? Visibility.Visible : Visibility.Collapsed;
            Row5m.Visibility = s.Show5m ? Visibility.Visible : Visibility.Collapsed;

            RowTop.Visibility = s.ShowTop ? Visibility.Visible : Visibility.Collapsed;
            RowAvg.Visibility = s.ShowAvg ? Visibility.Visible : Visibility.Collapsed;
            RowLast.Visibility = s.ShowLast ? Visibility.Visible : Visibility.Collapsed;

            Topmost = s.AlwaysOnTop;
            MenuTopmost.IsChecked = s.AlwaysOnTop;

            if (!_isExplicitlyHidden)
            {
                Opacity = s.WindowOpacity;
            }
        }

        private void OnMetricsUpdated()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.Invoke(UpdateVisuals);
        }

        private void UpdateVisuals()
        {
            var summary = _typingEngine.CachedSummary;
            bool active = _typingEngine.IsTypingActive;

            if (active)
            {
                // Live typing active metrics
                Value15s.Text = $"{Math.Round(_typingEngine.Current15sWpm)}";
                Value30s.Text = $"{Math.Round(_typingEngine.Current30sWpm)}";
                Value60s.Text = $"{Math.Round(_typingEngine.Current60sWpm)}";
                Value5m.Text = $"{Math.Round(_typingEngine.Current5mWpm)}";

                ValueTop.Text = $"{Math.Round(summary.AllTimeTopWpm)}";
                ValueAvg.Text = $"{Math.Round(summary.OverallAvgWpm)}";
                ValueLast.Text = $"{Math.Round(summary.LastSessionWpm)}";

                StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(16, 185, 129)); // Green
                StatusText.Text = "Live";
                StatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(16, 185, 129));

                AccuracyText.Text = $"{_typingEngine.CurrentAccuracy:F0}% Acc";
            }
            else
            {
                // Idle state: RETENTION of last active session & sustained metrics!
                double last15s = _typingEngine.LastFinished15sWpm > 0 ? _typingEngine.LastFinished15sWpm : summary.LastSession15sWpm;
                double last30s = _typingEngine.LastFinished30sWpm > 0 ? _typingEngine.LastFinished30sWpm : summary.LastSession30sWpm;
                double last60s = _typingEngine.LastFinished60sWpm > 0 ? _typingEngine.LastFinished60sWpm : summary.LastSession60sWpm;
                double lastAvg = _typingEngine.LastFinishedAvgWpm > 0 ? _typingEngine.LastFinishedAvgWpm : summary.LastSessionWpm;
                double last5m = _typingEngine.Current5mWpm > 0 ? _typingEngine.Current5mWpm : (_typingEngine.LastFinished5mWpm > 0 ? _typingEngine.LastFinished5mWpm : lastAvg);

                Value15s.Text = $"{Math.Round(last15s)}";
                Value30s.Text = $"{Math.Round(last30s)}";
                Value60s.Text = $"{Math.Round(last60s)}";
                Value5m.Text = $"{Math.Round(last5m)}";

                ValueTop.Text = $"{Math.Round(summary.AllTimeTopWpm)}";
                ValueAvg.Text = $"{Math.Round(summary.OverallAvgWpm)}";
                ValueLast.Text = $"{Math.Round(lastAvg)}";

                StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(56, 189, 248)); // Blue
                StatusText.Text = "Last";
                StatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(56, 189, 248));

                AccuracyText.Text = $"{_typingEngine.LastFinishedAccuracy:F0}% Acc";
            }

            if (_trayIcon != null)
            {
                _trayIcon.Text = $"WPM Overlay | Top: {Math.Round(summary.AllTimeTopWpm)} | Last: {Math.Round(summary.LastSessionWpm)}";
            }
        }

        private void OnTypingStarted()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.Invoke(() =>
            {
                if (_isExplicitlyHidden) return;

                // Animate Fade In
                var anim = new DoubleAnimation
                {
                    To = _settingsService.CurrentSettings.WindowOpacity,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(OpacityProperty, anim);
            });
        }

        private void OnTypingIdle()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.Invoke(() =>
            {
                if (_isExplicitlyHidden || _isHovered || _isSettingsOpen) return;

                if (_settingsService.CurrentSettings.AutoHideEnabled)
                {
                    // Animate Fade Out to 0
                    var anim = new DoubleAnimation
                    {
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(600),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };
                    BeginAnimation(OpacityProperty, anim);
                }
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                SavePosition();
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OverlayContextMenu.IsOpen = true;
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = true;
            // When hovered, ensure full visibility so user can read or interact
            if (!_isExplicitlyHidden)
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = _settingsService.CurrentSettings.WindowOpacity;
            }
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = false;
            if (!_typingEngine.IsTypingActive && _settingsService.CurrentSettings.AutoHideEnabled && !_isExplicitlyHidden)
            {
                OnTypingIdle();
            }
        }

        private void ToggleOverlayVisibility()
        {
            _isExplicitlyHidden = !_isExplicitlyHidden;
            if (_isExplicitlyHidden)
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = 0.0;
            }
            else
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = _settingsService.CurrentSettings.WindowOpacity;
            }
        }

        private void MenuHistory_Click(object sender, RoutedEventArgs e) => OpenHistoryWindow();
        private void MenuSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        private void MenuReset_Click(object sender, RoutedEventArgs e)
        {
            _typingEngine.ResetSession();
        }

        private void MenuTopmost_Click(object sender, RoutedEventArgs e)
        {
            bool isTop = MenuTopmost.IsChecked;
            Topmost = isTop;
            _settingsService.UpdateSettings(s => s.AlwaysOnTop = isTop);
        }

        private void MenuHide_Click(object sender, RoutedEventArgs e)
        {
            _isExplicitlyHidden = true;
            BeginAnimation(OpacityProperty, null);
            Opacity = 0.0;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void OpenHistoryWindow()
        {
            var historyWin = new HistoryWindow(_databaseService);
            historyWin.Owner = this;
            historyWin.ShowDialog();
            _typingEngine.RefreshStatsSummary();
            UpdateVisuals();
        }

        private void OpenSettingsWindow()
        {
            _isSettingsOpen = true;

            // Stop any ongoing fade-out and guarantee overlay is 100% visible on screen while configuring
            BeginAnimation(OpacityProperty, null);
            Opacity = _settingsService.CurrentSettings.WindowOpacity;

            var settingsWin = new SettingsWindow(_settingsService);
            settingsWin.Owner = this;
            settingsWin.ShowDialog();

            _isSettingsOpen = false;

            ApplySettings(_settingsService.CurrentSettings);

            if (!_typingEngine.IsTypingActive && _settingsService.CurrentSettings.AutoHideEnabled && !_isExplicitlyHidden)
            {
                OnTypingIdle();
            }
        }

        private void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
