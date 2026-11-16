using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Scraping;

/// <summary>
/// A source of parcel data (Regrid today; ATTOM or others later). Parcel Lookup asks one of these for each row.
/// Register each provider as an IParcelDataProvider; the page lists them by <see cref="Name"/>.
/// </summary>
public interface IParcelDataProvider
{
    /// <summary>Shown to the user when picking a provider.</summary>
    string Name { get; }

    /// <summary>
    /// Looks up one parcel by parcel ID, address, or a match URL from an earlier result.
    /// Never throws for a failed lookup: errors, not-found and rate limits come back on the result.
    /// </summary>
    Task<RegridParcelResult> LookupAsync(string query, CancellationToken ct);
}
