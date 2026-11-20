using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WPMOverlay.Services;
using WPMOverlay.Views;

namespace WPMOverlay
{
    public partial class App : WpfApplication
    {
        private const string SingleInstanceMutexName = @"Local\WPMOverlay_SingleInstance_Mutex";
        private const long MaxLogBytes = 512 * 1024; // 512 KB cap

        private Mutex? _singleInstanceMutex;
        private bool _ownsMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsMutex);
            if (!_ownsMutex)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown(0);
                return;
            }

            RegisterGlobalExceptionHandlers();

            var settingsService = new SettingsService();
            StartupManager.SyncStartupPathIfEnabled(settingsService.CurrentSettings.StartWithWindows);
            ThemeManager.ApplyTheme(settingsService.CurrentSettings);

            var mainWindow = new MainWindow(settingsService);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                if (_ownsMutex)
                {
                    try
                    {
                        _singleInstanceMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                    }
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("DispatcherUnhandledException", e.Exception);
            CleanupOnCrash();
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("AppDomainUnhandledException", ex);
            }
            CleanupOnCrash();
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private void CleanupOnCrash()
        {
            try
            {
                if (MainWindow is MainWindow mainWin)
                {
                    mainWin.CleanupResources();
                }
            }
            catch
            {
            }
        }

        private static void LogException(string source, Exception ex)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WPMOverlay");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string logPath = Path.Combine(folder, "error.log");
                if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogBytes)
                {
                    string archivePath = logPath + ".old";
                    File.Move(logPath, archivePath, overwrite: true);
                }

                string entry = $"[{DateTime.UtcNow:O}] [{source}] {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
            }
        }
    }
}
