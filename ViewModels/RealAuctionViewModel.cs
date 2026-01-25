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
    public class RealAuctionViewModel : ViewModelBase
    {
        private readonly IRealTaxDeedClient _realScraper;
        private readonly IRegridService _regridService;
        private readonly IBrowserService _browserService;

        private CancellationTokenSource? _cts;

        public ObservableCollection<PropertyDataViewModel> PropertyRecords { get; } = new();

        public RealAuctionCalendarDataViewModel RealAuctionCalendarData { get; }

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

        // -----------------------------
        // UI State
        // -----------------------------
        private bool _isScraping;
        public bool IsScraping
        {
            get => _isScraping;
            set => Set(ref _isScraping, value);
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
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

        private bool _isRegridDataLoaded;
        public bool IsRegridDataLoaded
        {
            get => _isRegridDataLoaded;
            set => Set(ref _isRegridDataLoaded, value);
        }

        private string? _currentAuctionUrl;
        public string? CurrentAuctionUrl
        {
            get => _currentAuctionUrl;
            private set => Set(ref _currentAuctionUrl, value);
        }

        private string? _currentAuctionAlias;
        public string? CurrentAuctionAlias
        {
            get => _currentAuctionAlias;
            private set => Set(ref _currentAuctionAlias, value);
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public RealAuctionViewModel(
            IRealTaxDeedClient realScraper,
            IRegridService regridService,
            IBrowserService browserService,
            RealAuctionCalendarDataViewModel realAuctionData)
        {
            _realScraper = realScraper;
            _regridService = regridService;
            _browserService = browserService;

            RealAuctionCalendarData = realAuctionData;
            RealAuctionCalendarData.AuctionUrlAvailable += OnAuctionUrlAvailable;
            RealAuctionCalendarData.Initialize();
        }

        // -----------------------------
        // Commands
        // -----------------------------
        public ICommand LoadRealAuctionCommand =>
            new RelayCommand(async _ => await LoadRealAuctionAsync(), _ => !IsScraping);

        public ICommand LoadRegridDataCommand =>
            new RelayCommand(async _ => await EnrichWithRegridAsync(), _ => PropertyRecords.Count > 0 && !IsScraping);

        public ICommand CancelCommand =>
            new RelayCommand(_ => CancelOperation(), _ => IsScraping);

        public ICommand ClearCommand =>
            new RelayCommand(_ => Clear());

        public ICommand OpenRealAuctionCommand =>
            new RelayCommand(_ => _browserService.Launch(CurrentAuctionUrl));

        public ICommand CopyRecordCommand =>
            new RelayCommand<PropertyDataViewModel>(vm =>
            {
                ClipboardFormatter.CopyAllToClipboard(PropertyRecords.Select(vm => vm.Record));
                Status = "All records copied to clipboard.";
            });

        private ICommand? _selectMatchCommand;
        public ICommand SelectMatchCommand =>
            _selectMatchCommand ??= new RelayCommand(match => ScrapeMatch((RegridMatch)match));

        // -----------------------------
        // RealAuction Loading
        // -----------------------------
        public async Task LoadRealAuctionAsync()
        {
            BeginOperation("Loading RealAuction data...");

            try
            {
                var ct = _cts!.Token;

                var progress = new Progress<int>(count =>
                {
                    Status = $"Items Found: {count}";
                });

                var records = await _realScraper
                    .GetAuctionRecordsAsync(CurrentAuctionUrl, ct, progress)
                    .ConfigureAwait(false);

                App.Current.Dispatcher.Invoke(() =>
                {
                    PropertyRecords.Clear();
                    foreach (var record in records)
                        PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
                });

                SetIdle($"Loaded {PropertyRecords.Count} properties.");
                IsScrapeVisible = false;
                IsResultButtonsVisible = true;
            }
            catch (OperationCanceledException)
            {
                SetIdle("Scrape canceled.");
            }
            catch (Exception ex)
            {
                SetIdle($"Error: {ex.Message}");
            }
        }

        // -----------------------------
        // Regrid Enrichment
        // -----------------------------
        public async Task EnrichWithRegridAsync()
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

                // 1. Build list of parcel queries
                var queries = PropertyRecords
                    .Select(vm => !string.IsNullOrWhiteSpace(vm.ParcelId) ? vm.ParcelId : vm.Address)
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
                    var result = await _regridService.ScrapeSingleAsync(query, ct);

                    // 4. Apply result to the row
                    ApplyRegridResult(vm, result);

                    // 5. Update progress text
                    ((IProgress<int>)progress).Report(index + 1);

                    // 6. External throttling between parcels
                    await Task.Delay(500, ct);

                    index++;
                }

                IsRegridDataLoaded = true;
                SetIdle("Regrid scraping complete.");
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

        private void ApplyRegridResult(PropertyDataViewModel vm, RegridParcelResult result)
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

        private async void ScrapeMatch(RegridMatch match)
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

        private void ApplyRegridResults(PropertyDataViewModel vm, RegridParcelResult result)
        {
            vm.Matches.Clear();
            vm.HasMultipleMatches = false;

            if (result.Error != null)
            {
                vm.Status = ScrapeStatus.Error;
                return;
            }

            if (result.NotFound)
            {
                vm.Status = ScrapeStatus.NotFound;
                return;
            }

            if (result.IsMultiple)
            {
                foreach (var match in result.Matches)
                    vm.Matches.Add(match);

                vm.HasMultipleMatches = true;
                vm.Status = ScrapeStatus.MultipleMatches;
                
                return;
            }

            // Success
            vm.Record = PropertyRecordMerger.Merge(vm.Record, result.Record!);
            vm.Status = ScrapeStatus.Success;
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        private void BeginOperation(string message)
        {
            CancelOperation();
            _cts = new CancellationTokenSource();
            IsScraping = true;
            Status = message;
        }

        private void CancelOperation()
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
                catch
                {
                    // No-op: cancellation is best-effort
                }
            }

        }

        private void SetIdle(string message)
        {
            IsScraping = false;
            Status = message;
        }

        private void Clear()
        {
            PropertyRecords.Clear();
            Status = string.Empty;
            IsRegridDataLoaded = false;
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }

        private void OnAuctionUrlAvailable(string url, DateTime date)
        {
            CurrentAuctionUrl = url;
            var countyName = RealAuctionCalendarData.SelectedCounty?.Name ?? "Auction";
            CurrentAuctionAlias = $"{countyName} - {date:M/d/yyyy}";
        }
    }
}


//public async Task EnrichWithRegridAsync2()
//{
//    if (PropertyRecords.Count == 0)
//    {
//        Status = "No properties to scrape.";
//        return;
//    }

//    BeginOperation("Start scraping with Regrid...");

//    try
//    {
//        var ct = _cts!.Token;

//        var queries = PropertyRecords
//            .Select(vm => !string.IsNullOrWhiteSpace(vm.ParcelId) ? vm.ParcelId : vm.Address)
//            .ToList();

//        var progress = new Progress<int>(count =>
//        {
//            Status = $"Processed {count} of {PropertyRecords.Count}";
//        });

//        int index = 0;

//        foreach (var query in queries)
//        {
//            ct.ThrowIfCancellationRequested();

//            var parcel = PropertyRecords[index];

//            // 🔥 Show loading BEFORE scraping
//            parcel.Status = ScrapeStatus.Loading;
//            parcel.Matches.Clear();
//            parcel.HasMultipleMatches = false;

//            // 🔥 Scrape ONE parcel at a time
//            var result = await _regridService.ScrapeSingleAsync(query, ct);

//            // Apply result
//            if (result.Error != null)
//            {
//                parcel.Status = ScrapeStatus.Error;
//            }
//            else if (result.NotFound)
//            {
//                parcel.Status = ScrapeStatus.NotFound;
//            }
//            else if (result.IsMultiple)
//            {
//                foreach (var match in result.Matches)
//                    parcel.Matches.Add(match);

//                parcel.HasMultipleMatches = true;
//                parcel.Status = ScrapeStatus.MultipleMatches;
//            }
//            else
//            {
//                parcel.Record = PropertyRecordMerger.Merge(parcel.Record, result.Record!);
//                parcel.Status = ScrapeStatus.Success;
//            }

//            ((IProgress<int>)progress).Report(index + 1);

//            // 🔥 External delay between parcels
//            await Task.Delay(500, ct);

//            index++;
//        }

//        IsRegridDataLoaded = true;
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

//public async Task EnrichWithRegridAsyncs()
//{
//    if (PropertyRecords.Count == 0)
//    {
//        Status = "No properties to enrich.";
//        return;
//    }

//    BeginOperation("Enriching with Regrid...");

//    try
//    {
//        var ct = _cts!.Token;

//        var queries = PropertyRecords
//            .Select(vm => vm.ParcelId)
//            .ToList();

//        var progress = new Progress<int>(count =>
//        {
//            Status = $"Processed {count} of {PropertyRecords.Count}";
//        });

//        var results = await _regridService
//            .ScrapeBatchAsync(queries, ct, progress)
//            .ConfigureAwait(false);

//        for (int i = 0; i < results.Count; i++)
//        {
//            var vm = PropertyRecords[i];
//            var result = results[i];

//            vm.Matches.Clear();
//            vm.HasMultipleMatches = false;

//            if (result.Error != null)
//            {
//                vm.Status = ScrapeStatus.Error;
//                continue;
//            }

//            if (result.NotFound)
//            {
//                vm.Status = ScrapeStatus.NotFound;
//                continue;
//            }

//            if (result.IsMultiple)
//            {
//                foreach (var match in result.Matches)
//                    vm.Matches.Add(match);

//                vm.HasMultipleMatches = true;
//                vm.Status = ScrapeStatus.MultipleMatches;
//                continue;
//            }

//            vm.Record = PropertyRecordMerger.Merge(vm.Record, result.Record!);
//            vm.Status = ScrapeStatus.Success;
//        }

//        IsRegridDataLoaded = true;
//        SetIdle("Regrid enrichment complete.");
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

// -----------------------------
// Single Parcel Refresh
// -----------------------------
//public async Task RefreshParcelFromRegridAsync(PropertyDataViewModel item, string query)
//{
//    BeginOperation("Refreshing parcel...");

//    try
//    {
//        var ct = _cts!.Token;

//        var result = await _regridService
//            .ScrapeSingleAsync(query, ct)
//            .ConfigureAwait(false);

//        item.Matches.Clear();
//        item.HasMultipleMatches = false;

//        if (result.Error != null)
//        {
//            item.Status = ScrapeStatus.Error;
//            SetIdle("Error refreshing parcel.");
//            return;
//        }

//        if (result.NotFound)
//        {
//            item.Status = ScrapeStatus.NotFound;
//            SetIdle("Parcel not found.");
//            return;
//        }

//        if (result.IsMultiple)
//        {
//            foreach (var match in result.Matches)
//                item.Matches.Add(match);

//            item.HasMultipleMatches = true;
//            item.Status = ScrapeStatus.MultipleMatches;
//            SetIdle("Multiple matches found.");
//            return;
//        }

//        item.Record = PropertyRecordMerger.Merge(item.Record, result.Record!);
//        item.Status = ScrapeStatus.Success;

//        SetIdle("Parcel refreshed.");
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