using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class AuctionItemViewModel : ViewModelBase
    {
        // The underlying data model
        public AuctionRecord Record { get; }

        // Expose properties for the DataGrid
        public string ParcelId => Record.ParcelId;
        public string Address => Record.PropertyAddress;
        public decimal Bid => Record.OpeningBid;
        public decimal? AssessedValue => Record.AssessedValue;
        public string DateDisplay => Record.AuctionDate.ToShortDateString();
        public string AppraiserUrl => Record.AppraiserUrl;

        // Command to open the AppraiserUrl
        private ICommand? _openAppraiserCommand;
        public ICommand OpenAppraiserCommand => _openAppraiserCommand ??= new RelayCommand<Button>(ExecuteOpenAppraiser);

        public AuctionItemViewModel(AuctionRecord record)
        {
            Record = record;
        }

        private void ExecuteOpenAppraiser(object? obj)
        {
            if (string.IsNullOrEmpty(Record.AppraiserUrl)) return;

            try
            {
                // Opens the default system browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = Record.AppraiserUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not open URL: {ex.Message}");
            }
        }
    }
}
