using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Regrid scraping operations: Handles sequential scraping, throttling, cancellation, and progress reporting.
    /// </summary>
    public class RegridService : IRegridService
    {
        private readonly IRegridScraper _scraper;
        private readonly IRegridSettings _settings;

        private bool _isAuthenticated;

        public RegridService(IRegridScraper scraper, IRegridSettings settings)
        {
            _scraper = scraper;
            _settings = settings;
        }

        /// <summary>
        /// Ensures authenticated Regrid session called automatically before any scrape.
        /// </summary>
        private async Task<bool> EnsureAuthenticatedAsync(CancellationToken ct)
        {
            if (_isAuthenticated)
                return true;

            string email = _settings.RegridUserName;
            string password = _settings.RegridPassword;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            _isAuthenticated = await _scraper.AuthenticateAsync(email, password, ct);
            return _isAuthenticated;
        }

        /// <summary>
        /// Scrapes a single parcel from Regrid.
        /// Handles authentication, error wrapping, and null safety.
        /// The ViewModel handles throttling and UI updates.
        /// </summary>
        public async Task<RegridParcelResult> ScrapeSingleAsync(string query, CancellationToken ct)
        {
            // Ensure we have a valid session
            if (!await EnsureAuthenticatedAsync(ct).ConfigureAwait(false))
            {
                return new RegridParcelResult
                {
                    Query = query,
                    Error = new Exception("Regrid authentication failed.")
                };
            }

            try
            {
                // Perform the actual scrape (search + detail)
                var result = await _scraper.GetPropertyDetailsAsync(query, ct).ConfigureAwait(false);

                // Normalize null result
                if (result == null)
                {
                    return new RegridParcelResult
                    {
                        Query = query,
                        NotFound = true
                    };
                }

                // Ensure the result always carries the original query
                return result with { Query = query };
            }
            catch (Exception ex)
            {
                return new RegridParcelResult
                {
                    Query = query,
                    Error = ex
                };
            }
        }
    }
}