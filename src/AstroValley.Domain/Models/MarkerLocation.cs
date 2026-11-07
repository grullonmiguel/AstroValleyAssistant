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

    // Research workflow, edited in the map popup and saved with the pins: Interested | Visited | Bid | Skip (null = none).
    public string? Status { get; set; }
    public string? Notes { get; set; }

    // Numbers parsed from ExtraDetails, written to the JSON as plain values (handy for a later Excel export). Missing or 0 = null.
    public decimal? BidAmount => Money("Minimum Bid");
    public decimal? AssessedAmount => Money("Assessed");

    /// <summary>Minimum bid as a percent of assessed value; null when either value is missing or 0.</summary>
    public double? BidToAssessedPercent =>
        BidAmount is { } bid && AssessedAmount is { } assessed ? Math.Round((double)(bid / assessed * 100), 1) : null;

    private decimal? Money(string key) =>
        ExtraDetails?.GetValueOrDefault(key) is { } text
        && decimal.TryParse(text, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.CurrentCulture, out var value)
        && value > 0 ? value : null;

    /// <summary>
    /// Validates that the coordinates are within valid geographic bounds.
    /// </summary>
    public bool IsValid => Latitude is >= -90.0 and <= 90.0 &&
                          Longitude is >= -180.0 and <= 180.0;
}
