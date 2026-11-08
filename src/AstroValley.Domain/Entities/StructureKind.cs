namespace AstroValley.Domain.Entities;

/// <summary>
/// The "Property Type" of a parcel: "Vacant land", "Structures", or empty when the data does not say.
/// Every source (a spreadsheet importer, the Regrid scrape) reduces what it knows to these values.
/// </summary>
public static class StructureKind
{
    public const string Vacant = "Vacant land";
    public const string Structures = "Structures";

    /// <summary>Parcel Fair's "Parcel Type": "Land Only" or "Land &amp; Structures".</summary>
    public static string FromParcelType(string? parcelType) => parcelType?.Trim().ToLowerInvariant() switch
    {
        null or "" => "",
        var t when t.Contains("structure") => Structures,
        var t when t.Contains("land only") || t == "land" => Vacant,
        _ => ""
    };

    /// <summary>
    /// Regrid's fields, numbers first because its text labels over-report ("Developed site with buildings" for a 142 sq ft shed).
    /// shortcut: 400 sq ft / 10% of parcel value are guesses from five samples (houses 1,465 to 2,639 sq ft, a shed 142); tune with more counties.
    /// A building count of 0 alone gives empty, not "Vacant land": footprints can miss buildings.
    /// </summary>
    public static string FromRegrid(bool? struct_, string? useDescription, string? siteDescription, string? activityDescription,
        double? buildingCount, double? footprintSqFt, decimal? improvementValue, decimal? parcelValue)
    {
        var use = useDescription?.ToUpperInvariant() ?? "";
        var site = siteDescription?.ToLowerInvariant() ?? "";
        var activity = activityDescription?.ToLowerInvariant() ?? "";

        // 1. size and value of what is built: a small outbuilding on an empty lot does not make it a structure
        if (footprintSqFt is > 0 || improvementValue is > 0)
        {
            var bigFootprint = footprintSqFt is >= 400;
            var bigValue = improvementValue is > 0 && parcelValue is > 0 && improvementValue / parcelValue >= 0.10m;
            return bigFootprint || bigValue ? Structures : Vacant;
        }

        // 2. no numbers: the county and land-use labels ("UNIMPROVED" contains "IMPROVED", so vacant wording is checked first)
        if (use.Contains("VACANT") || use.Contains("UNIMPROVED") || struct_ == false || activity.Contains("vacant")
            || site.Contains("no buildings") || site.Contains("natural state") || site.Contains("graded") || site.Contains("undeveloped"))
            return Vacant;
        if (use.Contains("IMPROVED") || struct_ == true || site.Contains("with buildings"))
            return Structures;

        return buildingCount is > 0 ? Structures : "";
    }
}
