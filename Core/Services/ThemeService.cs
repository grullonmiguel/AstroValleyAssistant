using System.Diagnostics;
using System.Windows;

namespace AstroValleyAssistant.Core.Services
{
    public class ThemeService : IThemeService
    {
        private const string DefaultTheme = "Light-Purple";
        private readonly List<ResourceDictionary> _themeDictionaries = new();
        private string _currentTheme;

        public void Initialize()
        {
            // Load the saved theme from settings. Fall back to the default if not set.
            string savedTheme = Properties.Settings.Default.ThemeName;
            SetTheme(savedTheme);
        }

        public void SetTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                themeName = DefaultTheme;
            }

            try
            {
                // Parse the theme name, e.g., "Light-Purple" -> ["Light", "Purple"]
                var parts = themeName.Split('-');
                if (parts.Length != 2)
                {
                    // If the format is invalid, fall back to the default
                    if (_currentTheme != DefaultTheme) SetTheme(DefaultTheme);
                    return;
                }

                string paletteName = parts[0];
                string accentName = parts[1];

                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

                // 1. Remove the old theme dictionaries
                foreach (var themeDict in _themeDictionaries)
                {
                    mergedDictionaries.Remove(themeDict);
                }
                _themeDictionaries.Clear();

                // 2. Add the new theme dictionaries
                var paletteDict = new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{paletteName}.xaml", UriKind.Relative) };
                var accentDict = new ResourceDictionary { Source = new Uri($"/Themes/Accents/{accentName}.xaml", UriKind.Relative) };

                mergedDictionaries.Add(paletteDict);
                mergedDictionaries.Add(accentDict);

                // 3. Track the new dictionaries so we can remove them later
                _themeDictionaries.Add(paletteDict);
                _themeDictionaries.Add(accentDict);

                // 4. Save the new theme to settings
                Properties.Settings.Default.ThemeName = themeName;
                Properties.Settings.Default.Save();

                _currentTheme = themeName;
                Debug.WriteLine($"Theme changed to: {_currentTheme}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting theme '{themeName}': {ex.Message}");
                // Attempt to apply the default theme as a fallback
                if (_currentTheme != DefaultTheme) SetTheme(DefaultTheme);
            }
        }

        public string GetCurrentTheme() => _currentTheme;
    }
}
