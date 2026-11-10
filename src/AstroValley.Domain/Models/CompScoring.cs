using System.Diagnostics;
using System.Globalization;

namespace AstroValley.Domain.Models;

/// <summary>
/// How much each comp counts toward the value, which comps look misleading, and how far to trust the result.
/// Comparability first: a similar size counts most (lot size for land; the house's floor area, beds and baths for homes),
/// then distance, then how recent the sale is. Active listings are asking prices, not sales, so they count half.
/// Works the same anywhere in the country.
/// shortcut: the curves and cut-offs are starting points from appraisal practice (3-5 strong comps, lot within about 0.7x-1.35x,
/// home within about 0.87x-1.15x and +-1 bed / bath, homes within about half a mile, 90 days preferred); first tuned on one Miami
/// home search (2026-11), not yet on rural homes or more cities; adjust them here once there is enough history.
/// </summary>
public static class CompScoring
{
    /// <summary>A comp at or above this weight is a strong comp (close in size, distance and time).</summary>
    public const double Strong = 0.6;

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The number a comp is compared on: $/acre for land, $/sqft for homes when the subject's floor area is known, else price; 0 = cannot be compared.</summary>
    public static decimal Metric(Comp c, bool isHome, bool bySqft = false) => !isHome ? c.PricePerAcre : bySqft ? c.PricePerSqft : c.Price;

    /// <summary>
    /// Sets Weight, WeightWhy and Outlier on each comp. <paramref name="subjectAcres"/> 0 = unknown (lot size is not compared).
    /// For homes: the subject's floor area, beds and baths when known (null = not compared), and whether it is a mobile / manufactured
    /// home or a duplex / multi-family building (otherwise a single-family house).
    /// </summary>
    public static void Score(IReadOnlyCollection<Comp> comps, double subjectAcres, bool isHome, DateTime today,
        int? homeSqft = null, int? beds = null, double? baths = null, bool mobile = false, bool multi = false, bool attached = false)
    {
        var kind = mobile ? "Mobile" : multi ? "Multi-family" : attached ? "Condo / villa / townhouse" : "Single-family";
        foreach (var c in comps)
        {
            var why = new List<string>();
            double size = 1, near = 0.7, recent;
            if (isHome) // the house matters, not the lot
            {
                if (homeSqft > 0 && c.HomeSqft > 0)
                {
                    var r = (double)Math.Max(homeSqft.Value, c.HomeSqft.Value) / Math.Min(homeSqft.Value, c.HomeSqft.Value);
                    // full weight within about 0.87x-1.15x, 0.85 at 1.25x, about half at 1.5x: $/sqft drifts with size, so only close sizes carry over
                    size = Math.Min(1, Math.Pow(1.15 / r, 2));
                    why.Add(r < 1.15 ? "similar home size" : c.HomeSqft > homeSqft ? $"home {r:0.#}× bigger" : $"home {r:0.#}× smaller");
                }
                else if (homeSqft > 0) { size = 0.6; why.Add("home size unknown"); }
                else why.Add("home size not compared");
                if (beds is { } b && c.Beds is { } cb)
                {
                    var d = Math.Abs(b - cb);
                    size *= d <= 1 ? 1 : d == 2 ? 0.7 : 0.4;
                    why.Add(d <= 1 ? $"{cb} bd (close)" : $"{cb} bd vs {b}");
                }
                if (baths is { } ba && c.Baths is { } cba)
                {
                    var d = Math.Abs(ba - cba);
                    size *= d <= 1 ? 1 : d <= 2 ? 0.8 : 0.6;
                    why.Add(d <= 1 ? $"{cba:0.#} ba (close)" : $"{cba:0.#} ba vs {ba:0.#}");
                }
            }
            else if (subjectAcres > 0 && c.Acres > 0)
            {
                var r = Math.Max(subjectAcres, c.Acres) / Math.Min(subjectAcres, c.Acres);
                size = Math.Min(1, 1.35 / r); // full weight within about 0.75x-1.35x, half at 2.7x
                why.Add(r < 1.35 ? "similar lot size" : c.Acres > subjectAcres ? $"lot {r:0.#}× bigger" : $"lot {r:0.#}× smaller");
            }
            else why.Add("lot size not compared");

            // land: full within 1 mi, 0.4 at 10 mi (rural sales are sparse). Homes: full within 0.5 mi, half at 1.5 mi, 0.15 from 2.2 mi,
            // since a mile or two in a city crosses into neighborhoods that sell very differently.
            if (c.Miles is { } mi)
            {
                near = isHome ? Math.Clamp(1 - (mi - 0.5) / 2, 0.15, 1) : Math.Clamp(1 - (mi - 1) / 15, 0.2, 1);
                why.Add($"{mi:0.0} mi away");
            }
            else why.Add("distance unknown");

            if (!c.Sold) { recent = 0.5; why.Add("active listing (asking price)"); }
            else if (c.SoldDate is { } d)
            {
                var months = Math.Max(0, (today - d).TotalDays / 30.4);
                recent = Math.Clamp(1 - (months - 3) / 30, 0.3, 1); // full within 3 months, 0.7 at a year
                why.Add(months < 1 ? "sold this month" : $"sold {months:0} mo ago");
            }
            else { recent = 0.8; why.Add("sale date unknown"); }

            c.Weight = Math.Round(size * near * recent, 2);
            c.WeightWhy = string.Join(", ", why);
            c.Outlier = null;
        }

        // misleading sales, flagged whatever the other comps say
        foreach (var c in comps)
        {
            c.OutlierTip = null;
            if (!isHome && c.Acres is > 0 and < 0.01) // under ~435 sqft: no real lot is that small, so its $/acre means nothing
                (c.Outlier, c.OutlierTip) = ($"Lot size looks wrong ({c.Acres * 43560:N0} sqft)",
                    "The listing's lot size is too small to be real (a data error on the site), so its price per acre is meaningless. Check the listing for the real lot size.");
            else if (c.Sold && c.Price < 1000)
                (c.Outlier, c.OutlierTip) = ($"Nominal price ({c.Price.ToString("C0", Us)})",
                    "Sales under $1,000 are usually family transfers, gifts or token deeds, not market sales.");
            else if (!isHome && c.HomeSqft > 0)
                (c.Outlier, c.OutlierTip) = ($"Has a {c.HomeSqft:N0} sqft home",
                    "A land comp that sold with a house is priced for the house, not the land, so it would push the land value up.");
            else if (isHome && c.HomeType == "LOT")
                (c.Outlier, c.OutlierTip) = ("Vacant lot", "A land sale has no house to compare, so it would pull a home's value down.");
            else if (isHome && c.Kind is { } k && k != kind) // mobile, single-family and multi-family homes are different markets
                (c.Outlier, c.OutlierTip) = k switch
                {
                    "Mobile" => ("Mobile / manufactured home", "Mobile and manufactured homes sell in a different market from site-built houses, usually for much less. Tick Mobile home if the property is one."),
                    "Multi-family" => ("Duplex / multi-family", "Duplexes and other multi-unit buildings sell as income properties, priced differently from single-family houses. Tick Duplex / multi if the property is one."),
                    "Condo / villa / townhouse" => ("Condo / villa / townhouse", "Attached homes (condos, villas, townhouses: a shared wall, usually HOA fees) sell in a different market from detached houses. Tick Condo / villa if the property is one."),
                    _ => ("Single-family house", $"The property is marked as {(mobile ? "a mobile home" : multi ? "a duplex / multi-family building" : "a condo / villa / townhouse")}; detached single-family houses sell in a different market.")
                };
        }

        // far from the middle; with fewer than 4 comps the middle itself is not known well enough
        var bySqft = isHome && homeSqft > 0;
        var rest = comps.Where(c => c.Outlier is null && Metric(c, isHome, bySqft) > 0).ToList();
        if (rest.Count < 4) return;
        var mid = (double)CompsResult.Median(rest.Select(c => Metric(c, isHome, bySqft)));
        var what = !isHome ? "$/acre" : bySqft ? "$/sqft" : "price";
        // homes' $/sqft sits in a narrower band than land's $/acre (which swings with tract size), so the cut-offs are tighter
        var (highCut, lowCut) = isHome ? (2.0, 0.5) : (2.5, 0.4);
        foreach (var c in rest)
        {
            var x = (double)Metric(c, isHome, bySqft) / mid;
            if (x > highCut)
                (c.Outlier, c.OutlierTip) = ($"{(x >= 10 ? x.ToString("N0") : x.ToString("0.#"))}× the median {what}",
                    $"Its {what} is far above the middle of the other comps. " + (isHome ? "Often a fully renovated or much newer house, a much better location, or bad data on the listing."
                        : "Usually a house or other improvements, a much better location, or bad data on the listing."));
            else if (x < lowCut)
                (c.Outlier, c.OutlierTip) = ($"Only {x:P0} of the median {what}",
                    $"Its {what} is far below the middle of the other comps. " + (isHome ? "Often a house needing major repairs, a distressed or family sale, or bad data."
                        : "Often a much bigger tract (bigger tracts sell cheaper per acre), a distressed or family sale, or bad data."));
        }
    }

    /// <summary>Weighted percentile (p 0-1): the value where the running weight reaches p of the total; the middle of two values on an exact tie.</summary>
    public static decimal WeightedPercentile(IEnumerable<(decimal Value, double Weight)> items, double p)
    {
        var v = items.Where(x => x.Weight > 0).OrderBy(x => x.Value).ToList();
        if (v.Count == 0) return 0;
        double total = v.Sum(x => x.Weight), target = p * total, run = 0;
        for (var i = 0; i < v.Count; i++)
        {
            run += v[i].Weight;
            if (Math.Abs(run - target) < 1e-9 && i + 1 < v.Count) return (v[i].Value + v[i + 1].Value) / 2;
            if (run > target) return v[i].Value;
        }
        return v[^1].Value;
    }

    /// <summary>"High" / "Medium" / "Low" and the sentence behind it, from the comps the value uses.</summary>
    public static (string Level, string Text) Confidence(IReadOnlyCollection<Comp> used, DateTime today)
    {
        if (used.Count == 0) return ("Low", "No comps");
        var strong = used.Count(c => c.Weight >= Strong);
        var level = strong >= 3 ? "High" : strong >= 1 && used.Count >= 3 ? "Medium" : "Low";
        var parts = new List<string> { $"{strong} strong of {used.Count} comps" };
        if (used.Where(c => c.Miles is not null).Select(c => c.Miles!.Value).DefaultIfEmpty(-1).Min() is var near and >= 0) parts.Add($"nearest {near:0.0} mi");
        if (used.Where(c => c.Sold && c.SoldDate is not null).Select(c => c.SoldDate!.Value).DefaultIfEmpty().Max() is var newest && newest != default)
            parts.Add((today - newest).TotalDays < 30.4 ? "newest sold this month" : $"newest sold {(today - newest).TotalDays / 30.4:0} mo ago");
        if (used.All(c => !c.Sold)) parts.Add("asking prices only");
        return (level, string.Join(" · ", parts));
    }

    /// <summary>Assert-based check of the rules above (no test project yet); run it from a scratch console or the debugger.</summary>
    public static void SelfCheck()
    {
        var today = new DateTime(2026, 11, 9);
        Comp Land(decimal price, double acres, double miles, string sold, int? home = null) => new(null, price, acres, true, null, sold, home) { Miles = miles };
        var comps = new List<Comp>
        {
            Land(20000, 0.5, 0.4, "Oct 01, 2026"),            // strong: similar, close, recent
            Land(22000, 0.6, 0.9, "Sep 15, 2026"),            // strong
            Land(21000, 0.55, 1.0, "Aug 20, 2026"),           // strong
            Land(150000, 0.5, 0.5, "Oct 05, 2026"),           // 7x the median $/acre: outlier
            Land(200000, 0.46, 0.5, "Aug 17, 2026", 1296),    // has a house: outlier
            Land(100, 0.5, 0.3, "Oct 10, 2026"),              // nominal price: outlier
            Land(30000, 5.0, 12, "Jan 05, 2025")              // far, old, much bigger: weak
        };
        Score(comps, 0.5679, false, today);
        Debug.Assert(comps[0].Weight >= Strong && comps[1].Weight >= Strong && comps[2].Weight >= Strong, "close, recent, similar lots are strong");
        Debug.Assert(comps[6].Weight < 0.15, "far, old, much bigger lot barely counts");
        Debug.Assert(comps[3].Outlier is not null && comps[4].Outlier!.Contains("home") && comps[5].Outlier!.Contains("Nominal"), "outliers flagged");
        Debug.Assert(comps[0].Outlier is null && comps[1].Outlier is null && comps[2].Outlier is null, "normal sales are not outliers");
        Debug.Assert(comps[6].Outlier!.StartsWith("Only"), "a big tract's low $/acre is flagged (bigger tracts sell cheaper per acre)");
        var used = comps.Where(c => c.Outlier is null).ToList();
        Debug.Assert(Confidence(used, today).Level == "High", "3 strong comps = High");
        var bad = new List<Comp> { Land(3000, 1.0 / 43560, 0.9, "Sep 14, 2026") }; // the listing said 1 sqft
        Score(bad, 0.5, false, today);
        Debug.Assert(bad[0].Outlier!.StartsWith("Lot size looks wrong"), "an impossible lot size is a data error, not a price outlier");
        // homes: the house decides, not the lot
        Comp Home(decimal price, int sqft, int bd, double ba, string type = "SINGLE_FAMILY") =>
            new(null, price, 0.17, true, null, "Oct 07, 2026", sqft, null, null, null, bd, ba, type) { Miles = 0.5 };
        var homes = new List<Comp> { Home(418000, 1300, 3, 2), Home(535000, 2986, 5, 4), Home(120000, 1250, 3, 2, "MANUFACTURED"), Home(9000, 0, 0, 0, "LOT") };
        Score(homes, 0.2433, true, today, homeSqft: 1296, beds: 3, baths: 2);
        Debug.Assert(homes[0].Weight >= Strong, "same size, beds and baths is strong");
        Debug.Assert(homes[1].Weight < homes[0].Weight * 0.5, "a house 2.3x bigger with 2 more beds counts far less");
        Debug.Assert(homes[2].Outlier!.StartsWith("Mobile") && homes[3].Outlier == "Vacant lot", "mobile home and vacant lot are flagged for a site-built house");
        var villa = new List<Comp> { Home(400000, 1300, 2, 2, "CONDO"), Home(420000, 1300, 2, 2, "TOWNHOUSE"), Home(600000, 1300, 2, 2) };
        Score(villa, 0, true, today, homeSqft: 1300, beds: 2, baths: 2, attached: true);
        Debug.Assert(villa[0].Outlier is null && villa[1].Outlier is null && villa[2].Outlier == "Single-family house", "a villa is compared with condos / townhouses, not detached houses");
        var duplex = new List<Comp> { Home(500000, 2000, 4, 2, "MULTI_FAMILY"), Home(450000, 2000, 4, 2) };
        Score(duplex, 0.24, true, today, homeSqft: 2110, beds: 4, baths: 2, multi: true);
        Debug.Assert(duplex[0].Outlier is null && duplex[1].Outlier == "Single-family house", "a duplex is compared with duplexes, not houses");
        Score(duplex, 0.24, true, today, homeSqft: 2110, beds: 4, baths: 2);
        Debug.Assert(duplex[0].Outlier!.StartsWith("Duplex") && duplex[1].Outlier is null, "a house is compared with houses, not duplexes");
        Debug.Assert(Metric(homes[0], true, bySqft: true) == 322m, "418,000 / 1,300 sqft = $322/sqft");

        Debug.Assert(WeightedPercentile([(10m, 1), (20m, 1)], 0.5) == 15m, "exact tie takes the middle");
        Debug.Assert(WeightedPercentile([(10m, 1), (20m, 3)], 0.5) == 20m, "heavier comp pulls the median");
        Debug.Assert(Confidence([comps[6]], today).Level == "Low", "one weak comp = Low");
    }
}
