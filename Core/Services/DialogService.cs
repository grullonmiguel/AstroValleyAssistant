using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Services
{
    public class DialogService : IDialogService
    {
        // This Action will be set by the MainViewModel.
        // It holds a reference to the method that can show a dialog.
        public Action<ViewModelDialogBase> ShowDialogAction { get; set; }
        public Action<ViewModelDialogBase> ShowDrawerDialogAction { get; set; }

        // This Action will be set by the MainViewModel for closing.
        public Action CloseDialogAction { get; set; }

        public void ShowDialog(ViewModelDialogBase viewModel, DialogOption dialogType = DialogOption.Default)
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
}
