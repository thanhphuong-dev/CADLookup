using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CADLookup.Infrastructure.Configuration
{
    public static class UserSettingsStore
    {
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CADLookup", "settings.json");

        public static string LoadLanguage()
        {
            return ReadSettings().Language;
        }

        public static void SaveLanguage(string language)
        {
            var settings = ReadSettings();
            settings.Language = language == "en" ? "en" : "vi";
            WriteSettings(settings);
        }

        public static string LoadTheme()
        {
            return ReadSettings().Theme;
        }

        public static void SaveTheme(string theme)
        {
            var settings = ReadSettings();
            settings.Theme = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            WriteSettings(settings);
        }

        public static string LoadGroupBy()
        {
            return ReadSettings().GroupBy;
        }

        public static void SaveGroupBy(string groupBy)
        {
            var settings = ReadSettings();
            settings.GroupBy = groupBy;
            WriteSettings(settings);
        }

        private static SettingsData ReadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new SettingsData();
                using (var stream = File.OpenRead(SettingsPath))
                {
                    var data = (SettingsData?)new DataContractJsonSerializer(typeof(SettingsData)).ReadObject(stream);
                    if (data != null)
                    {
                        if (data.Language != "en" && data.Language != "vi") data.Language = "vi";
                        if (data.Theme != "Dark" && data.Theme != "Light") data.Theme = "Light";
                        if (string.IsNullOrEmpty(data.GroupBy)) data.GroupBy = "Type";
                        return data;
                    }
                }
            }
            catch { }
            return new SettingsData();
        }

        private static void WriteSettings(SettingsData data)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (directory != null) Directory.CreateDirectory(directory);
                using (var stream = File.Create(SettingsPath))
                {
                    data.SchemaVersion = 1;
                    new DataContractJsonSerializer(typeof(SettingsData)).WriteObject(stream, data);
                }
            }
            catch
            {
                // A settings write failure must not prevent the inspector from opening.
            }
        }

        [DataContract]
        private sealed class SettingsData
        {
            [DataMember(Order = 1)] public int SchemaVersion { get; set; } = 1;
            [DataMember(Order = 2)] public string Language { get; set; } = "vi";
            [DataMember(Order = 3)] public string Theme { get; set; } = "Light";
            [DataMember(Order = 4)] public string GroupBy { get; set; } = "Type";
        }
    }
}
