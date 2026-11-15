using AstroValley.Presentation.ViewModels;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Shows modal dialogs on top of the window and returns what the user chose to the caller.
/// Only one dialog is shown at a time; extra requests wait their turn.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows any dialog view-model and returns its result (default if dismissed).</summary>
    Task<TResult?> ShowAsync<TResult>(DialogViewModelBase<TResult> viewModel);

    [Obsolete("Old overlay dialog; move the caller to ShowAsync.")]
    void ShowDialog(object dialogViewModel);

    void CloseDialog();

    bool IsDialogOpen { get; }
}
