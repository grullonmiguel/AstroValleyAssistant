using AstroValleyAssistant.Core.Abstract;

namespace AstroValleyAssistant.Core.Services
{
    public class DialogService : IDialogService
    {
        // This Action will be set by the MainViewModel.
        // It holds a reference to the method that can show a dialog.
        public Action<ViewModelDialogBase> ShowDialogAction { get; set; }

        // This Action will be set by the MainViewModel for closing.
        public Action CloseDialogAction { get; set; }

        public void ShowDialog(ViewModelDialogBase viewModel)
        {
            // When a viewmodel calls ShowDialog, we invoke the action.
            ShowDialogAction?.Invoke(viewModel);
        }

        public void CloseDialog()
        {
            CloseDialogAction?.Invoke();
        }
    }
}
