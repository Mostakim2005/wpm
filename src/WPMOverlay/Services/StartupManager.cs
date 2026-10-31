using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WPMOverlay.Services
{
    public static class StartupManager
    {
        private const string AppName = "WPMOverlay";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key == null) return false;
                var value = key.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
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
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

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
