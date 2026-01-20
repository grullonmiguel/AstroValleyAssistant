using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    public interface IRealTaxDeedClient
    {
        Task<List<AuctionRecord>> GetAuctionsAsync(string url, CancellationToken ct = default, IProgress<int> progress = null);
    }
}
