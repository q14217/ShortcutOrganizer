using System;
using System.IO;
using System.Text.Json;

namespace ShortcutOrganizer
{
    public class AppSettings
    {
        public string HotKeyModifiers { get; set; } = "Ctrl+Alt";
        public string HotKeyKey { get; set; } = "S";
        public string SortMode { get; set; } = "Default";
        public string ViewMode { get; set; } = "Card";
        public bool AutoBackupOnStartup { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowTrayBalloon { get; set; } = true;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShortcutOrganizer", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new AppSettings();
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "加载设置失败");
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string json = JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存设置失败");
            }
        }
    }
}