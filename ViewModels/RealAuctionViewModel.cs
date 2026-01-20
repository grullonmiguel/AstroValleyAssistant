using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
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
        public ICommand ScrapeRealAuctionCommand => _scrapeRealAuction ??= new RelayCommand(_ =>  Scrape());

        #endregion

        #region Properties

        // The collection your WPF DataGrid binds to
        public ObservableCollection<AuctionItemViewModel> AuctionRecords { get; set; } = new();

        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private string? _status;

        public bool? IsScraping
        {
            get => _isScraping;
            set => Set(ref _isScraping, value);
        }
        private bool? _isScraping;

        #endregion

        // Add properties and commands specific to the "Real Auction" view here.
        public RealAuctionViewModel(IRealTaxDeedClient realScraper)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(realScraper);

            _realScraper = realScraper;

            // Add Dummy Data for Styling
            LoadDummyData();
        }

        private void Scrape() => Task.Run(async () => { await ScrapeRealAuction(); });

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
                Status = $"Scrape complete! {records.Count} retrieved.";
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
    }
}
