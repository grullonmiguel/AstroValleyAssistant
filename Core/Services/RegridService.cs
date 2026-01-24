using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Regrid scraping operations: Handles sequential scraping, throttling, cancellation, and progress reporting.
    /// </summary>
    public class RegridService : IRegridService
    {
        private readonly IRegridClient _scraper;

        public RegridService(IRegridClient scraper)
        {
            _scraper = scraper;
        }

        /// <summary>
        /// Scrapes a single parcel from Regrid.
        /// </summary>
        public async Task<RegridParcelResult> ScrapeSingleAsync(string queryOrUrl, CancellationToken ct)
        {
            try
            {
                var result = await _scraper.GetPropertyDetailsAsync(queryOrUrl, ct).ConfigureAwait(false);

                return new RegridParcelResult
                {
                    Query = queryOrUrl,
                    NotFound = result.NotFound,
                    IsMultiple = result.IsMultiple,
                    Matches = result.Matches?.ToList() ?? new(),
                    Record = result.Record
                };
            }
            catch (Exception ex)
            {
                return new RegridParcelResult
                {
                    Query = queryOrUrl,
                    Error = ex
                };
            }
        }

        /// <summary>
        /// Scrapes a list of parcels sequentially with throttling.
        /// </summary>
        public async Task<IReadOnlyList<RegridParcelResult>> ScrapeBatchAsync(
            IEnumerable<string> queries,
            CancellationToken ct,
            IProgress<int> progress,
            int throttleDelayMs = 300)
        {
            var results = new List<RegridParcelResult>();
            int count = 0;

            foreach (var query in queries)
            {
                ct.ThrowIfCancellationRequested();

                var result = await ScrapeSingleAsync(query, ct).ConfigureAwait(false);
                results.Add(result);

                count++;
                progress.Report(count);

                // Respect Regrid rate limits
                await Task.Delay(throttleDelayMs, ct).ConfigureAwait(false);
            }

            return results;
        }
    }
}