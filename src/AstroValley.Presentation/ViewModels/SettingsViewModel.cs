using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public SettingsViewModel(IThemeService themeService)
    {
        // Ensure a valid theme service is provided 
        ArgumentNullException.ThrowIfNull(themeService);

        // Store the non-null theme service for use in this view model.
        _themeService = themeService;
    }

    [RelayCommand]
    private void ChangeTheme(string? theme)
    {
        if (!string.IsNullOrEmpty(theme))
            _themeService.SetTheme(theme);
    }
}