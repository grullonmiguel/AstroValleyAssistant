using AstroValley.Application.Interfaces.Settings;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class RegridSettingsViewModel : DialogViewModelBase
{
    private readonly IRegridSettings? _settings;

    public Action? Saved { get; set; }

    public string UserName
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveCommand.NotifyCanExecuteChanged();
        }
    }

    public string Password
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveCommand.NotifyCanExecuteChanged();
        }
    }

    public RegridSettingsViewModel(IRegridSettings settings)
    {
        _settings = settings;

        // 1. Initialize properties from the settings service
        UserName = _settings.RegridUserName;
        Password = _settings.RegridPassword;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save(object? parameter)
    {
        // 3. Persist local ViewModel state back to the service
        _settings!.RegridUserName = UserName;
        _settings!.RegridPassword = Password;
        _settings!.Save();

        // 2. Notify the parent that we are done
        Saved?.Invoke();
    }

    private bool CanSave(object? parameter)
    {
        // Only enable Save if the data is valid AND different from what's in the settings
        bool isChanged = UserName != _settings!.RegridUserName || Password != _settings.RegridPassword;
        return !string.IsNullOrWhiteSpace(UserName) && isChanged;
    }
}
