using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    /// <summary>
    /// High-level service that orchestrates Regrid scraping.
    /// It wraps the low-level scraper and provides batch + single scrape operations.
    /// </summary>
    public interface IRegridService
    {
        /// <summary>
        /// Scrapes a single parcel using a ParcelId, Address, or direct Regrid URL.
        /// </summary>
        Task<RegridParcelResult> ScrapeSingleAsync(string queryOrUrl, CancellationToken ct);

        /// <summary>
        /// Scrapes a list of parcels sequentially with throttling and progress reporting.
        /// </summary>
        Task<IReadOnlyList<RegridParcelResult>> ScrapeBatchAsync(
            IEnumerable<string> queries,
            CancellationToken ct,
            IProgress<int> progress,
            int throttleDelayMs = 300);
    }
}