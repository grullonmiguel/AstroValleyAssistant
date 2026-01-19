using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    public class RealAuctionSettingsViewModel : ViewModelDialogBase
    {
        private readonly IRealAuctionSettings _settings; 
        
        public Action Saved { get; set; }
        public RelayCommand SaveCommand { get; }

        // Properties with change notification
        public string Url
        {
            get => _url;
            set => Set(ref _url, value);
        }
        private string _url;

        public string State
        {
            get => _state;
            set => Set(ref _state, value);
        }
        private string _state;

        public string County
        {
            get => _county;
            set => Set(ref _county, value);
        }
        private string _county;

        public string LastAuctionDate
        {
            get => _lastAuctionDate;
            set => Set(ref _lastAuctionDate, value);
        }
        private string _lastAuctionDate;

        public RealAuctionSettingsViewModel(IRealAuctionSettings settings)
        {
            _settings = settings;

            // Load current values
            _url = _settings.Url;
            _state = _settings.State;
            _county = _settings.County;
            _lastAuctionDate = _settings.LastAuctionDate;

            SaveCommand = new RelayCommand(ExecuteSave, CanSave);
        }

        private void ExecuteSave(object parameter)
        {
            _settings.Url = Url;
            _settings.State = State;
            _settings.County = County;
            _settings.LastAuctionDate = LastAuctionDate;

            _settings.Save();
            Saved?.Invoke();
        }

        private bool CanSave(object parameter)
        {
            return !string.IsNullOrWhiteSpace(Url) &&
                   !string.IsNullOrWhiteSpace(State) &&
                   !string.IsNullOrWhiteSpace(County);
        }
    }
}
