using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    public class RegridSettingsViewModel : ViewModelDialogBase
    {
        private readonly IRegridSettings _settings;

        // The parent (MainViewModel) will hook into this
        public Action Saved { get; set; }

        // Your custom RelayCommand implementation
        public RelayCommand SaveCommand { get; }

        public string UserName
        {
            get => _userName;
            set => Set(ref _userName, value);
        }
        private string _userName;

        public string Password
        {
            get => _password;
            set => Set(ref _password, value);
        }
        private string _password;

        public RegridSettingsViewModel(IRegridSettings settings)
        {
            _settings = settings;

            // 1. Initialize properties from the settings service
            _userName = _settings.RegridUserName;
            _password = _settings.RegridPassword;

            // 2. Initialize the command
            SaveCommand = new RelayCommand(ExecuteSave, CanSave);
        }

        private void ExecuteSave(object parameter)
        {
            // 3. Persist local ViewModel state back to the service
            _settings.RegridUserName = UserName;
            _settings.RegridPassword = Password;
            _settings.Save();

            // 2. Notify the parent that we are done
            Saved?.Invoke();
        }

        private bool CanSave(object parameter)
        {
            // Only enable Save if the data is valid AND different from what's in the settings
            bool isChanged = UserName != _settings.RegridUserName || Password != _settings.RegridPassword;
            return !string.IsNullOrWhiteSpace(UserName) && isChanged;
        }
    }
}
