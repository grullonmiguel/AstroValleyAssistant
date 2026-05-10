using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AstroValley.Presentation.ViewModels;

/// <summary>
/// Base class for dialog ViewModels that return a result via TaskCompletionSource.
/// </summary>
/// <typeparam name="TResult">The type of result the dialog returns.</typeparam>
public abstract class DialogViewModelBase<TResult> : ObservableObject, IDialogViewModel
{
    private readonly TaskCompletionSource<TResult> _taskCompletionSource = new();

    /// <summary>
    /// Gets the title to display in the dialog chrome.
    /// </summary>
    public abstract string Title { get; }

    /// <summary>
    /// Represents the dialog service used to display dialogs within the application.
    /// </summary>
    private IDialogService? _dialogService;

    /// <summary>
    /// Gets a Task that completes when the dialog is closed with a result.
    /// </summary>
    public Task<TResult> Result => _taskCompletionSource.Task;

    /// <summary>
    /// Called by DialogService after ShowDialog so the VM can close itself.
    /// </summary>
    internal void SetDialogService(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    /// <summary>
    /// Completes the dialog with the specified result and requests closure.
    /// </summary>
    protected void CompleteDialog(TResult result)
    {
        _taskCompletionSource.TrySetResult(result);
        _dialogService?.CloseDialog();
    }

    /// <summary>
    /// Cancels the dialog without a result.
    /// </summary>
    protected void CancelDialog()
    {
        _taskCompletionSource.TrySetCanceled();
        _dialogService?.CloseDialog();
    }

    /// <summary>
    /// Called when the dialog is closing. Override to perform cleanup.
    /// Default implementation cancels the task if not already completed.
    /// </summary>
    public virtual void OnDialogClosing()
    {
        // If the task hasn't been completed yet, cancel it
        _taskCompletionSource.TrySetCanceled();
    }
}
