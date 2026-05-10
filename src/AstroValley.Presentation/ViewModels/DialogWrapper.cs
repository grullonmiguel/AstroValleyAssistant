using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

/// <summary>
/// Wraps a dialog ViewModel to provide chrome (title bar, close button).
/// </summary>
public partial class DialogWrapper : ObservableObject
{
    private readonly IDialogService _dialogService;

    public object ContentViewModel { get; }

    public string Title { get; }

    public DialogWrapper(object contentViewModel, IDialogService dialogService)
    {
        ContentViewModel = contentViewModel;
        _dialogService = dialogService;

        // Get title from ViewModel if it implements IDialogViewModel
        Title = contentViewModel is IDialogViewModel dialogVm
            ? dialogVm.Title
            : "Dialog";
    }

    [RelayCommand]
    private void Close()
    {
        _dialogService.CloseDialog();
    }
}
