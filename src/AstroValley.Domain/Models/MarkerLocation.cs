using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AstroValley.Domain.Models;

/// <summary>
/// Represents a geographic point to be rendered as a marker on the map.
/// </summary>
public record MarkerLocation : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged; // the parcel list shows the access flag
    public string? ParcelID { get; init; }
    public string Address { get; init; } = "N/A";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Acres { get; init; }
    public string? ParcelLines { get; init; }
    public string? County { get; init; }     // e.g. "Marion County", from the auction site
    public string? AuctionUrl { get; init; } // the RealAuction page the pin came from
    public DateTime? AuctionDate { get; init; } // drives pin colour and the "This week" filter
    public string? PinColor { get; init; }      // overrides the auction colour (pins with no auction, e.g. comps)
    public string? CompKey { get; init; }       // identifies a comp pin when the map is used to exclude comps
    public string? OwnedId { get; init; }       // a property already bought (My Properties): its Id there; such pins are saved with the property, not in a pin set
    public bool IsSubject { get; init; }       // the property the comps were searched for (Comps export)
    public Dictionary<string, string>? CompFacts { get; init; } // label -> value rows shown in the popup (Comps export)
    public Dictionary<string, string>? CompTips { get; init; }  // label -> hover explanation for a CompFacts row

    // Links shown in the Home map popup (null = not available, so the text is not a link).
    public string? RegridUrl { get; init; }
    public string? GoogleMapsUrl { get; init; }
    public string? FemaUrl { get; init; }
    public string? AppraiserUrl { get; init; }
    public Dictionary<string, string>? ExtraDetails { get; init; }

    // Research workflow, edited in the map popup and saved with the pins: Interested | Visited | Bid | Skip (null = none).
    public string? Status { get; set; }
    public string? Notes { get; set; }

    // Due diligence (saved with the pins): the risk flags found when the pin was made, the user's checklist answers and the names
    // to search in the official records. State picks the jurisdiction profile (rules and checklist) for the checklist.
    public string? State { get; init; }
    public List<AstroValley.Domain.DueDiligence.RiskFlag>? Risks { get; init; }
    public List<AstroValley.Domain.DueDiligence.DueDiligenceItem>? Checklist { get; set; }
    public List<string>? SearchNames { get; set; }

    /// <summary>Comparable sales the user accepted in the comps dialog; null until then.</summary>
    public CompsResult? Comps { get; set; }

    /// <summary>
    /// The pin for a scraped property, with everything the popup, the filters and the research sheet read; null without coordinates.
    /// Auction rows and owned properties both come through here, so their pins carry the same details.
    /// </summary>
    public static MarkerLocation? FromRecord(AstroValley.Domain.Entities.PropertyRecord r, string? auctionUrl)
    {
        if (AstroValley.Domain.Utilities.StringExtensions.TryParseDouble(r.Latitude) is not { } lat
            || AstroValley.Domain.Utilities.StringExtensions.TryParseDouble(r.Longitude) is not { } lon) return null;

        var details = new Dictionary<string, string>();
        void Add(string key, object? value) { if (value is not null && $"{value}".Trim().Length > 0) details[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!; }
        Add("Flood Zone", r.FloodZone);
        Add("Owner", r.Owner);
        Add("Legal Description", r.LegalDescription);
        Add("Property Type", r.PropertyType);   // "Vacant land" / "Structures"; absent when unknown
        Add("City", r.City);
        Add("Zip", r.Zip);                      // City, Zoning and Type feed the Excel sheet
        Add("Zoning", r.ZoningCode);
        Add("Type", r.ZoningType);
        Add("Elevation Low", r.ElevationLow);   // raw values: the pin computes its slope from these
        Add("Elevation High", r.ElevationHigh);
        var elevations = new[] { r.ElevationLow, r.ElevationHigh }.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
        if (elevations.Count > 0) details["Elevation"] = string.Join(" - ", elevations); // "low - high", or one value when equal
        if (r.AssessedValue is { } assessed) details["Assessed"] = assessed.ToString();  // current culture: read back by Money()
        if (r.OpeningBid is { } bid) details["Minimum Bid"] = bid.ToString();
        // house facts from the county record; each only when the county gave it
        Add("Home Type", r.HomeType);
        Add("Land Use", r.UseDescription);
        Add("Beds", r.Beds); Add("Baths", r.Baths); Add("Living Sqft", r.LivingSqft);
        Add("Units", r.Units); Add("Year Built", r.YearBuilt); Add("Buildings", r.BuildingCount);
        Add("Other Owners", r.OtherOwners);
        Add("House Check", AstroValley.Domain.Entities.HomeKind.VacantConflict(r.PropertyType, r.Beds, r.LivingSqft, r.YearBuilt));

        return new MarkerLocation
        {
            ParcelID = r.ParcelId,
            Address = r.Address,
            Acres = r.Acres?.ToString(),
            Latitude = lat,
            Longitude = lon,
            ParcelLines = r.ParcelLines,
            County = string.IsNullOrWhiteSpace(r.County) ? null : r.County,
            AuctionUrl = string.IsNullOrWhiteSpace(auctionUrl) ? null : auctionUrl,
            AuctionDate = r.AuctionDate.Year > 1 ? r.AuctionDate : null,
            RegridUrl = string.IsNullOrWhiteSpace(r.RegridUrl) ? null : r.RegridUrl,
            GoogleMapsUrl = AstroValley.Domain.Utilities.UrlBuilder.BuildGoogleMapsUrl(r),
            FemaUrl = AstroValley.Domain.Utilities.UrlBuilder.BuildFemaFloodUrl(r),
            AppraiserUrl = string.IsNullOrWhiteSpace(r.AppraiserUrl) ? null : r.AppraiserUrl,
            Comps = r.Comps,
            State = string.IsNullOrWhiteSpace(r.State) ? null : r.State,
            Risks = AstroValley.Domain.DueDiligence.Jurisdictions.Review(r, DateTime.Today),
            SearchNames = AstroValley.Domain.DueDiligence.Jurisdictions.SearchNames(r.Owner, r.OtherOwners, r.PreviousOwner),
            ExtraDetails = details
        };
    }

    /// <summary>Identifies a parcel on the map and in the saved files. Parcel IDs can repeat across counties, so the (rounded) position is part of it.</summary>
    [JsonIgnore]
    public string Key => FormattableString.Invariant($"{ParcelID}|{Latitude:F4}|{Longitude:F4}");

    /// <summary>
    /// The saved file this pin belongs to: one per auction, "FL_Miami-Dade_2026-12-10"; pins with no auction share "FL_Miami-Dade_no-auction".
    /// Scraping the same auction again lands in the same file, so the research on it is found again instead of split across versions.
    /// </summary>
    [JsonIgnore]
    public string SetName
    {
        get
        {
            var county = (County ?? "").Replace(" County", "", StringComparison.OrdinalIgnoreCase).Trim();
            var place = string.Join("_", new[] { State, county }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var name = $"{(place.Length > 0 ? place : "Unsorted")}_{AuctionDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "no-auction"}";
            return string.Concat(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        }
    }

    /// <summary>
    /// Takes the user's work (status, notes, access check, checklist, comps, added names) from an earlier save of the same parcel,
    /// wherever this pin has none of its own: a parcel scraped again arrives blank and must not wipe what was researched.
    /// </summary>
    public void RestoreWorkFrom(MarkerLocation saved)
    {
        Status ??= saved.Status;
        Notes ??= saved.Notes;
        Checklist ??= saved.Checklist;
        Comps ??= saved.Comps;
        if (saved.SearchNames is not null) SearchNames = [.. (SearchNames ?? []).Union(saved.SearchNames, StringComparer.OrdinalIgnoreCase)];
        if (AccessStatus is not null) return;
        AccessStatus = saved.AccessStatus;
        AccessDistanceFt = saved.AccessDistanceFt;
        AccessRoad = saved.AccessRoad;
        AccessChecked = saved.AccessChecked;
    }

    // Road access check (run from the map popup, saved with the pin): "Road" = a road touches or is within ~100 ft,
    // "Near" = nearest road within ~1,000 ft, "None" = no road found within ~1,000 ft ("possibly landlocked"; map data can miss roads).
    private string? _accessStatus;
    private double? _accessDistanceFt;
    private string? _accessRoad;

    public string? AccessStatus { get => _accessStatus; set { _accessStatus = value; AccessChanged(); } }
    public double? AccessDistanceFt { get => _accessDistanceFt; set { _accessDistanceFt = value; AccessChanged(); } }
    public string? AccessRoad { get => _accessRoad; set { _accessRoad = value; AccessChanged(); } }
    public string? AccessChecked { get; set; } // yyyy-MM-dd

    /// <summary>One-line result for tooltips (same wording as the map popup); not saved.</summary>
    [JsonIgnore]
    public string? AccessText => AccessStatus switch
    {
        "Road" => $"Road access: {AccessRoad ?? "road"}{(AccessDistanceFt > 15 ? $" ({AccessDistanceFt:N0} ft)" : "")}",
        "Near" => $"Nearest road {AccessDistanceFt:N0} ft away{(AccessRoad is null ? "" : $" ({AccessRoad})")}, no frontage",
        "None" => "Possibly landlocked: no road within 1,000 ft",
        _ => null
    };

    private void AccessChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessText)));
    }

    // Numbers parsed from ExtraDetails, written to the JSON as plain values (handy for a later Excel export). Missing or 0 = null.
    public decimal? BidAmount => Money("Minimum Bid");
    public decimal? AssessedAmount => Money("Assessed");

    // Written to the JSON as plain values so a pin that already has comps is easy to tell: null = no comps sent back yet.
    public decimal? AvgPricePerAcre => Comps is { Kind: "Land", PricePerAcre: > 0 } c ? c.PricePerAcre : null;
    public decimal? CompsValue => Comps?.EstimatedValue is > 0 ? Comps.EstimatedValue : null;
    public DateTime? CompsDate => Comps?.Date;

    /// <summary>"Vacant land" / "Structures", or null when unknown (see StructureKind); drives the map filter and the Excel Type column.</summary>
    public string? PropertyType => ExtraDetails?.GetValueOrDefault("Property Type");

    /// <summary>True when the legal description uses co-ownership wording (undivided / fractional / partial interest, tenants in common).
    /// shortcut: wording match only, so it can miss unusual phrasing or flag a description that merely mentions it; always read the legal description.</summary>
    public bool UndividedInterest => ExtraDetails?.GetValueOrDefault("Legal Description") is { } legal && UndividedWording.IsMatch(legal);

    /// <summary>True when an owner reads as an estate or heirs ("JUANITA FRAZIER EST OF"): probate and title risk.</summary>
    public bool EstateOwner => AstroValley.Domain.Entities.HomeKind.IsEstateOwner(
        $"{ExtraDetails?.GetValueOrDefault("Owner")}; {ExtraDetails?.GetValueOrDefault("Other Owners")}");

    private static readonly System.Text.RegularExpressions.Regex UndividedWording = new(
        @"UNDIV|\bUND\.?\s*INT|\d+\s*/\s*\d+\s*%?\s*INT|\d+(\.\d+)?\s*%\s*INT|(ONE|TWO|THREE|FOUR|FIVE)[- ](HALF|HALVES|THIRD|THIRDS|FOURTH|FOURTHS|QUARTER|QUARTERS|FIFTH|FIFTHS|SIXTH|EIGHTH)\s+INT|FRACTIONAL\s+INT|PARTIAL\s+INT|TENANT(S|CY)\s+IN\s+COMMON|\bTIC\b|SEV(ERED)?\.?\s*MIN|LIFE\s+EST",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Minimum bid as a percent of assessed value; null when either value is missing or 0.</summary>
    public double? BidToAssessedPercent =>
        BidAmount is { } bid && AssessedAmount is { } assessed ? Math.Round((double)(bid / assessed * 100), 1) : null;

    /// <summary>
    /// Ground slope across the parcel as a percent: (highest - lowest elevation) / the parcel's longest dimension.
    /// shortcut: assumes Regrid elevations are in feet, and the longest dimension is the distance between the farthest two outline
    /// points, so it is a lower bound of the steepest grade; null when elevation or the outline is missing.
    /// </summary>
    public double? SlopePercent => _slope ??= ComputeSlope();
    private double? _slope;

    private double? ComputeSlope()
    {
        if (ExtraDetails is null || string.IsNullOrWhiteSpace(ParcelLines)) return null;
        if (!Number(ExtraDetails.GetValueOrDefault("Elevation Low"), out var low) || !Number(ExtraDetails.GetValueOrDefault("Elevation High"), out var high)) return null;
        var points = new List<(double Lon, double Lat)>();
        try { using var doc = System.Text.Json.JsonDocument.Parse(ParcelLines); CollectPoints(doc.RootElement, points); }
        catch (System.Text.Json.JsonException) { return null; }
        if (points.Count < 2) return null;

        var step = Math.Max(1, points.Count / 400); // keeps the pairwise search small on very detailed outlines
        var sample = points.Where((_, i) => i % step == 0).ToList();
        var metersPerDegLat = 110540.0;
        var metersPerDegLon = 111320.0 * Math.Cos(sample[0].Lat * Math.PI / 180);
        double longest = 0;
        for (var i = 0; i < sample.Count; i++)
            for (var j = i + 1; j < sample.Count; j++)
                longest = Math.Max(longest, Math.Sqrt(Math.Pow((sample[i].Lon - sample[j].Lon) * metersPerDegLon, 2) + Math.Pow((sample[i].Lat - sample[j].Lat) * metersPerDegLat, 2)));
        var longestFeet = longest * 3.28084;
        return longestFeet < 1 ? null : Math.Round(Math.Abs(high - low) / longestFeet * 100, 1);
    }

    private static bool Number(string? text, out double value) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

    // GeoJSON: any array that starts with a number is a [lon, lat] point; everything else is nesting
    private static void CollectPoints(System.Text.Json.JsonElement e, List<(double, double)> into)
    {
        if (e.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            if (e.GetArrayLength() >= 2 && e[0].ValueKind == System.Text.Json.JsonValueKind.Number) into.Add((e[0].GetDouble(), e[1].GetDouble()));
            else foreach (var item in e.EnumerateArray()) CollectPoints(item, into);
        }
        else if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) CollectPoints(p.Value, into);
    }

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
