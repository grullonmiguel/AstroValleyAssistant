using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    internal class RealAuctionViewModel : ViewModelBase
    {
        private readonly IRealTaxDeedClient? _realScraper;

        #region Commands

        // Command to open the AppraiserUrl
        private ICommand? _scrapeRealAuction;
        public ICommand ScrapeRealAuctionCommand => _scrapeRealAuction ??= new RelayCommand(_ =>  Scrape());

        #endregion

        // The collection your WPF DataGrid binds to
        public ObservableCollection<AuctionItemViewModel> Auctions { get; } = new();

        // Add properties and commands specific to the "Real Auction" view here.
        public RealAuctionViewModel(IRealTaxDeedClient realScraper)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(realScraper);

            _realScraper = realScraper;
        }

        private void Scrape()
        {
            Task.Run(async () => { await ScrapeRealAuction(); });
        }

        private async Task ScrapeRealAuction()
        {
            var url = "https://escambia.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/04/2026";
            url = "https://alachua.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/27/2026";
            url = "https://citrus.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/04/2026";
            url = "https://indian-river.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/20/2026";

            url = "https://putnam.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/21/2026";
            url = "https://volusia.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/27/2026";
            url = "https://sarasota.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/22/2026";
            url = "https://flagler.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/17/2026";
            url = "https://miami-dade.realtaxdeed.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=01/22/2026";
            url = "https://atascosa.texas.sheriffsaleauctions.com/index.cfm?zaction=AUCTION&Zmethod=PREVIEW&AUCTIONDATE=02/03/2026";

            try
            {
                // 1. Get raw records from the service
                var records = await _realScraper!.GetAuctionsAsync(url);

                // 2. Clear old data and wrap new data in ItemViewModels
                Auctions.Clear();
                foreach (var record in records)
                {
                    Auctions.Add(new AuctionItemViewModel(record));
                }
            }
            catch (Exception ex)
            {

                
            }
        }
    }
}
