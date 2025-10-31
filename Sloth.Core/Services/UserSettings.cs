using System;
using System.IO;
using System.Text.Json;

namespace SlothApp.Services
{
    public class UserSettings
    {
        public string? ConfigPath { get; set; }
        public string? CustomersPath { get; set; }
        public string? DestRoot { get; set; }
        public string? SourceDir { get; set; }
        public string? LogPath { get; set; }

        private static string SettingsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sloth");

        private static string SettingsFile =>
            Path.Combine(SettingsDir, "settings.user.json");

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new UserSettings();
                }
            }
            catch { /* ignore */ }
            return new UserSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { /* ignore */ }
        }
    }
}