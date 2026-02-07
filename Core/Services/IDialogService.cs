using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Services
{
    public interface IDialogService
    {
        void CloseDialog();

        void ShowDialog(ViewModelDialogBase viewModel, DialogOption dialogType = DialogOption.Default);
    }
}