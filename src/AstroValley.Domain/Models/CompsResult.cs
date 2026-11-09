namespace AstroValley.Domain.Models;

/// <summary>One comparable listing read from a real-estate site.</summary>
public record Comp(string? Address, decimal Price, double Acres, bool Sold, string? Url, string? SoldText = null, int? HomeSqft = null, double? Lat = null, double? Lon = null)
{
    /// <summary>Miles from the property being valued; set after the comp is placed on the map (null = could not be placed).</summary>
    public double? Miles { get; set; }

    /// <summary>Coordinates found by geocoding the address (sites other than Zillow give none); used to pin the comp on a map.</summary>
    public double? FoundLat { get; set; }
    public double? FoundLon { get; set; }

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
    public string? HomeText => HomeSqft is > 0 ? $"{HomeSqft:N0} sqft home" : null;

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
    public List<Comp> Comps { get; init; } = [];

    public static decimal Median(IEnumerable<decimal> values)
    {
        var v = values.Order().ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}
