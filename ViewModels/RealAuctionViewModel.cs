using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace AstroValleyAssistant.ViewModels
{
    public class RealAuctionViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cts;
        private readonly IRegridClient? _regridScraper;
        private readonly IRealTaxDeedClient? _realScraper;
        private readonly IRegridSettings _settings;

        #region Commands
        // Command to open the AppraiserUrl
        private ICommand? _openRealAuctionCommand;
        public ICommand OpenRealAuctiomCommand => _openRealAuctionCommand ??= new RelayCommand<Button>(ExecuteOpenRealAuction);

        // Command to open the AppraiserUrl
        private ICommand? _scrapeRealAuction;
        public ICommand ScrapeRealAuctionCommand => _scrapeRealAuction ??= new RelayCommand(_ =>  Scrape(), _ => CanScrape());

        private ICommand? _clearCommand;
        public ICommand ClearCommand => _clearCommand ??= new RelayCommand(_ => Clear());

        private ICommand? _scrapeRegridCommand;
        public ICommand ScrapeRegridCommand => _scrapeRegridCommand ??= new AsyncRelayCommand(_ => ScrapeRegridAsync(), _ => CanScrapeRegrid());

        #endregion

        #region Properties

        public RealAuctionDataViewModel RealAuctionData { get; }

        // The collection your WPF DataGrid binds to
        public ObservableCollection<PropertyDataViewModel> AuctionRecords { get; set; } = new();

        public PropertyDataViewModel SelectedAuctionRecord
        {
            get => _selectedAuctionRecord;
            set
            {
                if (_selectedAuctionRecord != value)
                {
                    Set(ref _selectedAuctionRecord, value);
                }
            }
        }
        private PropertyDataViewModel _selectedAuctionRecord;

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

        public bool IsRegridDataLoaded
        {
            get => _isRegridDataLoaded;
            set => Set(ref _isRegridDataLoaded, value);
        }
        private bool _isRegridDataLoaded = false;

        #endregion

        public RealAuctionViewModel(IRegridClient regridScraper, IRealTaxDeedClient realScraper, RealAuctionDataViewModel realAuctionDataViewModel, IRegridSettings settings)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(realScraper);
            ArgumentNullException.ThrowIfNull(regridScraper);
            ArgumentNullException.ThrowIfNull(realAuctionDataViewModel);

            _realScraper = realScraper;
            _regridScraper = regridScraper;
            _settings = settings;

            RealAuctionData = realAuctionDataViewModel;
            RealAuctionData.AuctionUrlAvailable += OnAuctionUrlAvailable;

            // Add Dummy Data for Styling
            //LoadDummyData();

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
                var records = await _realScraper!.GetAuctionsAsync(CurrentAuctionUrl, _cts.Token, progressHandler);

                // 2. Clear old data and wrap new data in ItemViewModels
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AuctionRecords.Clear();
                    foreach (var record in records)
                    {
                        AuctionRecords.Add(new PropertyDataViewModel(record));
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
                ParcelId: "04-3002-026-1440",
                PropertyAddress: "00 UNASSIGNED LOCATION RE",
                OpeningBid: 775.22m,
                AssessedValue: 600.00m,
                AuctionDate: DateTime.Now,
                PageNumber: 1,
                AppraiserUrl: "https://app.regrid.com/us/fl/miami-dade/hialeah/974617/birdseye.jpg"
            );
            
            AuctionRecords.Add(new PropertyDataViewModel(record1));

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
            AuctionRecords.Add(new PropertyDataViewModel(record2));

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
            AuctionRecords.Add(new PropertyDataViewModel(record3));

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
            AuctionRecords.Add(new PropertyDataViewModel(record4));
        }

        #region Real Auction Counties

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

        private bool CanScrapeRegrid()
        {
            return AuctionRecords.Count() > 0;
        }

        private void Clear()
        {
            // clear your results here (e.g., collection, text, etc.)
            AuctionRecords.Clear();
            Status = string.Empty;
            IsRegridDataLoaded = false;

            // show Scrape again, hide result buttons
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }

        #endregion

        #region Regrid

        private bool _isRegridAuthenticated = false;

        private async Task<bool> InitializeRegridAsync(CancellationToken ct)
        {
            if (_regridScraper == null) return false;

            // 1. Check if we already have a valid session in memory
            if (_isRegridAuthenticated)
            {
                Status = "Reusing existing Regrid session...";
                return true;
            }

            Status = "Authenticating with Regrid...";

            string email = _settings.RegridUserName;
            string password = _settings.RegridPassword;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Status = "Regrid credentials missing.";
                return false;
            }

            // 2. Perform the initial handshake only once
            _isRegridAuthenticated = await _regridScraper.AuthenticateAsync(email, password, ct);

            if (!_isRegridAuthenticated)
            {
                Status = "Authentication failed. Check credentials.";
            }

            return _isRegridAuthenticated;
        }

        private async Task ScrapeRegridAsync()
        {
            // 1. Thread Safety & Cancellation Setup
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel(); // Signal cancellation to any running tasks 
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, we can safely ignore this and move on
                }
                finally
                {
                    _cts.Dispose(); // Clean up the old source 
                }
            }
 
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            IsScraping = true;
            Status = "Initializing Regrid session...";

            try
            {
                // 2. "When Needed" Initialization
                bool isAuthenticated = await InitializeRegridAsync(ct);
                if (!isAuthenticated)
                {
                    Status = "Authentication failed. Check credentials.";
                    return;
                }

                Status = "Processing parcels...";

                // Use IProgress to update the UI count safely from the background thread 
                var progress = new Progress<int>(count => {
                    Status = $"Processed {count} of {AuctionRecords.Count} parcels";
                });

                int processedCount = 0;

                foreach (var item in AuctionRecords)
                {
                    if (!IsRegridDataLoaded) IsRegridDataLoaded = true;

                    // Check for user cancellation before each network call
                    ct.ThrowIfCancellationRequested(); 
                    item.Status = ScrapeStatus.Loading;

                    string query = !string.IsNullOrWhiteSpace(item.ParcelId) ? item.ParcelId : item.Address;

                    // 1. Get the Wrapper Result
                    var result = await _regridScraper.GetPropertyDetailsAsync(query, ct);

                    // 2. Evaluate the outcome
                    if (result.NotFound)
                    {
                        item.Status = ScrapeStatus.NotFound;
                    }
                    else if (result.IsMultiple)
                    {
                        item.Status = ScrapeStatus.MultipleMatches;
                        foreach (var match in result.Matches)
                        {
                            item.Matches.Add(match);
                        }
                        item.HasMultipleMatches = true;
                    }
                    else
                    {
                        // 3. Success: Hand the record to the ViewModel
                        item.Regrid = result.Record;
                        item.Status = ScrapeStatus.Success;

                    }

                    processedCount++;
                    ((IProgress<int>)progress).Report(processedCount);

                    // Polite Throttling to remain polite to server firewalls
                    await Task.Delay(800, ct);
                }

                Status = $"Scraping complete. {processedCount} items updated.";
            }
            catch (OperationCanceledException)
            {
                 Status = "Scraping process canceled by user."; 
    }
            catch (Exception ex)
            {
                Status = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                IsScraping = false;
            }
        }


        private void ExecuteOpenRealAuction(object? obj)
        {
            if (string.IsNullOrEmpty(CurrentAuctionUrl)) return;

            LaunchBrowser(CurrentAuctionUrl);
        }

        public void LaunchBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching {url}: {ex.Message}");
            }
        }

        #endregion
    }
}