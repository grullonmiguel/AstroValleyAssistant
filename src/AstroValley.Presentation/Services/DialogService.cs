using AstroValley.Domain.Enums;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Services;

public class DialogService : IDialogService
{
    // This Action will be set by the MainViewModel.
    // It holds a reference to the method that can show a dialog.
    public Action<DialogViewModelBase> ShowDialogAction { get; set; }
    public Action<DialogViewModelBase> ShowDrawerDialogAction { get; set; }

    // This Action will be set by the MainViewModel for closing.
    public Action CloseDialogAction { get; set; }

    public void ShowDialog(DialogViewModelBase viewModel, DialogOption dialogType = DialogOption.Default)
    {
        // When a viewmodel calls ShowDialog, we invoke the action.
        if (dialogType == DialogOption.Default) 
            ShowDialogAction?.Invoke(viewModel);
        else 
            ShowDrawerDialogAction?.Invoke(viewModel);
    }

    public void CloseDialog()
    {
        CloseDialogAction?.Invoke();
    }
}
