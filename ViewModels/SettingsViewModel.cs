using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;

        public ICommand ChangeThemeCommand { get; }

        public SettingsViewModel(IThemeService themeService)
        {
            _themeService = themeService;
            ChangeThemeCommand = new RelayCommand(p => SetNewTheme(p as string));
        }

        private void SetNewTheme(string themeName)
        {
            // Example: "Dark-Teal"
            if (!string.IsNullOrEmpty(themeName))
            {
                _themeService.SetTheme(themeName);
            }
        }
    }
}
