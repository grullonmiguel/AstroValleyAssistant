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
        public ICommand PasteParcelsCommand =>
            _pasteParcelsCommand ??= new RelayCommand(_ => PasteParcels());

        private ICommand? _pasteAddressesCommand;
        public ICommand PasteAddressesCommand =>
            _pasteAddressesCommand ??= new RelayCommand(_ => PasteAddresses());





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

        //protected override async Task ScrapeAsync()
        //{
        //    if (PropertyRecords.Count == 0)
        //    {
        //        Status = "No records to scrape.";
        //        return;
        //    }

        //    BeginOperation("Scraping Regrid...");

        //    try
        //    {
        //        _cts = new CancellationTokenSource();
        //        var ct = _cts.Token;

        //        int index = 0;

        //        foreach (var vm in PropertyRecords)
        //        {
        //            ct.ThrowIfCancellationRequested();

        //            vm.Status = ScrapeStatus.Loading;
        //            vm.Matches.Clear();
        //            vm.HasMultipleMatches = false;

        //            string query = ScrapeMode == RegridScrapeMode.ParcelId
        //                ? vm.ParcelId
        //                : vm.Address;

        //            var result = await _regridService.ScrapeSingleAsync(query, ct);

        //            ApplyRegridResult(vm, result);

        //            index++;
        //            Status = $"Processed {index} of {PropertyRecords.Count}";

        //            await Task.Delay(500, ct);
        //        }

        //        SetIdle("Regrid scraping complete.");
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        SetIdle("Operation canceled.");
        //    }
        //    catch (Exception ex)
        //    {
        //        SetIdle($"Error: {ex.Message}");
        //    }
        //}

    }
}
