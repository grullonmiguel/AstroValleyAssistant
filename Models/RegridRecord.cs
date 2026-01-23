namespace AstroValleyAssistant.Models
{
    public record RegridRecord(
        string ParcelId,
        string Address,
        string City,
        string Zip,
        double? Acres,
        string Owner,
        string ZoningCode,
        string ZoningType,
        string GeoCoordinates,
        string ElevationHigh,
        string ElevationLow,
        string FloodZone,
        string RegridUrl,
        decimal? AssessedValue,
        string BirdseyeUrl, 
        List<RegridMatch>? PotentialMatches = null
    );

    public record RegridMatch(
        string Headline,
        string Context,
        string Owner,
        string FullUrl
    );
}
