using System.Globalization;
using System.Text.RegularExpressions;
using AstroValley.Domain.Entities;

namespace AstroValley.Domain.DueDiligence;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum RiskSeverity { Info, Caution, High }

/// <summary>One thing to look into before bidding, e.g. "Recent $0 transfer". Code is stable so a state rule can replace a common one.</summary>
public record RiskFlag(string Code, RiskSeverity Severity, string Title, string Detail);

/// <summary>
/// One risk check (strategy pattern). Common rules apply everywhere; a jurisdiction profile adds its own or replaces one with the
/// same <see cref="Code"/> (see <see cref="JurisdictionProfile"/>), so state differences never become if/else chains here.
/// </summary>
public interface IRiskRule
{
    string Code { get; }
    /// <returns>A flag, or null when the rule does not apply or the county gave no data for it.</returns>
    RiskFlag? Evaluate(PropertyRecord record, DateTime today);
}

internal static class Fmt
{
    public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal? v) => (v ?? 0).ToString("C0", Us);
    public static string Owners(PropertyRecord r) => $"{r.Owner}; {r.OtherOwners}";
}

/// <summary>A $0 transfer in the last 3 years: a trust, quitclaim or inheritance deed, and the owner names on file may already be out of date.</summary>
public sealed class RecentZeroTransferRule : IRiskRule
{
    public string Code => "recent-zero-transfer";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) =>
        r.LastTransferDate is { } d && r.LastSalePrice is 0 && (today - d).TotalDays <= 3 * 365
            ? new(Code, RiskSeverity.Caution, $"$0 transfer on {d:yyyy-MM-dd}",
                "Ownership changed recently with no sale price: usually a trust, quitclaim or inheritance deed, and the owner names on file may be out of date; pull that deed first. "
                + "(Some counties record $0 because they never publish prices; then this means less.)")
            : null;
}

/// <summary>Many owners on record usually means heirs' property: everyone must be served, and title is hard to insure.</summary>
public sealed class ManyOwnersRule : IRiskRule
{
    public string Code => "many-owners";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) => r.OwnerCount switch
    {
        >= 10 => new(Code, RiskSeverity.High, $"{r.OwnerCount} owners on record", "Very likely heirs' property. Each owner is a party to clear; expect title problems and a harder resale."),
        > 3 => new(Code, RiskSeverity.Caution, $"{r.OwnerCount} owners on record", "Several owners: check how each one took title and whether any have died (heirs)."),
        _ => null
    };
}

/// <summary>An owner reads as an estate, heirs or executor: probate decides who can convey.</summary>
public sealed class EstateOwnerRule : IRiskRule
{
    public string Code => "estate-owner";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) =>
        HomeKind.IsEstateOwner(Fmt.Owners(r))
            ? new(Code, RiskSeverity.High, "Estate / heirs owner", "An owner is an estate or heirs. Check the probate case: who can convey, and whether all heirs were notified of the tax sale.")
            : null;
}

/// <summary>A trust, LLC or corporation took title recently: find the trustee / managers and the deed that put it there.</summary>
public sealed class EntityOwnerRule : IRiskRule
{
    private static readonly Regex Entity = new(@"\b(TRUST|TRUSTEE|LLC|L\.L\.C|INC|CORP|CORPORATION|LP|LLP|LTD|COMPANY)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public string Code => "entity-owner";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) =>
        Entity.IsMatch(Fmt.Owners(r)) && r.LastTransferDate is { } d && (today - d).TotalDays <= 3 * 365
            ? new(Code, RiskSeverity.Caution, "Trust / company owner since " + d.ToString("yyyy"),
                "A trust or company took title recently. Search its name and its trustee or managers in the official records as well as the old owners.")
            : null;
}

/// <summary>The county values the building at a small share of the property: it likely needs major work (or is a teardown).</summary>
public sealed class LowBuildingValueRule : IRiskRule
{
    public string Code => "low-building-value";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today)
    {
        if (r.PropertyType != StructureKind.Structures || r.ImprovementValue is not > 0 || r.LandValue is not > 0) return null;
        var share = r.ImprovementValue.Value / (r.ImprovementValue.Value + r.LandValue.Value);
        return share < 0.2m
            ? new(Code, RiskSeverity.Caution, $"Building is {share:P0} of the county value",
                $"Building {Fmt.Money(r.ImprovementValue)} vs land {Fmt.Money(r.LandValue)}: the structure likely needs major work. Value from the low end of the comps range.")
            : null;
    }
}

/// <summary>Homestead: the owner lives there (or did). Common wording; states with special tax-sale rules replace it (see FloridaProfile).</summary>
public sealed class HomesteadRule : IRiskRule
{
    public string Code => "homestead";
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) =>
        r.Homestead == true
            ? new(Code, RiskSeverity.Info, "Homestead property", "The owner claims it as their residence: expect it to be occupied. Some states add notice rules or protections for homesteads.")
            : null;
}
