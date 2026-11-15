using AstroValley.Presentation.Helpers;
using AstroValley.Presentation.Models;
using AstroValley.Presentation.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Pages;

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly IThemeService _theme;

    public SettingsViewModel(IThemeService theme)
    {
        _theme = theme;

        ColorOptions = AppDefaults.Palettes.Select(p => new ColorOption(p.Name, p.Primary)).ToList();

        foreach (var option in ColorOptions)
        {
            option.IsSelected = option.Color == theme.PrimaryColor;
        }
    }

    public override string Title => "Settings";

    public IReadOnlyList<ColorOption> ColorOptions { get; }

    [RelayCommand]
    private void SelectColor(ColorOption option)
    {
        foreach (var other in ColorOptions)
        {
            other.IsSelected = ReferenceEquals(other, option);
        }

        _theme.SetPrimaryColor(option.Color);
    }
}
