namespace AstroValley.Presentation.Services;

public interface IDialogService
{
    /// <summary>
    /// Shows a dialog with the specified ViewModel.
    /// </summary>
    /// <param name="dialogViewModel">The ViewModel for the dialog content.</param>
    void ShowDialog(object dialogViewModel);

    /// <summary>
    /// Closes the currently displayed dialog.
    /// </summary>
    void CloseDialog();

    /// <summary>
    /// Gets whether a dialog is currently displayed.
    /// </summary>
    bool IsDialogOpen { get; }

    //void ShowDialog(DialogViewModelBase viewModel, DialogOption dialogType = DialogOption.Default);
}