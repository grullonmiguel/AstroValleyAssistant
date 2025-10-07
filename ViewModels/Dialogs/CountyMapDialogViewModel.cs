using AstroValleyAssistant.Core;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    public class CountyMapDialogViewModel : ViewModelDialogBase
    {
        public StateViewModel State { get; }

        public CountyMapDialogViewModel(StateViewModel state)
        {
            State = state;
            Title = $"{State.Name}: {State.CountyCount} Counties";

            // TODO: Load county data for the given state
        }
    }
}
