using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    public interface IRealTaxDeedClient
    {
        Task<List<PropertyRecord>> GetAuctionRecordsAsync(string url, CancellationToken ct = default, IProgress<int> progress = null);
    }
}
