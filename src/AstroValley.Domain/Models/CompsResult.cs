namespace AstroValley.Domain.Models;

/// <summary>One comparable listing read from a real-estate site.</summary>
public record Comp(string? Address, decimal Price, double Acres, bool Sold, string? Url, string? SoldText = null)
{
    public decimal PricePerAcre => Acres > 0 ? Math.Round(Price / (decimal)Acres) : 0;

    /// <summary>Lot size as the sites show it: square feet under an acre, else acres.</summary>
    public string LotText => Acres >= 1 ? $"{Acres:0.##} acres" : $"{Acres * 43560:N0} sqft";

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
