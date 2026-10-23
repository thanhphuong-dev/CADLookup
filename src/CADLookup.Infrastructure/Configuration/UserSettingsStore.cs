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
            try
            {
                if (!File.Exists(SettingsPath)) return "vi";
                using (var stream = File.OpenRead(SettingsPath))
                {
                    var data = (SettingsData?)new DataContractJsonSerializer(typeof(SettingsData)).ReadObject(stream);
                    return data?.SchemaVersion == 1 && (data.Language == "vi" || data.Language == "en") ? data.Language : "vi";
                }
            }
            catch { return "vi"; }
        }

        public static void SaveLanguage(string language)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (directory != null) Directory.CreateDirectory(directory);
                using (var stream = File.Create(SettingsPath))
                    new DataContractJsonSerializer(typeof(SettingsData)).WriteObject(stream,
                        new SettingsData { SchemaVersion = 1, Language = language == "en" ? "en" : "vi" });
            }
            catch
            {
                // A settings write failure must not prevent the inspector from opening.
            }
        }

        [DataContract]
        private sealed class SettingsData
        {
            [DataMember(Order = 1)] public int SchemaVersion { get; set; }
            [DataMember(Order = 2)] public string Language { get; set; } = "vi";
        }
    }
}
