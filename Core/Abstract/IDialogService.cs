using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    public interface IDialogService
    {
        void CloseDialog();

        void ShowDialog(ViewModelDialogBase viewModel, DialogOption dialogType = DialogOption.Default);
    }
}