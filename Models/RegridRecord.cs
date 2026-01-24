namespace AstroValleyAssistant.Models
{
    public record RegridMatch(
        string Headline,
        string Context,
        string Owner,
        string FullUrl
    );

    public record PropertyRecord
    {
        // --- Auction Data ---
        public string ParcelId { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public decimal OpeningBid { get; init; }
        public decimal? AssessedValue { get; init; }
        public DateTime AuctionDate { get; init; }
        public int PageNumber { get; init; }
        public string AppraiserUrl { get; init; } = string.Empty;

        // --- Regrid Data (Merged) ---
        public string City { get; init; } = string.Empty;
        public string Zip { get; init; } = string.Empty;
        public double? Acres { get; init; }
        public string Owner { get; init; } = string.Empty;
        public string ZoningCode { get; init; } = string.Empty;
        public string ZoningType { get; init; } = string.Empty;
        public string GeoCoordinates { get; init; } = string.Empty;
        public string ElevationHigh { get; init; } = string.Empty;
        public string ElevationLow { get; init; } = string.Empty;
        public string FloodZone { get; init; } = string.Empty;
        public string RegridUrl { get; init; } = string.Empty;
        public string BirdseyeUrl { get; init; } = string.Empty;

        // --- State Management ---
        public List<RegridMatch> PotentialMatches { get; init; } = new();
    }
}
