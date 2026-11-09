namespace AstroValley.Domain.Entities;

public record PropertyRecord
{
    // --- Auction Data ---
    public string ParcelId { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public decimal? OpeningBid { get; init; }
    public decimal? AssessedValue { get; init; }
    public DateTime AuctionDate { get; init; }
    public int PageNumber { get; init; }
    public string AppraiserUrl { get; init; } = string.Empty;

    // --- Regrid Data (Merged) ---
    public string City { get; init; } = string.Empty;
    public string County { get; init; } = string.Empty;   // e.g. "Shelby County"; set by importers, else the pin takes it from the auction URL
    public string Zip { get; init; } = string.Empty;
    public double? Acres { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string ZoningCode { get; init; } = string.Empty;
    public string ZoningType { get; init; } = string.Empty;
    public string Latitude { get; init; } = string.Empty;
    public string Longitude { get; init; } = string.Empty;
    public string GeoCoordinates { get; init; } = string.Empty;
    public string ElevationHigh { get; init; } = string.Empty;
    public string ElevationLow { get; init; } = string.Empty;
    public string FloodZone { get; init; } = string.Empty;
    public string LegalDescription { get; init; } = string.Empty;
    public string PropertyType { get; init; } = string.Empty;   // StructureKind: "Vacant land", "Structures" or empty; a spreadsheet's value wins over Regrid's
    public string RegridUrl { get; init; } = string.Empty;
    public string BirdseyeUrl { get; init; } = string.Empty;
    public string? ParcelLines { get; init; } = string.Empty;

    // --- Comps (sent back from the Comps tab) ---
    public AstroValley.Domain.Models.CompsResult? Comps { get; init; }

    // --- State Management ---
    public List<RegridMatch> PotentialMatches { get; init; } = new();
}
