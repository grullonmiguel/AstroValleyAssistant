using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using System.Windows;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class RegridViewModel : PropertyScraperViewModelBase
    {
        // -----------------------------
        // Commands
        // -----------------------------

        private ICommand? _pasteParcelsCommand;
        public ICommand PasteParcelsCommand => _pasteParcelsCommand ??= new RelayCommand(_ => PasteParcels());

        private ICommand? _pasteAddressesCommand;
        public ICommand PasteAddressesCommand => _pasteAddressesCommand ??= new RelayCommand(_ => PasteAddresses());

        // -----------------------------
        // Constructor
        // -----------------------------
        public RegridViewModel(IRegridService regridService, IBrowserService browserService)
        {
            _regridService = regridService;
            _browserService = browserService;   
        }

        // -----------------------------
        // Paste Logic
        // -----------------------------

        private void PasteParcels()
        {
            if (!Clipboard.ContainsText())
                return;

            string text = Clipboard.GetText();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            PropertyRecords.Clear();

            foreach (var line in lines)
            {
                var record = new PropertyRecord{ ParcelId = line.Trim() };
                PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
            }

            ScrapeMode = RegridScrapeMode.ParcelId;

            IsScrapeVisible = PropertyRecords.Count == 0;
            IsResultButtonsVisible = PropertyRecords.Count > 0;
        }

        private void PasteAddresses()
        {
            if (!Clipboard.ContainsText())
                return;

            string text = Clipboard.GetText();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            PropertyRecords.Clear();

            foreach (var line in lines)
            {
                var record = new PropertyRecord { Address = line.Trim() };
                PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
            }

            ScrapeMode = RegridScrapeMode.Address;
            IsScrapeVisible = PropertyRecords.Count == 0;
            IsResultButtonsVisible = PropertyRecords.Count > 0;
        }

        // -----------------------------
        // Scrape Logic
        // -----------------------------

    }
}