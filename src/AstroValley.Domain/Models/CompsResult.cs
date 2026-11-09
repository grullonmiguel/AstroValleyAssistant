namespace AstroValley.Domain.Models;

/// <summary>One comparable listing read from a real-estate site.</summary>
public record Comp(string? Address, decimal Price, double Acres, bool Sold, string? Url, string? SoldText = null, int? HomeSqft = null, double? Lat = null, double? Lon = null, string? Verify = null,
    int? Beds = null, double? Baths = null, string? HomeType = null)
{
    // HomeType: Zillow's type (SINGLE_FAMILY, MANUFACTURED, LOT, ...), or MANUFACTURED when a card says mobile / manufactured; null = unknown.
    /// <summary>"Single-family", "Mobile" or "Multi-family" (duplex and up): separate markets. Null when the site did not say, or a vacant lot.</summary>
    public string? Kind => HomeType switch { null or "LOT" => null, "MANUFACTURED" => "Mobile", "MULTI_FAMILY" => "Multi-family", _ => "Single-family" };

    /// <summary>Price per square foot of the house; 0 without a floor area.</summary>
    public decimal PricePerSqft => HomeSqft is > 0 ? Math.Round(Price / HomeSqft.Value) : 0;

    // Verify: why this comp needs checking before it is trusted (no listing link, no address, or an address guessed from the card text); null = fine.

    /// <summary>Miles from the property being valued; set after the comp is placed on the map (null = could not be placed).</summary>
    public double? Miles { get; set; }

    /// <summary>Coordinates found by geocoding the address (sites other than Zillow give none); used to pin the comp on a map.</summary>
    public double? FoundLat { get; set; }
    public double? FoundLon { get; set; }

    // Set by CompScoring each time the list refreshes: how much this comp counts (0-1), why, and why it looks misleading (null = fine).
    public double Weight { get; set; } = 1;
    public string? WeightWhy { get; set; }
    public string? Outlier { get; set; }
    public string? OutlierTip { get; set; } // a sentence on what that kind of outlier usually means
    /// <summary>Left out of the value: an outlier the user has not chosen to keep (set by the Comps tab).</summary>
    public bool IsExcluded { get; set; }
    /// <summary>Left out only because it is not among the best matches the value uses (the Comps tab's "Top 5").</summary>
    public bool OutsideTop { get; set; }
    /// <summary>Why the card is faded: the outlier reason, or outside the top matches; null when it counts.</summary>
    public string? LeftOutWhy => Outlier ?? (OutsideTop ? "Outside the 5 best matches: not counted" : null);

    /// <summary>Weight as 1-4 filled dots, for the card and the map list.</summary>
    public string WeightDots { get { var n = Math.Clamp((int)Math.Ceiling(Weight * 4), 1, 4); return new string('●', n) + new string('○', 4 - n); } }
    public string WeightTip => $"Counts {Weight:P0} toward the value: {WeightWhy}. Weight = size match (the lot for land; the house, beds and baths for homes) × distance × how recent the sale is; ●●●● counts fully, ●○○○ barely counts.";

    public decimal PricePerAcre => Acres > 0 ? Math.Round(Price / (decimal)Acres) : 0;

    /// <summary>Lot size as the sites show it: square feet under an acre, else acres; a house card may not show one at all.</summary>
    public string LotText => Acres <= 0 ? "Lot size n/a" : Acres >= 1 ? $"{Acres:0.##} acres" : $"{Acres * 43560:N0} sqft";

    /// <summary>The other unit, shown small under the lot size so both units are always visible.</summary>
    public string LotDetail => Acres <= 0 ? "" : Acres >= 1 ? $"{Acres * 43560:N0} sqft" : $"{Acres:0.00} acres";

    /// <summary>Price per acre and distance, on their own line.</summary>
    public string PriceMiles => string.Join(" · ", new[]
    {
        Acres > 0 ? $"{PricePerAcre:C0} per Acre" : null,
        Miles is { } m ? $"{m:0.0} mi away" : null
    }.Where(x => x is not null));

    /// <summary>The house's floor area, when the card shows one (null for land).</summary>
    private bool HasHouse => HomeSqft is > 0 || Beds is > 0 || Baths is > 0;

    /// <summary>The card's heading: the house for a home ("2,466 sqft · 4 bd / 2 ba"), the lot otherwise.</summary>
    public string Headline => !HasHouse ? LotText : string.Join(" · ", new[]
    {
        HomeSqft is > 0 ? $"{HomeSqft:N0} sqft" : null,
        Beds is > 0 || Baths is > 0 ? $"{Beds?.ToString() ?? "?"} bd / {Baths?.ToString("0.#") ?? "?"} ba" : null
    }.Where(x => x is not null));

    /// <summary>The line under the heading: the lot for a home, the other lot unit for land.</summary>
    public string SubLine => !HasHouse ? LotDetail : Acres > 0 ? $"Lot {LotText} ({LotDetail})" : "Lot size n/a";

    /// <summary>A home's $/sqft and kind ("$230/sqft · Multi-family"); null for land.</summary>
    public string? HomeText => !HasHouse ? null : string.Join(" · ", new[]
    {
        PricePerSqft > 0 ? $"{PricePerSqft:C0}/sqft" : null, Kind
    }.Where(x => x is not null)) is { Length: > 0 } t ? t : null;

    public DateTime? SoldDate => DateTime.TryParse(SoldText, System.Globalization.CultureInfo.GetCultureInfo("en-US"), out var d) ? d : null;

    /// <summary>"SOLD AUG 14, 2026" or "ACTIVE", for the card's badge.</summary>
    public string Badge => Sold ? $"SOLD {SoldText}".Trim().ToUpperInvariant() : "ACTIVE";
}

/// <summary>
/// The comps the user accepted for a pin; saved with the pin and used by the Excel export.
/// Kind "Land" values the parcel as Acres x PricePerAcre; "Home" uses the median price (EstimatedValue).
/// </summary>
public record CompsResult
{
    public string Kind { get; init; } = "Land";
    public string? Site { get; init; }
    public DateTime Date { get; init; } = DateTime.Today;
    public int Count { get; init; }
    public decimal PricePerAcre { get; init; }   // the one used (median or average, as chosen)
    public decimal MedianPrice { get; init; }
    public decimal? EstimatedValue { get; init; } // what the parcel is worth by these comps
    public decimal? RangeLow { get; init; }       // the middle half of the weighted comps, as a value range
    public decimal? RangeHigh { get; init; }
    public string? Confidence { get; init; }      // "High" / "Medium" / "Low"
    public string? ConfidenceText { get; init; }  // the sentence behind it
    public List<Comp> Comps { get; init; } = [];

    public static decimal Median(IEnumerable<decimal> values)
    {
        var v = values.Order().ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}
