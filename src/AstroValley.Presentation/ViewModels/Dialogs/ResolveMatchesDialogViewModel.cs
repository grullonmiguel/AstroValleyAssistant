using AstroValley.Domain.Entities;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// A lookup found several parcels: shows what we searched for next to each candidate and returns the one the user picks
/// (null when cancelled or dismissed). Show it with IDialogService.ShowAsync.
/// </summary>
public partial class ResolveMatchesDialogViewModel(string originalParcelId, string originalAddress, string originalOwner, IEnumerable<RegridMatch> matches)
    : DialogViewModelBase<RegridMatch?>
{
    public override string Title => "Pick the right parcel";

    // What was searched for, to compare against each match
    public string OriginalParcelId { get; } = originalParcelId;
    public string OriginalAddress { get; } = originalAddress;
    public string OriginalOwner { get; } = originalOwner;

    public IReadOnlyList<RegridMatch> Matches { get; } = matches.ToList();

    [RelayCommand]
    private void SelectMatch(RegridMatch match) => CompleteDialog(match);

    [RelayCommand]
    private void Cancel() => CompleteDialog(null);
}
