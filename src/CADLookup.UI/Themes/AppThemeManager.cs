using System;
using System.Windows;
using System.Windows.Media;
using CADLookup.Infrastructure.Configuration;

namespace CADLookup.UI.Themes
{
    public static class AppThemeManager
    {
        private static readonly Uri LightSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
        private static readonly Uri DarkSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml");

        public static string CurrentTheme { get; private set; } = UserSettingsStore.LoadTheme();

        public static void ApplyTheme(FrameworkElement element, string theme)
        {
            if (element == null) return;
            var isDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
            CurrentTheme = isDark ? "Dark" : "Light";
            UserSettingsStore.SaveTheme(CurrentTheme);

            var resources = element.Resources;
            UpdateSkinDictionary(resources, isDark);
            ApplyBrushes(resources, isDark);
        }

        public static void ToggleTheme(FrameworkElement element)
        {
            ApplyTheme(element, CurrentTheme == "Dark" ? "Light" : "Dark");
        }

        private static void UpdateSkinDictionary(ResourceDictionary resources, bool isDark)
        {
            var targetUri = isDark ? DarkSkinUri : LightSkinUri;

            for (var i = 0; i < resources.MergedDictionaries.Count; i++)
            {
                var dict = resources.MergedDictionaries[i];
                if (dict.Source != null && (dict.Source.ToString().Contains("SkinDefault") || dict.Source.ToString().Contains("SkinDark")))
                {
                    resources.MergedDictionaries[i] = new ResourceDictionary { Source = targetUri };
                    return;
                }
            }

            resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = targetUri });
        }

        public static void ApplyBrushes(ResourceDictionary dict, bool isDark)
        {
            dict["AppBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x13, 0x16, 0x1E) : Color.FromRgb(0xF4, 0xF6, 0xFA));
            dict["CardBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1D, 0x22, 0x2D) : Color.FromRgb(0xFF, 0xFF, 0xFF));
            dict["CardBorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x2E, 0x37, 0x47) : Color.FromRgb(0xE5, 0xEA, 0xF1));
            dict["CardSubtleBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x23, 0x29, 0x36) : Color.FromRgb(0xFB, 0xFC, 0xFE));
            dict["CardSubtleBorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x31, 0x3B, 0x4D) : Color.FromRgb(0xED, 0xF0, 0xF5));
            dict["TextPrimaryBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0xF1, 0xF5, 0xF9) : Color.FromRgb(0x1F, 0x2D, 0x42));
            dict["TextSecondaryBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x94, 0xA3, 0xB8) : Color.FromRgb(0x72, 0x80, 0x96));
            dict["HeaderBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0F, 0x12, 0x1A) : Color.FromRgb(0x17, 0x24, 0x3A));
            dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x28, 0x64, 0xD7));
            dict["DataGridBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1D, 0x22, 0x2D) : Color.FromRgb(0xFF, 0xFF, 0xFF));
            dict["DataGridRowBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1D, 0x22, 0x2D) : Color.FromRgb(0xFF, 0xFF, 0xFF));
            dict["DataGridAltRowBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x18, 0x1D, 0x26) : Color.FromRgb(0xF8, 0xFA, 0xFD));
            dict["DataGridGridLinesBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x2B, 0x34, 0x44) : Color.FromRgb(0xEA, 0xEF, 0xF5));
            dict["TagBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x2A, 0x33, 0x46) : Color.FromRgb(0xEA, 0xF1, 0xFF));
            dict["TagTextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x60, 0xA5, 0xFA) : Color.FromRgb(0x27, 0x5A, 0xB5));
        }
    }
}
