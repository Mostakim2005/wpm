using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WPMOverlay.Models;
using WPMOverlay.Services;
using Forms = System.Windows.Forms;

namespace WPMOverlay.Views
{
    public partial class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const double EngineSessionIdleSeconds = 5.0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly KeyboardHook _keyboardHook;
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private readonly TypingEngine _typingEngine;
        private readonly DispatcherTimer _autoHideGraceTimer;

        private Forms.NotifyIcon? _trayIcon;
        private Forms.ContextMenuStrip? _trayContextMenu;
        private System.Drawing.Icon? _trayCustomIcon;
        private string? _lastTrayText;

        private bool _isHovered = false;
        private bool _isExplicitlyHidden = false;
        private bool _isDialogOpen = false;
        private bool _isCleanedUp = false;
        private bool? _lastActiveVisualState;
        private IntPtr _hwnd = IntPtr.Zero;

        private HistoryWindow? _activeHistoryWindow;
        private SettingsWindow? _activeSettingsWindow;

        // Coalescing state for non-blocking UI metrics dispatch
        private readonly object _snapshotLock = new();
        private MetricsSnapshot _latestSnapshot;
        private int _metricsDispatchPending;

        public MainWindow() : this(null)
        {
        }

        public MainWindow(SettingsService? settingsService)
        {
            InitializeComponent();

            _keyboardHook = new KeyboardHook();
            _databaseService = new DatabaseService();
            _settingsService = settingsService ?? new SettingsService();
            _typingEngine = new TypingEngine(_keyboardHook, _databaseService, _settingsService);

            _autoHideGraceTimer = new DispatcherTimer();
            _autoHideGraceTimer.Tick += (s, e) =>
            {
                _autoHideGraceTimer.Stop();
                if (!_typingEngine.IsTypingActive && _settingsService.CurrentSettings.AutoHideEnabled && !_isHovered && !_isDialogOpen && !_isExplicitlyHidden)
                {
                    StartFadeOut();
                }
            };

            ApplySettings(_settingsService.CurrentSettings);

            _settingsService.SettingsUpdated += OnSettingsUpdated;
            _typingEngine.MetricsUpdated += OnMetricsUpdated;
            _typingEngine.TypingStarted += OnTypingStarted;
            _typingEngine.TypingIdle += OnTypingIdle;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            if (_hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
            }
        }

        private void SetClickThrough(bool clickThrough)
        {
            IsHitTestVisible = !clickThrough;

            if (_hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (clickThrough)
            {
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupTrayIcon();
            RestorePosition();
            _typingEngine.Start();

            // Initial visual update from immutable snapshot (no UI-thread SQLite query)
            UpdateVisuals(_typingEngine.GetCurrentSnapshot());
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            CleanupResources();
        }

        public void CleanupResources()
        {
            if (_isCleanedUp) return;
            _isCleanedUp = true;

            _autoHideGraceTimer.Stop();
            _settingsService.SettingsUpdated -= OnSettingsUpdated;
            _typingEngine.MetricsUpdated -= OnMetricsUpdated;
            _typingEngine.TypingStarted -= OnTypingStarted;
            _typingEngine.TypingIdle -= OnTypingIdle;

            _typingEngine.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.ContextMenuStrip = null;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_trayContextMenu != null)
            {
                _trayContextMenu.Dispose();
                _trayContextMenu = null;
            }

            if (_trayCustomIcon != null)
            {
                _trayCustomIcon.Dispose();
                _trayCustomIcon = null;
            }
        }

        private void SetupTrayIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/WPMOverlay;component/app_icon.ico");
                var streamInfo = WpfApplication.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (streamInfo.Stream)
                    {
                        _trayCustomIcon = new System.Drawing.Icon(streamInfo.Stream);
                    }
                }
            }
            catch
            {
                _trayCustomIcon = null;
            }

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = _trayCustomIcon ?? System.Drawing.SystemIcons.Application,
                Text = "WPM Overlay - Typing Speed Tracker",
                Visible = true
            };
            _lastTrayText = _trayIcon.Text;

            _trayContextMenu = new Forms.ContextMenuStrip();
            _trayContextMenu.Items.Add("Show / Hide Overlay", null, (s, e) => Dispatcher.BeginInvoke(new Action(ToggleOverlayVisibility)));
            _trayContextMenu.Items.Add("History & Analytics", null, (s, e) => Dispatcher.BeginInvoke(new Action(OpenHistoryWindow)));
            _trayContextMenu.Items.Add("Settings", null, (s, e) => Dispatcher.BeginInvoke(new Action(OpenSettingsWindow)));
            _trayContextMenu.Items.Add("-");
            _trayContextMenu.Items.Add("Exit", null, (s, e) => Dispatcher.BeginInvoke(new Action(ExitApplication)));

            _trayIcon.ContextMenuStrip = _trayContextMenu;
            _trayIcon.DoubleClick += (s, e) => Dispatcher.BeginInvoke(new Action(ToggleOverlayVisibility));
        }

        private void RestorePosition()
        {
            var settings = _settingsService.CurrentSettings;
            double width = ActualWidth > 0 ? ActualWidth : 260;
            double height = ActualHeight > 0 ? ActualHeight : 120;

            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (settings.PositionX.HasValue && settings.PositionY.HasValue)
            {
                double x = settings.PositionX.Value;
                double y = settings.PositionY.Value;

                // Clamp inside current virtual screen bounds (supports negative multi-monitor coordinates
                // and recovers cleanly if a previously used monitor was disconnected)
                double maxLeft = Math.Max(virtualLeft, virtualRight - width);
                double maxTop = Math.Max(virtualTop, virtualBottom - height);

                Left = Math.Clamp(x, virtualLeft, maxLeft);
                Top = Math.Clamp(y, virtualTop, maxTop);
            }
            else
            {
                // Default position: Top Right corner of the primary WorkArea (respects taskbar)
                var workArea = SystemParameters.WorkArea;
                Left = Math.Max(workArea.Left, workArea.Right - width - 40);
                Top = workArea.Top + 40;
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
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() => ApplySettings(settings)));
        }

        private void ApplySettings(AppSettings s)
        {
            // Apply dynamic design tokens to all open windows
            ThemeManager.ApplyTheme(s);

            // Apply scale
            HudScaleTransform.ScaleX = s.ScaleFactor;
            HudScaleTransform.ScaleY = s.ScaleFactor;

            // Metric visibility
            Row15s.Visibility = s.Show15s ? Visibility.Visible : Visibility.Collapsed;
            Row30s.Visibility = s.Show30s ? Visibility.Visible : Visibility.Collapsed;
            Row60s.Visibility = s.Show60s ? Visibility.Visible : Visibility.Collapsed;
            Row5m.Visibility = s.Show5m ? Visibility.Visible : Visibility.Collapsed;

            RowTop.Visibility = s.ShowTop ? Visibility.Visible : Visibility.Collapsed;
            RowAvg.Visibility = s.ShowAvg ? Visibility.Visible : Visibility.Collapsed;
            RowLast.Visibility = s.ShowLast ? Visibility.Visible : Visibility.Collapsed;

            // Dynamically collapse empty columns and divider when all rows in a column are hidden
            bool hasLeft = s.Show15s || s.Show30s || s.Show60s || s.Show5m;
            bool hasRight = s.ShowTop || s.ShowAvg || s.ShowLast;

            LeftMetricsColumn.Visibility = hasLeft ? Visibility.Visible : Visibility.Collapsed;
            LeftColDef.MinWidth = hasLeft ? 110 : 0;
            LeftMetricsColumn.Margin = hasLeft && hasRight ? new Thickness(0, 0, 10, 0) : new Thickness(0);

            RightMetricsColumn.Visibility = hasRight ? Visibility.Visible : Visibility.Collapsed;
            RightColDef.MinWidth = hasRight ? 100 : 0;
            RightMetricsColumn.Margin = hasLeft && hasRight ? new Thickness(10, 0, 0, 0) : new Thickness(0);

            bool showDivider = hasLeft && hasRight;
            VerticalDivider.Visibility = showDivider ? Visibility.Visible : Visibility.Collapsed;
            DividerColDef.Width = showDivider ? new GridLength(1) : new GridLength(0);

            Topmost = s.AlwaysOnTop;
            MenuTopmost.IsChecked = s.AlwaysOnTop;

            if (!_isExplicitlyHidden)
            {
                // Clear any HoldEnd Opacity animation clock before assigning local Opacity
                BeginAnimation(OpacityProperty, null);
                Opacity = s.WindowOpacity;
                SetClickThrough(false);
            }
        }

        private void OnMetricsUpdated(MetricsSnapshot snapshot)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

            lock (_snapshotLock)
            {
                _latestSnapshot = snapshot;
            }

            if (Interlocked.Exchange(ref _metricsDispatchPending, 1) == 0)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Interlocked.Exchange(ref _metricsDispatchPending, 0);
                    MetricsSnapshot current;
                    lock (_snapshotLock)
                    {
                        current = _latestSnapshot;
                    }
                    UpdateVisuals(current);
                }), DispatcherPriority.Background);
            }
        }

        private void UpdateVisuals(MetricsSnapshot snapshot)
        {
            var summary = snapshot.Summary ?? new StatsSummary();
            bool active = snapshot.IsTypingActive;

            if (active)
            {
                // Live typing active metrics
                Value15s.Text = $"{Math.Round(snapshot.Current15sWpm)}";
                Value30s.Text = $"{Math.Round(snapshot.Current30sWpm)}";
                Value60s.Text = $"{Math.Round(snapshot.Current60sWpm)}";
                Value5m.Text = $"{Math.Round(snapshot.Current5mWpm)}";

                ValueTop.Text = $"{Math.Round(summary.AllTimeTopWpm)}";
                ValueAvg.Text = $"{Math.Round(summary.OverallAvgWpm)}";
                ValueLast.Text = $"{Math.Round(summary.LastSessionWpm)}";

                AccuracyText.Text = $"{snapshot.CurrentAccuracy:F0}% Acc";
            }
            else
            {
                // Idle state: RETENTION of last active session & sustained metrics!
                double last15s = snapshot.LastFinished15sWpm > 0 ? snapshot.LastFinished15sWpm : summary.LastSession15sWpm;
                double last30s = snapshot.LastFinished30sWpm > 0 ? snapshot.LastFinished30sWpm : summary.LastSession30sWpm;
                double last60s = snapshot.LastFinished60sWpm > 0 ? snapshot.LastFinished60sWpm : summary.LastSession60sWpm;
                double lastAvg = snapshot.LastFinishedAvgWpm > 0 ? snapshot.LastFinishedAvgWpm : summary.LastSessionWpm;
                double last5m = snapshot.Current5mWpm > 0 ? snapshot.Current5mWpm : (snapshot.LastFinished5mWpm > 0 ? snapshot.LastFinished5mWpm : lastAvg);

                Value15s.Text = $"{Math.Round(last15s)}";
                Value30s.Text = $"{Math.Round(last30s)}";
                Value60s.Text = $"{Math.Round(last60s)}";
                Value5m.Text = $"{Math.Round(last5m)}";

                ValueTop.Text = $"{Math.Round(summary.AllTimeTopWpm)}";
                ValueAvg.Text = $"{Math.Round(summary.OverallAvgWpm)}";
                ValueLast.Text = $"{Math.Round(lastAvg)}";

                AccuracyText.Text = $"{snapshot.LastFinishedAccuracy:F0}% Acc";
            }

            // Update status pill via DynamicResource tokens only on state transition (Trap 2, zero per-tick allocations)
            if (_lastActiveVisualState != active)
            {
                _lastActiveVisualState = active;
                string token = active ? "AppHighlightBrush" : "AppAccentBrush";
                StatusDot.SetResourceReference(Shape.FillProperty, token);
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, token);
                StatusText.Text = active ? "Live" : "Last";
            }

            if (_trayIcon != null)
            {
                string trayText = $"WPM Overlay | Top: {Math.Round(summary.AllTimeTopWpm)} | Last: {Math.Round(summary.LastSessionWpm)}";
                if (trayText.Length > 63)
                {
                    trayText = trayText[..63];
                }

                if (!string.Equals(_lastTrayText, trayText, StringComparison.Ordinal))
                {
                    _lastTrayText = trayText;
                    _trayIcon.Text = trayText;
                }
            }
        }

        private void StartFadeIn()
        {
            if (_isExplicitlyHidden) return;
            SetClickThrough(false);

            var anim = new DoubleAnimation
            {
                To = _settingsService.CurrentSettings.WindowOpacity,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void StartFadeOut()
        {
            if (_isExplicitlyHidden || _isHovered || _isDialogOpen) return;
            if (_settingsService.CurrentSettings.AutoHideEnabled)
            {
                var anim = new DoubleAnimation
                {
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                anim.Completed += (s, e) =>
                {
                    if (!_typingEngine.IsTypingActive && !_isHovered && !_isDialogOpen && !_isExplicitlyHidden && Opacity < 0.05)
                    {
                        SetClickThrough(true);
                    }
                };
                BeginAnimation(OpacityProperty, anim);
            }
        }

        private void TriggerAutoHideGracePeriod(double? overrideDelaySeconds = null)
        {
            if (_isHovered || _isDialogOpen || _isExplicitlyHidden || !_settingsService.CurrentSettings.AutoHideEnabled) return;

            if (!_typingEngine.IsTypingActive)
            {
                double delaySec = overrideDelaySeconds ?? _settingsService.CurrentSettings.AutoHideDelaySeconds;
                if (delaySec <= 0)
                {
                    StartFadeOut();
                    return;
                }

                _autoHideGraceTimer.Stop();
                _autoHideGraceTimer.Interval = TimeSpan.FromSeconds(delaySec);
                _autoHideGraceTimer.Start();
            }
        }

        private void OnTypingStarted()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _autoHideGraceTimer.Stop();
                StartFadeIn();
            }), DispatcherPriority.Normal);
        }

        private void OnTypingIdle()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                double configuredDelay = _settingsService.CurrentSettings.AutoHideDelaySeconds;
                double remainingDelay = configuredDelay - EngineSessionIdleSeconds;
                TriggerAutoHideGracePeriod(remainingDelay);
            }), DispatcherPriority.Background);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                    SavePosition();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OverlayContextMenu.IsOpen = true;
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = true;
            _autoHideGraceTimer.Stop();
            if (!_isExplicitlyHidden)
            {
                SetClickThrough(false);
                BeginAnimation(OpacityProperty, null);
                Opacity = _settingsService.CurrentSettings.WindowOpacity;
            }
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = false;
            TriggerAutoHideGracePeriod();
        }

        private void ToggleOverlayVisibility()
        {
            _isExplicitlyHidden = !_isExplicitlyHidden;
            if (_isExplicitlyHidden)
            {
                _autoHideGraceTimer.Stop();
                BeginAnimation(OpacityProperty, null);
                Hide();
            }
            else
            {
                SetClickThrough(false);
                BeginAnimation(OpacityProperty, null);
                Opacity = _settingsService.CurrentSettings.WindowOpacity;
                Show();
                TriggerAutoHideGracePeriod();
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
            _autoHideGraceTimer.Stop();
            BeginAnimation(OpacityProperty, null);
            Hide();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void OpenHistoryWindow()
        {
            if (_activeHistoryWindow != null)
            {
                _activeHistoryWindow.Activate();
                return;
            }

            try
            {
                _isDialogOpen = true;
                _autoHideGraceTimer.Stop();

                if (!_isExplicitlyHidden)
                {
                    SetClickThrough(false);
                    BeginAnimation(OpacityProperty, null);
                    Opacity = _settingsService.CurrentSettings.WindowOpacity;
                }

                var historyWin = new HistoryWindow(_databaseService, _typingEngine);
                historyWin.Owner = IsVisible ? this : null;
                _activeHistoryWindow = historyWin;
                historyWin.ShowDialog();

                UpdateVisuals(_typingEngine.GetCurrentSnapshot());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening history window: {ex}");
                WpfMessageBox.Show($"Failed to open History: {ex.Message}", "History Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _activeHistoryWindow = null;
                _isDialogOpen = false;
                TriggerAutoHideGracePeriod();
            }
        }

        private void OpenSettingsWindow()
        {
            if (_activeSettingsWindow != null)
            {
                _activeSettingsWindow.Activate();
                return;
            }

            try
            {
                _isDialogOpen = true;
                _autoHideGraceTimer.Stop();

                // Stop any ongoing fade-out and guarantee overlay is visible while configuring
                if (!_isExplicitlyHidden)
                {
                    SetClickThrough(false);
                    BeginAnimation(OpacityProperty, null);
                    Opacity = _settingsService.CurrentSettings.WindowOpacity;
                }

                var settingsWin = new SettingsWindow(_settingsService);
                settingsWin.Owner = IsVisible ? this : null;
                settingsWin.PreviewSettingsRequested += ApplySettings;
                _activeSettingsWindow = settingsWin;

                try
                {
                    settingsWin.ShowDialog();
                }
                finally
                {
                    settingsWin.PreviewSettingsRequested -= ApplySettings;
                }

                ApplySettings(_settingsService.CurrentSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening settings window: {ex}");
                WpfMessageBox.Show($"Failed to open Settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _activeSettingsWindow = null;
                _isDialogOpen = false;
                TriggerAutoHideGracePeriod();
            }
        }

        private void ExitApplication()
        {
            WpfApplication.Current.Shutdown();
        }
    }
}
