using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;

namespace AstroValleyAssistant.Core.Services
{
    public interface IRealTaxDeedClient
    {
        Task<List<PropertyRecord>> GetAuctionRecordsAsync(string url, CancellationToken ct = default, IProgress<int> progress = null);
    }
}
