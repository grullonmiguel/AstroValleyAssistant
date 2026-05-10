using AstroValley.Domain.Entities;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class ResolveMatchesViewModel : DialogViewModelBase<RegridMatch?>
{
    public override string Title => "Resolve Multiple Matches";

    // Original property information for comparison
    public string OriginalParcelId { get; }
    public string OriginalAddress { get; }
    public string OriginalOwner { get; }

    public ObservableCollection<RegridMatch> Matches { get; }

    public ResolveMatchesViewModel(
        string originalParcelId,
        string originalAddress,
        string originalOwner,
        IEnumerable<RegridMatch> matches)
    {
        // Store original property info
        OriginalParcelId = originalParcelId;
        OriginalAddress = originalAddress;
        OriginalOwner = originalOwner;

        Matches = new ObservableCollection<RegridMatch>(matches);
    }

    [RelayCommand]
    private void SelectMatch(RegridMatch match)
    {
        CompleteDialog(match);
    }

    [RelayCommand]
    private void Cancel()
    {
        CompleteDialog(null);
    }

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
