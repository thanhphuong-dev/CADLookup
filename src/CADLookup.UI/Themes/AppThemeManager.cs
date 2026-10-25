using System;
using System.IO;
using System.Reflection;
using System.Windows;
using CADLookup.Infrastructure.Configuration;

namespace CADLookup.UI.Themes
{
    /// <summary>Swaps HandyControl's light/dark color dictionaries; all brushes come from HandyControl itself.</summary>
    public static class AppThemeManager
    {
        private static readonly Uri LightSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
        private static readonly Uri DarkSkinUri = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml");

        public static string CurrentTheme { get; private set; } = UserSettingsStore.LoadTheme();

        public static void EnsureHandyControlLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, "HandyControl", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            var assemblyDirectory = Path.GetDirectoryName(typeof(AppThemeManager).Assembly.Location);
            var handyControlPath = string.IsNullOrEmpty(assemblyDirectory)
                ? string.Empty
                : Path.Combine(assemblyDirectory, "HandyControl.dll");

            if (!string.IsNullOrEmpty(handyControlPath) && File.Exists(handyControlPath))
                Assembly.LoadFrom(handyControlPath);
            else
                Assembly.Load(new AssemblyName("HandyControl"));
        }

        public static void ApplyTheme(FrameworkElement element, string theme)
        {
            if (element == null) return;
            var isDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
            CurrentTheme = isDark ? "Dark" : "Light";
            UserSettingsStore.SaveTheme(CurrentTheme);
            UpdateSkinDictionary(element.Resources, isDark);
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
    }
}
