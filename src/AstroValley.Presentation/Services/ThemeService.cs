using AstroValley.Presentation.Helpers;
using AstroValley.Presentation.Services.Interfaces;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using System.Windows.Media;

namespace AstroValley.Presentation.Services
{
    public sealed class ThemeService : IThemeService
    {
        private readonly PaletteHelper _paletteHelper = new();
        private readonly ISettingsService _settings;
        private readonly ILogger<ThemeService> _logger;

        public ThemeService(ISettingsService settings, ILogger<ThemeService> logger)
        {
            _settings = settings;
            _logger = logger;
            PrimaryColor = ParseOrDefault(settings.Current.PrimaryColor);
        }

        public Color PrimaryColor { get; private set; }

        public void Initialize() => Apply(PrimaryColor);

        public void SetPrimaryColor(Color color)
        {
            Apply(color);
            _settings.Current.PrimaryColor = color.ToString();
            _settings.Save();
            _logger.LogInformation("Primary color changed to {Color}", color);
        }

        private void Apply(Color color)
        {
            PrimaryColor = color;

            // Material Design derives the light/dark shades and the readable foreground from this one color.
            var theme = _paletteHelper.GetTheme();
            theme.SetPrimaryColor(color);
            theme.SetSecondaryColor(color);
            _paletteHelper.SetTheme(theme);

            // shortcut: a primary not in AppDefaults.Palettes gets the first palette's companions, derive them if custom colors are ever allowed.
            var palette = AppDefaults.Palettes.FirstOrDefault(p => ParseOrDefault(p.Primary) == color, AppDefaults.Palettes[0]);
            var resources = System.Windows.Application.Current.Resources;
            resources["AppBackgroundBrush"] = FrozenBrush(palette.Background);
            resources["SecondaryEmphasisBrush"] = FrozenBrush(palette.SecondaryEmphasis);
        }

        private static SolidColorBrush FrozenBrush(string hex)
        {
            var brush = new SolidColorBrush(ParseOrDefault(hex));
            brush.Freeze();
            return brush;
        }

        private static Color ParseOrDefault(string? hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex ?? AppDefaults.PrimaryColorHex);
            }
            catch (FormatException)
            {
                return (Color)ColorConverter.ConvertFromString(AppDefaults.PrimaryColorHex);
            }
        }
    }
}
