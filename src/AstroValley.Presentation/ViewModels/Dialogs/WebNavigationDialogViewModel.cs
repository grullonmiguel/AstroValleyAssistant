using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class WebNavigationDialogViewModel : DialogViewModelBase<string?>
{
    public override string Title => "Real Auction";

    public Action? OnGoBackRequested { get; set; }

    public string InitialUrl { get; }

    // Matches realtaxdeed.com or realforeclose.com auction preview URLs with an AUCTIONDATE param
    private static readonly Regex RealAuctionUrlPattern = new(
        @"^https?://[^.]+\.real(taxdeed|foreclose)\.com/.*[?&]zaction=AUCTION.*AUCTIONDATE=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string CurrentUrl
    {
        get => _currentUrl;
        set
        {
            if (SetProperty(ref _currentUrl, value))
            {
                IsValidAuctionUrl = RealAuctionUrlPattern.IsMatch(value);
                SelectUrlCommand.NotifyCanExecuteChanged();
            }
        }
    }
    private string _currentUrl;

    public bool CanGoBack
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public bool IsValidAuctionUrl
    {
        get => field;
        private set => SetProperty(ref field, value);
    }

    public WebNavigationDialogViewModel(string initialUrl)
    {
        InitialUrl = initialUrl;
        _currentUrl = initialUrl;
        IsValidAuctionUrl = RealAuctionUrlPattern.IsMatch(initialUrl);
    }

    [RelayCommand(CanExecute = nameof(IsValidAuctionUrl))]
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
        OnGoBackRequested?.Invoke();
    }

    private bool CanExecuteGoBack() => CanGoBack;

    public override void OnDialogClosing()
    {
        CompleteDialog(null);
    }
}
