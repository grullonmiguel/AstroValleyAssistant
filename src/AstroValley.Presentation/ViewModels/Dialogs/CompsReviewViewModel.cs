using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>Comps on a map: each pin has an include switch. Completes with the keys of the excluded comps, or null when cancelled.</summary>
public partial class CompsReviewViewModel(string html, Func<Task> export) : DialogViewModelBase<string[]?>
{
    public override string Title => "Review comps";
    public string Html { get; } = html;

    [RelayCommand]
    private Task ExportHtml() => export();

    [RelayCommand]
    private void Cancel() => CompleteDialog(null);

    public void Finish(string[] excluded) => CompleteDialog(excluded);

    // closing with the X is a cancel, not an error
    public override void OnDialogClosing() => CompleteDialog(null);
}
