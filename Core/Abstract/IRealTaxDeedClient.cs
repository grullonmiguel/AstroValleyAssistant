using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    public interface IRealTaxDeedClient
    {
        Task<List<AuctionRecord>> GetAuctionsAsync(string url);
    }
}
