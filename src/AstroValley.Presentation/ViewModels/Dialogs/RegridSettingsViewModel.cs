using AstroValley.Application.Interfaces.Settings;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class RegridSettingsViewModel : DialogViewModelBase<bool>
{
    public override string Title => "Regrid Settings";

    private readonly IRegridSettings? _settings;

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
        CompleteDialog(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CompleteDialog(false); // Signals cancellation
    }

    public override void OnDialogClosing()
    {
        base.OnDialogClosing(); // Cancels task if not completed
    }

    private bool CanSave(object? parameter)
    {
        // Only enable Save if the data is valid AND different from what's in the settings
        bool isChanged = UserName != _settings!.RegridUserName || Password != _settings.RegridPassword;
        return !string.IsNullOrWhiteSpace(UserName) && isChanged;
    }
}
