using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class WebNavigationDialogViewModel : DialogViewModelBase<string?>
{
    public override string Title => "Real Auction";

    public Action? OnGoBackRequested { get; set; }

    public string InitialUrl { get; }

    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetProperty(ref _currentUrl, value);
    }
    private string _currentUrl;


    public bool CanGoBack
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public WebNavigationDialogViewModel(string initialUrl)
    {
        InitialUrl = initialUrl;
        _currentUrl = initialUrl;
    }

    [RelayCommand]
    private void SelectUrl()
    {
        CompleteDialog(CurrentUrl);
    }

    [RelayCommand]
    private void Cancel()
    {
        CompleteDialog(null);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGoBack))]
    private void GoBack()
    {
        // Will be invoked via a public method from the View
        OnGoBackRequested?.Invoke();
    }

    private bool CanExecuteGoBack() => CanGoBack;

    /// <summary>
    /// Lifecycle hook called when the dialog is being closed via X button.
    /// Complete with null instead of canceling to avoid TaskCanceledException.
    /// </summary>
    public override void OnDialogClosing()
    {
        // Don't call base - we want to complete with null, not cancel
        CompleteDialog(null);
    }
}
