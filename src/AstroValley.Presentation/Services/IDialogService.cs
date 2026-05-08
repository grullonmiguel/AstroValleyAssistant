using AstroValley.Domain.Enums;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Services;

public interface IDialogService
{
    void CloseDialog();

    void ShowDialog(DialogViewModelBase viewModel, DialogOption dialogType = DialogOption.Default);
}