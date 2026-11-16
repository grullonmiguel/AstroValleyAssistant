using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// A browser in a dialog: the user browses a site to the page they want and presses Select, which returns that page's URL
/// (null when cancelled). The caller decides which pages count (<paramref name="isAcceptable"/>) and what to tell the user (<paramref name="hint"/>),
/// so every list source that starts from a website (RealAuction today) can reuse it. Show it with IDialogService.ShowAsync.
/// </summary>
public sealed partial class BrowseForUrlDialogViewModel(string title, string startUrl, Func<string, bool> isAcceptable, string hint)
    : DialogViewModelBase<string?>
{
    public override string Title => title;

    public string StartUrl => startUrl;

    /// <summary>Shown until the browser is on a page that can be selected.</summary>
    public string Hint => hint;

    /// <summary>The page the browser is on (the view updates it after each navigation).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAcceptable))]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    private string currentUrl = startUrl;

    public bool IsAcceptable => isAcceptable(CurrentUrl);

    [RelayCommand(CanExecute = nameof(IsAcceptable))]
    private void Select() => CompleteDialog(CurrentUrl);

    [RelayCommand]
    private void Cancel() => CompleteDialog(null);
}
