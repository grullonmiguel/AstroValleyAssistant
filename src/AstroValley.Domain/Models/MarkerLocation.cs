namespace AstroValley.Domain.Models;

/// <summary>
/// Represents a geographic point to be rendered as a marker on the map.
/// </summary>
public record MarkerLocation
{
    public string? ParcelID { get; init; }
    public string Address { get; init; } = "N/A";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Acres { get; init; }
    public string? ParcelLines { get; init; }
    public DateTime? AuctionDate { get; init; } // drives pin colour and the "This week" filter

    // Links shown in the Home map popup (null = not available, so the text is not a link).
    public string? RegridUrl { get; init; }
    public string? GoogleMapsUrl { get; init; }
    public string? FemaUrl { get; init; }
    public string? AppraiserUrl { get; init; }
    public Dictionary<string, string>? ExtraDetails { get; init; }

    /// <summary>
    /// Validates that the coordinates are within valid geographic bounds.
    /// </summary>
    public bool IsValid => Latitude is >= -90.0 and <= 90.0 &&
                          Longitude is >= -180.0 and <= 180.0;
}
