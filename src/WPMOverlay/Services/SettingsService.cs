using System;
using System.IO;
using System.Text.Json;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static readonly string DefaultSettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPMOverlay");

        private static readonly string DefaultSettingsFilePath = Path.Combine(DefaultSettingsFolder, "config.json");

        private readonly string _settingsFilePath;

        public AppSettings CurrentSettings { get; private set; }

        public event Action<AppSettings>? SettingsUpdated;

        public SettingsService(string? settingsFilePath = null)
        {
            _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
                ? DefaultSettingsFilePath
                : settingsFilePath;

            CurrentSettings = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        settings.Sanitize();
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
                CurrentSettings.Sanitize();

                string? folder = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
                string tempPath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsFilePath, overwrite: true);

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
