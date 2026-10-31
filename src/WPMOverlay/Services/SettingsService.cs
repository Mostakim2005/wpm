using System;
using System.IO;
using System.Text.Json;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class SettingsService
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPMOverlay");

        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "config.json");

        public AppSettings CurrentSettings { get; private set; }

        public event Action<AppSettings>? SettingsUpdated;

        public SettingsService()
        {
            CurrentSettings = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        CurrentSettings = settings;
                        return CurrentSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            CurrentSettings = AppSettings.CreateDefault();
            Save();
            return CurrentSettings;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentSettings, options);
                File.WriteAllText(SettingsFilePath, json);

                SettingsUpdated?.Invoke(CurrentSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            updateAction(CurrentSettings);
            Save();
        }
    }
}
