using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Utilities;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public abstract class PropertyScraperViewModelBase : ViewModelBase
    {
        protected CancellationTokenSource? _cts;
        protected IRegridService? _regridService;
        protected IBrowserService? _browserService;

        // -----------------------------
        // Shared Commands
        // -----------------------------

        private ICommand? _cancelCommand;
        public ICommand CancelCommand => 
            _cancelCommand ??= new RelayCommand(_ => CancelOperation(), _ => IsScraping);

        private ICommand? _clearCommand;
        public ICommand ClearCommand => 
            _clearCommand ??= new RelayCommand(_ => Clear());

        private ICommand? _copyRecordCommand;
        public ICommand CopyRecordCommand => _copyRecordCommand ??=
            new RelayCommand<PropertyDataViewModel>(vm =>
            {
                ClipboardFormatter.CopyAllToClipboard(PropertyRecords.Select(vm => vm.Record));
                Status = "All records copied to clipboard.";
            });

        private AsyncRelayCommand? _selectMatchCommand;
        public ICommand SelectMatchCommand => 
            _selectMatchCommand ??= new AsyncRelayCommand(match => ScrapeMatch((RegridMatch)match));

        private ICommand? _loadRegridDataCommand;
        public ICommand LoadRegridDataCommand =>
            _loadRegridDataCommand ??= new RelayCommand(async _ => await EnrichWithRegridAsync(), _ => PropertyRecords.Count > 0 && !IsScraping);

        private ICommand? _scrapeCommand;
        public ICommand ScrapeCommand =>
            _scrapeCommand ??= new RelayCommand(mode => SetScrapeMode((RegridScrapeMode)mode));

        //protected abstract Task ScrapeAsync();

        // -----------------------------
        // UI State
        // -----------------------------

        private string? _status;
        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private bool _isScraping;
        public bool IsScraping
        {
            get => _isScraping;
            set => Set(ref _isScraping, value);
        }

        private bool _isRegridDataLoaded;
        public bool IsRegridDataLoaded
        {
            get => _isRegridDataLoaded;
            set => Set(ref _isRegridDataLoaded, value);
        }
       
        private bool _isScrapeVisible = true;
        public bool IsScrapeVisible
        {
            get => _isScrapeVisible;
            set => Set(ref _isScrapeVisible, value);
        }

        private bool _isResultButtonsVisible;
        public bool IsResultButtonsVisible
        {
            get => _isResultButtonsVisible;
            set => Set(ref _isResultButtonsVisible, value);
        }
       
        private RegridScrapeMode _scrapeMode = RegridScrapeMode.ParcelId;
        public RegridScrapeMode ScrapeMode
        {
            get => _scrapeMode;
            set => Set(ref _scrapeMode, value);
        }

        public PropertyDataViewModel? PropertySelected
        {
            get => _propertySelected;
            set
            {
                if (_propertySelected != value)
                {
                    Set(ref _propertySelected, value);
                }
            }
        }
        private PropertyDataViewModel? _propertySelected;

        // Shared collection
        public ObservableCollection<PropertyDataViewModel> PropertyRecords { get; } = new();

        // -----------------------------
        // Regrid Scraping
        // -----------------------------

        protected async Task EnrichWithRegridAsync()
        {
            if (PropertyRecords.Count == 0)
            {
                Status = "No properties to scrape.";
                return;
            }

            BeginOperation("Begin Regrid Scraping...");

            try
            {
                var ct = _cts!.Token;

                //            string query = ScrapeMode == RegridScrapeMode.ParcelId
                //                ? vm.ParcelId
                //                : vm.Address;

                // 1. Build list of parcel queries
                var queries = PropertyRecords
                    .Select(vm => ScrapeMode == RegridScrapeMode.ParcelId ? vm.ParcelId : vm.Address)
                    .ToList();

                var progress = new Progress<int>(count =>
                {
                    Status = $"Processed {count} of {PropertyRecords.Count}";
                });

                int index = 0;

                foreach (var query in queries)
                {
                    ct.ThrowIfCancellationRequested();

                    var vm = PropertyRecords[index];

                    // 2. Show "Loading" BEFORE scraping begins
                    vm.Status = ScrapeStatus.Loading;
                    vm.Matches.Clear();
                    vm.HasMultipleMatches = false;

                    // 3. Scrape a single parcel
                    var result = await _regridService!.ScrapeSingleAsync(query, ct);

                    // 4. Apply result to the row
                    ApplyRegridResult(vm, result);

                    // 5. Update progress text
                    ((IProgress<int>)progress).Report(index + 1);

                    // 6. External throttling between parcels
                    await Task.Delay(500, ct);

                    index++;
                }

                IsRegridDataLoaded = true;
                SetIdle($"Regrid complete. Processed {PropertyRecords.Count} properties.");
            }
            catch (OperationCanceledException)
            {
                SetIdle("Operation canceled.");
            }
            catch (Exception ex)
            {
                SetIdle($"Error: {ex.Message}");
            }
        }

        protected async Task ScrapeMatch(RegridMatch match)
        {
            if (PropertySelected == null)
                return;

            BeginOperation("Scraping selected Regrid match...");

            try
            {
                var ct = _cts!.Token;

                // 1. Show loading state on the selected row
                PropertySelected.Status = ScrapeStatus.Loading;
                PropertySelected.Matches.Clear();
                PropertySelected.HasMultipleMatches = false;

                // 2. Scrape using the final parcel URL
                var result = await _regridService.ScrapeSingleAsync(match.FullUrl, ct);

                // 3. Apply the result to the selected row
                ApplyRegridResult(PropertySelected, result);

                // 4. Mark sidebar as resolved
                PropertySelected.Matches.Clear();
                PropertySelected.HasMultipleMatches = false;

                SetIdle("Match scraping complete.");
            }
            catch (OperationCanceledException)
            {
                SetIdle("Operation canceled.");
            }
            catch (Exception ex)
            {
                SetIdle($"Error: {ex.Message}");
            }
        }

        protected void ApplyRegridResult(PropertyDataViewModel vm, RegridParcelResult result)
        {
            vm.Matches.Clear();
            vm.HasMultipleMatches = false;

            // Error case
            if (result.Error != null)
            {
                vm.Status = ScrapeStatus.Error;
                return;
            }

            // Not Found case
            if (result.NotFound)
            {
                // If scraper provided a record with a RegridUrl (search URL), merge just that
                if (!string.IsNullOrWhiteSpace(result.Record?.RegridUrl))
                {
                    var existing = vm.Record ?? new PropertyRecord();
                    vm.Record = existing with { RegridUrl = result.Record.RegridUrl };
                }

                vm.Status = ScrapeStatus.NotFound;
                return;
            }

            // Multiple Matches case
            if (result.IsMultiple)
            {
                foreach (var match in result.Matches)
                    vm.Matches.Add(match);

                vm.HasMultipleMatches = true;

                // If scraper provided a record with a RegridUrl (search URL), merge just that
                if (!string.IsNullOrWhiteSpace(result.Record?.RegridUrl))
                {
                    var existing = vm.Record ?? new PropertyRecord();
                    vm.Record = existing with { RegridUrl = result.Record.RegridUrl };
                }

                vm.Status = ScrapeStatus.MultipleMatches;
                return;
            }

            // Success case: full merge
            vm.Record = PropertyRecordMerger.Merge(vm.Record, result.Record!);
            vm.Status = ScrapeStatus.Success;
        }

        private void SetScrapeMode(RegridScrapeMode mode)
        {
            ScrapeMode = mode;
            Status = $"Scrape mode set to {mode}";
            _= EnrichWithRegridAsync();
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        protected void BeginOperation(string message)
        {
            CancelOperation();
            _cts = new CancellationTokenSource();
            IsScraping = true;
            Status = message;
        }

        protected void CancelOperation()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    // Trigger cancellation
                    _cts?.Cancel();

                    // Update UI immediately
                    Status = "Canceling...";
                }
                catch { } // No-op: cancellation is best-effort
            }
        }

        protected void SetIdle(string message)
        {
            IsScraping = false;
            Status = message;
        }

        // -----------------------------
        // Shared Clear Logic
        // -----------------------------
        private void Clear()
        {
            PropertyRecords.Clear();
            Status = string.Empty;
            IsRegridDataLoaded = false;
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }
    }
}
