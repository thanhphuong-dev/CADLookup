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

        public static bool LoadPropertyGrouping()
        {
            return ReadSettings().GroupProperties;
        }

        public static void SavePropertyGrouping(bool grouped)
        {
            var settings = ReadSettings();
            settings.GroupProperties = grouped;
            WriteSettings(settings);
        }

        /// <summary>Returns null when no valid window bounds were stored yet.</summary>
        public static WindowBounds? LoadWindowBounds()
        {
            var data = ReadSettings().Window;
            if (data == null || data.Width < 640 || data.Height < 480) return null;
            return new WindowBounds(data.Left, data.Top, data.Width, data.Height, data.Maximized);
        }

        public static void SaveWindowBounds(double left, double top, double width, double height, bool maximized)
        {
            if (double.IsNaN(left) || double.IsNaN(top) || width < 640 || height < 480) return;
            var settings = ReadSettings();
            settings.Window = new WindowBoundsData { Left = left, Top = top, Width = width, Height = height, Maximized = maximized };
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
            [DataMember(Order = 5, EmitDefaultValue = false)] public bool GroupProperties { get; set; } = true;
            [DataMember(Order = 6, EmitDefaultValue = false)] public WindowBoundsData? Window { get; set; }
        }

        [DataContract]
        private sealed class WindowBoundsData
        {
            [DataMember(Order = 1)] public double Left { get; set; }
            [DataMember(Order = 2)] public double Top { get; set; }
            [DataMember(Order = 3)] public double Width { get; set; } = 1360;
            [DataMember(Order = 4)] public double Height { get; set; } = 860;
            [DataMember(Order = 5)] public bool Maximized { get; set; }
        }
    }

    public sealed class WindowBounds
    {
        public WindowBounds(double left, double top, double width, double height, bool maximized)
        {
            Left = left; Top = top; Width = width; Height = height; Maximized = maximized;
        }

        public double Left { get; }
        public double Top { get; }
        public double Width { get; }
        public double Height { get; }
        public bool Maximized { get; }
    }
}
