using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Services;

public class DialogService : IDialogService
{
    private readonly MainViewModel _mainViewModel;

    public bool IsDialogOpen => _mainViewModel.CurrentDialogViewModel != null;

    public DialogService(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public void ShowDialog(object dialogViewModel)
    {
        // Inject self so the VM can trigger close via CompleteDialog/CancelDialog
        var vmType = dialogViewModel.GetType();
        var method = vmType.GetMethod("SetDialogService",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(dialogViewModel, [this]);

        // Wrap the dialog ViewModel in a DialogWrapper that provides chrome
        var wrapper = new DialogWrapper(dialogViewModel, this);
        _mainViewModel.CurrentDialogViewModel = wrapper;
    }

    public void CloseDialog()
    {
        var currentDialog = _mainViewModel.CurrentDialogViewModel;
        _mainViewModel.CurrentDialogViewModel = null;

        // Notify ViewModel AFTER clearing, so any re-entrant CloseDialog calls are no-ops
        if (currentDialog is DialogWrapper wrapper &&
            wrapper.ContentViewModel is IDialogViewModel dialogVm)
        {
            dialogVm.OnDialogClosing();
        }
    }
}
