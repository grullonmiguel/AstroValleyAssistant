using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    /// <summary>
    /// Provides authenticated access to Regrid and exposes a single-parcel scrape operation.
    /// The ViewModel controls sequencing, throttling, and UI updates.
    /// </summary>
    public interface IRegridService
    {
        /// <summary>
        /// Scrapes a single parcel (search + detail) and returns a structured result.
        /// Authentication is handled automatically on first use.
        /// </summary>
        Task<RegridParcelResult> ScrapeSingleAsync(string query, CancellationToken ct);
    }
}