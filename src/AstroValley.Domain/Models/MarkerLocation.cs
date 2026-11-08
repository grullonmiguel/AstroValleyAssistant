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

    // Links shown in the Home map popup (null = not available, so the text is not a link).
    public string? RegridUrl { get; init; }
    public string? GoogleMapsUrl { get; init; }
    public string? FemaUrl { get; init; }
    public string? AppraiserUrl { get; init; }
    public Dictionary<string, string>? ExtraDetails { get; init; }

    // Research workflow, edited in the map popup and saved with the pins: Interested | Visited | Bid | Skip (null = none).
    public string? Status { get; set; }
    public string? Notes { get; set; }

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

    /// <summary>True when the legal description uses co-ownership wording (undivided / fractional / partial interest, tenants in common).
    /// shortcut: wording match only, so it can miss unusual phrasing or flag a description that merely mentions it; always read the legal description.</summary>
    public bool UndividedInterest => ExtraDetails?.GetValueOrDefault("Legal Description") is { } legal && UndividedWording.IsMatch(legal);

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
