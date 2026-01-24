using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class RealAuctionViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cts;
        private readonly IRegridClient? _regridScraper;
        private readonly IRealTaxDeedClient? _realScraper;
        private readonly IRegridSettings _regridSettings;
        private readonly IBrowserService _browserService;
        private bool _isRegridAuthenticated = false;

        #region Commands

        // Command to open the AppraiserUrl
        private ICommand? _openRealAuctionCommand;
        public ICommand OpenRealAuctiomCommand => _openRealAuctionCommand ??= new RelayCommand<Button>(OpenRealAuctionUrl);

        // Command to open the AppraiserUrl
        private ICommand? _scrapeRealAuction;
        public ICommand ScrapeRealAuctionCommand => _scrapeRealAuction ??= new RelayCommand(_ => ScrapeRealAuction(), _ => CanScrapeRealAuction());

        private ICommand? _clearCommand;
        public ICommand ClearCommand => _clearCommand ??= new RelayCommand(_ => Clear());

        private ICommand? _scrapeRegridCommand;
        public ICommand ScrapeRegridCommand => _scrapeRegridCommand ??= new AsyncRelayCommand(_ => ScrapeRegridDataAsync(), _ => CanScrapeRegrid());

        #endregion

        #region Properties

        // Holds a Real Auction Calendar Data
        public RealAuctionCalendarDataViewModel RealAuctionCalendarData { get; }

        // The collection your WPF DataGrid binds to
        public ObservableCollection<PropertyDataViewModel> PropertyRecords { get; set; } = new();

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
        private bool? _isScraping = false;

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

        #region Constructor

        public RealAuctionViewModel(IRealTaxDeedClient realScraper, 
                                    IRegridClient regridScraper, 
                                    IRegridSettings regridSettings,
                                    IBrowserService browserService,
                                    RealAuctionCalendarDataViewModel realAuctionData)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(browserService);
            ArgumentNullException.ThrowIfNull(realScraper);
            ArgumentNullException.ThrowIfNull(regridScraper);
            ArgumentNullException.ThrowIfNull(regridSettings);
            ArgumentNullException.ThrowIfNull(realAuctionData);

            // initial state
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;

            _realScraper = realScraper;
            _regridScraper = regridScraper;
            _regridSettings = regridSettings;
            _browserService = browserService;

            RealAuctionCalendarData = realAuctionData;
            RealAuctionCalendarData.AuctionUrlAvailable += OnAuctionUrlAvailable;
            RealAuctionCalendarData.Initialize();

            //LoadDummyData(); // Add Dummy Data for Styling
        }

        #endregion

        #region Methods

        // =============== REAL AUCTION ===============

        private void ScrapeRealAuction() => Task.Run(ScrapeRealAuctionAsync);

        private async Task ScrapeRealAuctionAsync()
        {
            // Create a fresh cancellation token for this run
            _cts = new CancellationTokenSource();

            IsScraping = true;
            Status = "Scraping started...";

            // Progress handler updates the UI thread automatically (SynchronizationContext)
            var progressHandler = new Progress<int>(count =>
            {
                Status = $"Items Found: {count}";
            });

            try
            {
                // Fetch auction records asynchronously
                var records = await _realScraper!.GetAuctionRecordsAsync(
                    CurrentAuctionUrl,
                    _cts.Token,
                    progressHandler
                ).ConfigureAwait(false);

                // Update UI-bound collection on the UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PropertyRecords.Clear();

                    foreach (var record in records)
                        PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
                });

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
                _cts.Dispose();
                _cts = null;

                IsScraping = false;
                IsScrapeVisible = false;
                IsResultButtonsVisible = true;
            }
        }

        private async Task ScrapeRealAuctionAsyncs()
        {
            _cts = new CancellationTokenSource();
            IsScraping = true;
            Status = "Scraping started...";

            // Updates the UI thread whenever progress is reported
            var progressHandler = new Progress<int>(count =>
            {
                Status = $"Items Found: {count}";
            });

            try
            {
                // 1. Get raw records from the service
                var records = await _realScraper!.GetAuctionRecordsAsync(CurrentAuctionUrl, _cts.Token, progressHandler);

                // 2. Clear old data and wrap new data in ItemViewModels
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PropertyRecords.Clear();
                    foreach (var record in records)
                    {
                        PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
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
                _cts.Dispose();
                IsScraping = false;
                IsScrapeVisible = false;
                IsResultButtonsVisible = true;
            }
        }

        private void OnAuctionUrlAvailable(string url, DateTime date)
        {
            CurrentAuctionUrl = url;

            var countyName = RealAuctionCalendarData.SelectedCounty?.Name ?? "Auction";
            CurrentAuctionAlias = $"{countyName} - {date:M/d/yyyy}";

            (_scrapeRealAuction as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanScrapeRealAuction()
        {
            // require a non-empty, well-formed absolute URL
            if (string.IsNullOrWhiteSpace(CurrentAuctionUrl) && 
                !Uri.IsWellFormedUriString(CurrentAuctionUrl, UriKind.Absolute))
                return false;

            // require a valid, non-past date from the data VM
            var date = RealAuctionCalendarData.SelectedDate;
            if (date is null || date.Value.Date < RealAuctionCalendarData.MinAuctionDate)
                return false;

            return true;
        }

        private void Clear()
        {
            // clear results (collection, text, etc.)
            PropertyRecords.Clear();
            Status = string.Empty;
            IsRegridDataLoaded = false;

            // show ScrapeRealAuction again, hide result buttons
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }
        
        private void OpenRealAuctionUrl(object? obj)
        {
            _browserService.Launch(CurrentAuctionUrl);
        }

        // =============== REGRID ===============

        private async Task<bool> EstablishRegridAuthentication(CancellationToken ct)
        {
            if (_regridScraper == null) return false;

            // 1. Check if we already have a valid session in memory
            if (_isRegridAuthenticated)
            {
                Status = "Reusing existing Regrid session...";
                return true;
            }

            Status = "Authenticating with Regrid...";

            string email = _regridSettings.RegridUserName;
            string password = _regridSettings.RegridPassword;

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

        private async Task ScrapeRegridDataAsync()
        {
            // Reset any previous scraping operation and create a fresh token
            ResetCancellationToken();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            IsScraping = true;
            Status = "Initializing Regrid session...";

            try
            {
                // Authenticate before scraping
                bool isAuthenticated = await EstablishRegridAuthentication(ct).ConfigureAwait(false);
                if (!isAuthenticated)
                {
                    Status = "Authentication failed.";
                    return;
                }

                // Progress handler automatically marshals updates to the UI thread
                Progress<int> progress = new(count =>
                {
                    Status = $"Processed {count} of {PropertyRecords.Count} parcels";
                });


                // Mark data as loading once, not inside the loop
                IsRegridDataLoaded = true;

                // Loop through parcels with cancellation support
                for (int i = 0; i < PropertyRecords.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var item = PropertyRecords[i];
                    string query = GetParcelQuery(item);

                    await ProcessSingleParcelAsync(item, query, ct).ConfigureAwait(false);

                    ((IProgress<int>)progress).Report(i + 1);

                    // Polite throttling between requests
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }

                Status = $"Scraping complete. {PropertyRecords.Count} items updated.";
            }
            catch (OperationCanceledException)
            {
                Status = "Canceled by user.";
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;

                IsScraping = false;
            }
        }

        public async Task ProcessSingleParcelAsync(PropertyDataViewModel item, string queryOrUrl, CancellationToken ct)
        {
            try
            {
                item.Status = ScrapeStatus.Loading;

                var result = await _regridScraper.GetPropertyDetailsAsync(queryOrUrl, ct);

                // Always reset match state before applying new results
                item.Matches.Clear();
                item.HasMultipleMatches = false;

                if (result.NotFound)
                {
                    item.Status = ScrapeStatus.NotFound;
                    return;
                }

                if (result.IsMultiple)
                {
                    foreach (var match in result.Matches)
                        item.Matches.Add(match);

                    item.HasMultipleMatches = true;
                    item.Status = ScrapeStatus.MultipleMatches;
                    return;
                }

                // Success case
                item.Record = result.Record;
                item.Status = ScrapeStatus.Success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing parcel {queryOrUrl}: {ex}");
                item.Status = ScrapeStatus.Error;
            }
        }

        private bool CanScrapeRegrid() => PropertyRecords.Count() > 0;

        private static string GetParcelQuery(PropertyDataViewModel item) => !string.IsNullOrWhiteSpace(item.ParcelId)
                ? item.ParcelId
                : item.Address;

        private void ResetCancellationToken()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); }
                catch (ObjectDisposedException) { } // Already disposed — safe to ignore
                finally { _cts.Dispose(); }
            }
        }

        private void LoadDummyData()
        {
            PropertyRecords.Clear();

            var record1 = new PropertyRecord
            {
                ParcelId = "04-3002-026-1440",
                Address = "00 UNASSIGNED LOCATION RE",
                OpeningBid = 775.22m,
                AssessedValue = 600.00m,
                AuctionDate = DateTime.Now,
                PageNumber = 1,
                AppraiserUrl = "https://example.com"
            };

            PropertyRecords.Add(new PropertyDataViewModel(record1, _browserService));

            var record2 = new PropertyRecord
            {
                ParcelId = "11-10-24-4075-2390-0270",
                Address = "456 OAK ST, INTERLACHEN, FL",
                OpeningBid = 635.72m,
                AssessedValue = 800.00m,
                AuctionDate = DateTime.Now,
                PageNumber = 1,
                AppraiserUrl = "https://example.com"
            };
            PropertyRecords.Add(new PropertyDataViewModel(record2, _browserService));

            var record3 = new PropertyRecord
            {
                ParcelId = "09-08-22-1100-0010-0050",
                Address = "789 PINE RD, SATSUMA, FL",
                OpeningBid = 1041.13m,
                AssessedValue = 3100.00m,
                AuctionDate = DateTime.Now,
                PageNumber = 1,
                AppraiserUrl = "https://example.com"
            };
            PropertyRecords.Add(new PropertyDataViewModel(record3, _browserService));

            var record4 = new PropertyRecord
            {
                ParcelId = "10-10-24-4075-2390-0110",
                Address = "321 RIVER RD, WELAKA, FL",
                OpeningBid = 582.97m,
                AssessedValue = 700.00m,
                AuctionDate = DateTime.Now,
                PageNumber = 1,
                AppraiserUrl = "https://example.com"
            };
            PropertyRecords.Add(new PropertyDataViewModel(record4, _browserService));
        }

        #endregion
    }
}

//private async Task ScrapeRegridAsync()
//{
//    // 1. Signal cancellation to any running tasks 
//    if (_cts != null)
//    {
//        try
//        {
//            _cts.Cancel(); 
//        }
//        catch (ObjectDisposedException)
//        {
//            // Already disposed, we can safely ignore this and move on
//        }
//        finally
//        {
//            _cts.Dispose();
//        }
//    }

//    _cts = new CancellationTokenSource();
//    var ct = _cts.Token;

//    IsScraping = true;
//    Status = "Initializing Regrid session...";

//    try
//    {
//        // 2. "When Needed" Initialization
//        bool isAuthenticated = await InitializeRegridAsync(ct);
//        if (!isAuthenticated)
//        {
//            Status = "Authentication failed. Check credentials.";
//            return;
//        }

//        Status = "Processing parcels...";

//        // Use IProgress to update the UI count safely from the background thread 
//        var progress = new Progress<int>(count =>
//        {
//            Status = $"Processed {count} of {PropertyRecords.Count} parcels";
//        });

//        int processedCount = 0;

//        foreach (var item in PropertyRecords)
//        {
//            if (!IsRegridDataLoaded) IsRegridDataLoaded = true;

//            // Check for user cancellation before each network call
//            ct.ThrowIfCancellationRequested();
//            item.Status = ScrapeStatus.Loading;

//            string query = !string.IsNullOrWhiteSpace(item.ParcelId) ? item.ParcelId : item.Address;

//            // 1. Get the Wrapper Result
//            var result = await _regridScraper!.GetPropertyDetailsAsync(query, ct);

//            // 2. Evaluate the outcome
//            if (result.NotFound)
//            {
//                item.Status = ScrapeStatus.NotFound;
//            }
//            else if (result.IsMultiple)
//            {
//                item.Status = ScrapeStatus.MultipleMatches;
//                foreach (var match in result.Matches)
//                {
//                    item.Matches.Add(match);
//                }
//                item.HasMultipleMatches = true;
//            }
//            else
//            {
//                // 3. Success: Hand the record to the ViewModel
//                item.Record = result.Record;
//                item.Status = ScrapeStatus.Success;
//            }

//            processedCount++;
//            ((IProgress<int>)progress).Report(processedCount);

//            // Polite Throttling to remain polite to server firewalls
//            await Task.Delay(800, ct);
//        }

//        Status = $"Scraping complete. {processedCount} items updated.";
//    }
//    catch (OperationCanceledException)
//    {
//        Status = "Scraping process canceled by user.";
//    }
//    catch (Exception ex)
//    {
//        Status = $"Unexpected error: {ex.Message}";
//    }
//    finally
//    {
//        IsScraping = false;
//    }
//}