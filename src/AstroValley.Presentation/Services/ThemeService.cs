using System.Diagnostics;
using System.Windows;

namespace AstroValley.Presentation.Services;

public class ThemeService : IThemeService
{
    private const string DefaultTheme = "Light-Purple";
    private readonly List<ResourceDictionary> _themeDictionaries = new();
    private readonly SettingsService _settings;
    private string _currentTheme = DefaultTheme;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize()
    {
        string savedTheme = _settings.ThemeName;
        SetTheme(savedTheme);
    }

    public void SetTheme(string themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            themeName = DefaultTheme;

        try
        {
            var parts = themeName.Split('-');
            if (parts.Length != 2)
            {
                if (_currentTheme != DefaultTheme) SetTheme(DefaultTheme);
                return;
            }

            string paletteName = parts[0];
            string accentName = parts[1];

            var mergedDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

            foreach (var themeDict in _themeDictionaries)
                mergedDictionaries.Remove(themeDict);
            _themeDictionaries.Clear();

            var paletteDict = new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{paletteName}.xaml", UriKind.Relative) };
            var accentDict = new ResourceDictionary { Source = new Uri($"/Themes/Accents/{accentName}.xaml", UriKind.Relative) };

            mergedDictionaries.Add(paletteDict);
            mergedDictionaries.Add(accentDict);

            _themeDictionaries.Add(paletteDict);
            _themeDictionaries.Add(accentDict);

            _settings.ThemeName = themeName;
            _settings.Save();

            _currentTheme = themeName;
            Debug.WriteLine($"Theme changed to: {_currentTheme}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting theme '{themeName}': {ex.Message}");
            if (_currentTheme != DefaultTheme) SetTheme(DefaultTheme);
        }
    }

    public string GetCurrentTheme() => _currentTheme;
}
