namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// Interface for dialog ViewModels that provides metadata and lifecycle hooks.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>
    /// Gets the title to display in the dialog title bar.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Called when the dialog is about to close (via close button or programmatically).
    /// Use this to clean up resources or cancel pending operations.
    /// </summary>
    void OnDialogClosing();
}
