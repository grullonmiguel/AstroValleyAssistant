using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    internal class RealAuctionViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cts;
        private readonly IRealTaxDeedClient? _realScraper;

        #region Commands

        // Command to open the AppraiserUrl
        private ICommand? _scrapeRealAuction;
        public ICommand ScrapeRealAuctionCommand => _scrapeRealAuction ??= new RelayCommand(_ =>  Scrape(), _ => CanScrape());

        private ICommand? _clearCommand;
        public ICommand ClearCommand => _clearCommand ??= new RelayCommand(_ => Clear());

        #endregion

        #region Properties

        public RealAuctionDataViewModel RealAuctionData { get; }

        // The collection your WPF DataGrid binds to
        public ObservableCollection<AuctionItemViewModel> AuctionRecords { get; set; } = new();

        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private string? _status;

        public string? CurrentAuctionUrl
        {
            get => _currentAuctionUrl;
            private set => Set(ref _currentAuctionUrl, value);
        }
        private string? _currentAuctionUrl;

        public string? CurrentAuctionAlias
        {
            get => _currentAuctionAlias;
            private set => Set(ref _currentAuctionAlias, value);
        }
        private string? _currentAuctionAlias;

        public bool? IsScraping
        {
            get => _isScraping;
            set => Set(ref _isScraping, value);
        }
        private bool? _isScraping;

        public bool IsScrapeVisible
        {
            get => _isScrapeVisible;
            set => Set(ref _isScrapeVisible, value);
        }
        private bool _isScrapeVisible;
        
        public bool IsResultButtonsVisible
        {
            get => _isResultButtonsVisible;
            set => Set(ref _isResultButtonsVisible, value);
        }
        private bool _isResultButtonsVisible;

        #endregion

        // Add properties and commands specific to the "Real Auction" view here.
        public RealAuctionViewModel(IRealTaxDeedClient realScraper, RealAuctionDataViewModel realAuctionDataViewModel)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(realScraper);
            ArgumentNullException.ThrowIfNull(realAuctionDataViewModel);

            _realScraper = realScraper;

            RealAuctionData = realAuctionDataViewModel;
            RealAuctionData.AuctionUrlAvailable += OnAuctionUrlAvailable;

            // Add Dummy Data for Styling
            LoadDummyData();

            OnLoaded();
            // initial state: only Scrape visible
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }

        private void OnLoaded()
        {
            Task.Run(RealAuctionData.InitializeAsync);
        }

        private void Scrape()
        {
            Task.Run(async () => 
            { 
                await ScrapeRealAuction();

                // when results are available:
                IsScrapeVisible = false;
                IsResultButtonsVisible = true;
            });
        }

        private async Task ScrapeRealAuction()
        {
            _cts = new CancellationTokenSource();
            IsScraping = true;
            Status = "Scraping started...";

            var url = "https://escambia.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/04/2026";
            url = "https://alachua.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/27/2026";
            url = "https://citrus.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/04/2026";
            url = "https://indian-river.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/20/2026";

            url = "https://volusia.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/27/2026";
            url = "https://sarasota.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/22/2026";
            url = "https://flagler.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/17/2026";
            url = "https://putnam.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/21/2026";
            url = "https://atascosa.texas.sheriffsaleauctions.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/03/2026";
            url = "https://miami-dade.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/22/2026";

            // This action updates the UI thread whenever progress is reported
            // progressData.Current is the items found, progressData.Total is the estimate
            // Initialize progress reporter with the Tuple (Current, Total)
            // This handles the updates whenever progress.Report(count) is called in the client
            var progressHandler = new Progress<int>(count =>
            {
                Status = $"Items Found: {count}";
            });

            try
            {
                // 1. Get raw records from the service
                var records = await _realScraper!.GetAuctionsAsync(url, _cts.Token, progressHandler);

                // 2. Clear old data and wrap new data in ItemViewModels
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AuctionRecords.Clear();
                    foreach (var record in records)
                    {
                        AuctionRecords.Add(new AuctionItemViewModel(record));
                    }
                });

                // Final UI update
                Status = $"Process completed! {records.Count} records retrieved.";
            }
            catch (OperationCanceledException)
            {
                Status = "Scrape cancelled by user."; 
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsScraping = false;
                _cts.Dispose();
            }
        }

        private void LoadDummyData()
        {
            AuctionRecords.Clear();

            // Row 1: Washington, Raymond
            var record1 = new AuctionRecord(
                ParcelId: "12-10-23-4183-0980-0020",
                PropertyAddress: "00 UNASSIGNED LOCATION RE",
                OpeningBid: 775.22m,
                AssessedValue: 600.00m,
                AuctionDate: DateTime.Now,
                PageNumber: 1,
                AppraiserUrl: "https://example.com"
            );
            AuctionRecords.Add(new AuctionItemViewModel(record1));

            // Row 2: Allen, Devario D
            var record2 = new AuctionRecord(
                ParcelId: "11-10-24-4075-2390-0270",
                PropertyAddress: "456 OAK ST, INTERLACHEN, FL",
                OpeningBid: 635.72m,
                AssessedValue: 800.00m,
                AuctionDate: DateTime.Now,
                PageNumber: 1,
                AppraiserUrl: "https://example.com"
            );
            AuctionRecords.Add(new AuctionItemViewModel(record2));

            // Row 3: Webster, Lewis F
            var record3 = new AuctionRecord(
                ParcelId: "09-08-22-1100-0010-0050",
                PropertyAddress: "789 PINE RD, SATSUMA, FL",
                OpeningBid: 1041.13m,
                AssessedValue: 3100.00m,
                AuctionDate: DateTime.Now,
                PageNumber: 1,
                AppraiserUrl: "https://example.com"
            );
            AuctionRecords.Add(new AuctionItemViewModel(record3));

            // Row 4: Roby Est, Eugene
            var record4 = new AuctionRecord(
                ParcelId: "10-10-24-4075-2390-0110",
                PropertyAddress: "321 RIVER RD, WELAKA, FL",
                OpeningBid: 582.97m,
                AssessedValue: 700.00m,
                AuctionDate: DateTime.Now,
                PageNumber: 1,
                AppraiserUrl: "https://example.com"
            );
            AuctionRecords.Add(new AuctionItemViewModel(record4));
        }

        #region Real Auction Counties

        private async Task LoadCountiesAsync()
        {
            try
            {

                // 1. Get the county info from the service
                //var countiesForState = await _realService.GetCountiesForStateAsync("FL");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load: {ex.Message}");
            }
        }

        private void OnAuctionUrlAvailable(string url, DateTime date)
        {
            CurrentAuctionUrl = url;

            var countyName = RealAuctionData.SelectedCounty?.Name ?? "Auction";
            CurrentAuctionAlias = $"{countyName} - {date:M/d/yyyy}";

            (_scrapeRealAuction as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanScrape()
        {
            // require a non-empty, well-formed absolute URL
            if (string.IsNullOrWhiteSpace(CurrentAuctionUrl))
                return false;

            if (!Uri.IsWellFormedUriString(CurrentAuctionUrl, UriKind.Absolute))
                return false;

            // require a valid, non-past date from the data VM
            var date = RealAuctionData.SelectedDate;
            if (date is null || date.Value.Date < DateTime.Today)
                return false;

            return true;
        }

        private void Clear()
        {
            // clear your results here (e.g., collection, text, etc.)
            AuctionRecords.Clear();
            Status = string.Empty;

            // show Scrape again, hide result buttons
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }

        #endregion
    }
}
