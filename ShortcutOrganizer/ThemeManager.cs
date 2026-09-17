using System;
using System.Linq;

namespace ShortcutOrganizer
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = false;

        public static void ApplyTheme(bool dark)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var newDict = new System.Windows.ResourceDictionary
            {
                Source = new Uri(
                    dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
                    UriKind.Relative)
            };

            var old = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme") ||
                     d.Source.OriginalString.Contains("DarkTheme")));

            if (old != null) app.Resources.MergedDictionaries.Remove(old);
            app.Resources.MergedDictionaries.Add(newDict);

            IsDark = dark;
        }

        public static void Toggle() => ApplyTheme(!IsDark);
    }
}