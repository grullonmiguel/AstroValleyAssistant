using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Services;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;

        private ICommand? _changeThemeCommand;
        public ICommand ChangeThemeCommand => _changeThemeCommand ??= new RelayCommand(theme => SetNewTheme(theme as string));

        public SettingsViewModel(IThemeService themeService)
        {
            // Ensure a valid theme service is provided 
            ArgumentNullException.ThrowIfNull(themeService);

            // Store the non-null theme service for use in this view model.
            _themeService = themeService;
        }

        private void SetNewTheme(string? theme)
        {
            if (!string.IsNullOrEmpty(theme))
                _themeService.SetTheme(theme);
        }
    }
}