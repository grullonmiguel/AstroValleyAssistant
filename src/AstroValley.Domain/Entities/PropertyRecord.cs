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

    // --- House facts from the county record (via Regrid). Any of them can be missing: counties and states report different fields. ---
    public string UseDescription { get; init; } = string.Empty; // the county's land use, e.g. "MULTIFAMILY 2-9 UNITS : 2 LIVING UNITS"
    public string HomeType { get; init; } = string.Empty;       // HomeKind label: "Single-family", "Duplex-4plex", "Vacant lot", ... or empty
    public int? Beds { get; init; }
    public double? Baths { get; init; }                         // a half bath counts 0.5
    public int? LivingSqft { get; init; }
    public int? Units { get; init; }
    public int? YearBuilt { get; init; }
    public int? BuildingCount { get; init; }                    // Regrid's building footprints on the parcel
    public string OtherOwners { get; init; } = string.Empty;    // owner 2-4, "; "-separated (estate / heirs wording shows up here)

    // --- Ownership and value signals for the risk flags (also optional per county) ---
    public string State { get; init; } = string.Empty;          // two-letter code ("FL"): picks the jurisdiction profile
    public DateTime? LastTransferDate { get; init; }
    public decimal? LastSalePrice { get; init; }                // 0 = a $0 transfer (trust, quitclaim, inheritance), not "unknown"
    public int? OwnerCount { get; init; }
    public bool? Homestead { get; init; }                       // null = the county did not say
    public decimal? ImprovementValue { get; init; }             // the building's share of the county's value
    public decimal? LandValue { get; init; }
    public string PreviousOwner { get; init; } = string.Empty;

    // --- Comps (sent back from the Comps tab) ---
    public AstroValley.Domain.Models.CompsResult? Comps { get; init; }

    // --- State Management ---
    public List<RegridMatch> PotentialMatches { get; init; } = new();
}
