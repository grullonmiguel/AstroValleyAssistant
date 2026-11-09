namespace AstroValley.Application.Interfaces.Data;

/// <summary>Free address lookup (US Census, then OpenStreetMap). Call sequentially: the OpenStreetMap service allows about one request per second.</summary>
public interface IGeocoder
{
    /// <summary>Latitude/longitude of the address, or null when no service could place it.</summary>
    Task<(double Lat, double Lon)?> GeocodeAsync(string address, CancellationToken ct = default);

    /// <summary>The 5-digit ZIP code at a point, or null when it can't be found.</summary>
    Task<string?> ZipAsync(double lat, double lon, CancellationToken ct = default);
}
