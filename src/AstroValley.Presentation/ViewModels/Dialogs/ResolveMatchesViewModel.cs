using AstroValley.Domain.Entities;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public partial class ResolveMatchesViewModel : DialogViewModelBase
{
    private readonly IDialogService _dialogService;
    private TaskCompletionSource<RegridMatch?> _completionSource = new();

    public Task<RegridMatch?> Result => _completionSource.Task;

    // ADD: Original property information for comparison
    public string OriginalParcelId { get; }
    public string OriginalAddress { get; }
    public string OriginalOwner { get; }

    public ObservableCollection<RegridMatch> Matches { get; }

    public ResolveMatchesViewModel(
        string originalParcelId,
        string originalAddress,
        string originalOwner,
        IEnumerable<RegridMatch> matches,
        IDialogService dialogService)
    {
        _dialogService = dialogService;

        // Store original property info
        OriginalParcelId = originalParcelId;
        OriginalAddress = originalAddress;
        OriginalOwner = originalOwner;

        Matches = new ObservableCollection<RegridMatch>(matches);
        Title = "Resolve Multiple Matches";
    }

    [RelayCommand]
    private void SelectMatch(RegridMatch match)
    {
        // Check if already completed to prevent exception
        if (!_completionSource.Task.IsCompleted)
        {
            _completionSource.SetResult(match);
        }
        _dialogService.CloseDialog();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Check if already completed to prevent exception
        if (!_completionSource.Task.IsCompleted)
        {
            _completionSource.SetResult(null);
        }
        _dialogService.CloseDialog();
    }

    /// <summary>
    /// Lifecycle hook called when the dialog is being closed.
    /// Ensures the TaskCompletionSource is completed to prevent hanging awaits.
    /// </summary>
    public void OnDialogClosing()
    {
        // If the task hasn't been completed yet (user clicked X button),
        // complete it with null to indicate cancellation
        if (!_completionSource.Task.IsCompleted)
        {
            _completionSource.SetResult(null);
        }
    }
}
