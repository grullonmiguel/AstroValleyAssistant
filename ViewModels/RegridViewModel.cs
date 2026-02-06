using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Export;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using AstroValleyAssistant.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class RegridViewModel : PropertyScraperViewModelBase
    {
        private readonly IDialogService _dialogService;

        // -----------------------------
        // Commands
        // -----------------------------

        private ICommand? _pasteParcelsCommand;
        public ICommand PasteParcelsCommand => _pasteParcelsCommand ??= new RelayCommand(_ => PasteParcels());

        private ICommand? _pasteAddressesCommand;
        public ICommand PasteAddressesCommand => _pasteAddressesCommand ??= new RelayCommand(_ => PasteAddresses());

        private ICommand? _openImportCommand;
        public ICommand OpenImportCommand => _openImportCommand ??= new RelayCommand(_ => ShowImportView());

        // -----------------------------
        // Constructor
        // -----------------------------
        public RegridViewModel(
            IRegridService regridService, 
            IBrowserService browserService, 
            IDialogService dialogService,
            IExporter<IEnumerable<PropertyRecord>, string> clipboardExporter)
        {
            _regridService = regridService;
            _browserService = browserService;
            _dialogService = dialogService;
            _clipboardExporter = clipboardExporter;
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

        private void ShowImportView()
        {
            // 1. Resolve the ViewModel from the DI container to inject IFileService automatically
            var vm = App.Current.Services.GetRequiredService<ImportViewModel>();

            // 2. Set the callback specifically for this instanceCompleted = records =>
            vm.OnImportCompleted = records =>
            {
                LoadImportedRecords(records);
            };
            
            // 3. Show the dialog
            _dialogService?.ShowDialog(vm);
        }

        public void LoadImportedRecords(List<PropertyRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                Status = "No records imported.";
                return;
            }

            PropertyRecords.Clear();

            foreach (var record in records)
                PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));

            // Auto-detect scrape mode based on what the imported file contains
            if (records.All(r => !string.IsNullOrWhiteSpace(r.ParcelId)))
                ScrapeMode = RegridScrapeMode.ParcelId;
            else if (records.All(r => !string.IsNullOrWhiteSpace(r.Address)))
                ScrapeMode = RegridScrapeMode.Address;

            IsScrapeVisible = !PropertyRecords.Any();
            IsResultButtonsVisible = PropertyRecords.Count > 0;
            SetIdle($"Import complete. {records.Count} records loaded.");

            _dialogService.CloseDialog();
        }
    }
}