using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Scraping;

public interface IRealTaxDeedClient
{
    Task<List<PropertyRecord>> GetAuctionRecordsAsync(string url, CancellationToken ct = default, IProgress<int> progress = null);
}
