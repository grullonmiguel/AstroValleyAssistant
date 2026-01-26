using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class RealAuctionViewModel : PropertyScraperViewModelBase
    {
        private readonly IRealTaxDeedClient _realScraper;

        // -----------------------------
        // UI State
        // -----------------------------

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

        public RealAuctionCalendarDataViewModel RealAuctionCalendarData { get; }

        // -----------------------------
        // Constructor
        // -----------------------------
        public RealAuctionViewModel(IRegridService regridService,
                                    IRealTaxDeedClient realScraper,
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

        public ICommand OpenRealAuctionCommand =>
            new RelayCommand(_ => _browserService!.Launch(CurrentAuctionUrl));  

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
        // Helpers
        // -----------------------------

        private void OnAuctionUrlAvailable(string url, DateTime date)
        {
            CurrentAuctionUrl = url;
            var countyName = RealAuctionCalendarData?.SelectedCounty?.Name ?? "Auction";
            CurrentAuctionAlias = $"{countyName} - {date:M/d/yy}";
        }
    }
}