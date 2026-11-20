using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace WPMOverlay.Services
{
    public static class StartupManager
    {
        private const string AppName = "WPMOverlay";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static string? GetCurrentExecutablePath()
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }
            return exePath;
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key == null) return false;

                if (key.GetValue(AppName) is not string storedValue || string.IsNullOrWhiteSpace(storedValue))
                {
                    return false;
                }

                string normalizedStored = storedValue.Trim().Trim('"');
                string? currentExe = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(currentExe))
                {
                    return true;
                }

                return string.Equals(normalizedStored, currentExe, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void SyncStartupPathIfEnabled(bool startWithWindows)
        {
            if (startWithWindows && !IsStartupEnabled())
            {
                SetStartup(true);
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = GetCurrentExecutablePath();
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup registry key: {ex.Message}");
            }
        }
    }
}
