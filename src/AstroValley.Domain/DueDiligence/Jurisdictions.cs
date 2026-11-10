using System.Diagnostics;
using AstroValley.Domain.Entities;

namespace AstroValley.Domain.DueDiligence;

/// <summary>Everything that differs by place: which risk rules apply and what the checklist holds and where the record offices are online.</summary>
public interface IJurisdictionProfile
{
    string Name { get; }
    IReadOnlyList<IRiskRule> RiskRules { get; }
    /// <summary>The record offices' websites, in the order the offices are shown. Empty until someone sets the place up.</summary>
    IReadOnlyList<RecordOfficeLink> Links { get; }
    /// <summary>A fresh checklist (new objects each call, so pins never share answers).</summary>
    IReadOnlyList<DueDiligenceItem> NewChecklist();
}

/// <summary>
/// The rules that apply everywhere. A state or county profile derives from it and lists only what differs: an extra rule / item,
/// or one with the same Code / Key, which replaces the common one (template method: the merge lives here, once).
/// </summary>
public class JurisdictionProfile : IJurisdictionProfile
{
    public virtual string Name => "Any state";

    protected virtual IEnumerable<IRiskRule> CommonRules =>
        [new RecentZeroTransferRule(), new ManyOwnersRule(), new EstateOwnerRule(), new EntityOwnerRule(), new LowBuildingValueRule(), new HomesteadRule()];
    protected virtual IEnumerable<IRiskRule> LocalRules => [];
    protected virtual IEnumerable<DueDiligenceItem> LocalChecklist => [];

    public virtual IReadOnlyList<RecordOfficeLink> Links => [];
    public IReadOnlyList<IRiskRule> RiskRules => Merge(CommonRules, LocalRules, r => r.Code);
    public IReadOnlyList<DueDiligenceItem> NewChecklist() => [.. Merge(CommonChecklist.Items, LocalChecklist, i => i.Key).Select(i => i.Copy())];

    // local entries replace common ones with the same key (in place, keeping the order); new keys go at the end
    private static List<T> Merge<T>(IEnumerable<T> common, IEnumerable<T> local, Func<T, string> key)
    {
        var byKey = local.ToDictionary(key);
        var merged = common.Select(c => byKey.Remove(key(c), out var replacement) ? replacement : c).ToList();
        merged.AddRange(byKey.Values);
        return merged;
    }
}

/// <summary>Finds the profile for a place: county first ("FL/Miami-Dade"), then state ("FL"), then the common one. Register new places here.</summary>
public static class Jurisdictions
{
    private static readonly IJurisdictionProfile Common = new JurisdictionProfile();
    private static readonly Dictionary<string, IJurisdictionProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FL"] = new FloridaProfile(),
        [CountyKey("FL", "Miami-Dade")] = new MiamiDadeProfile(),
    };

    // letters only, so Regrid's "Miami-Dade County" and the auction site's "Miamidade County" find the same profile
    private static string CountyKey(string? state, string county) =>
        $"{state}/{string.Concat(county.Replace(" County", "", StringComparison.OrdinalIgnoreCase).Where(char.IsLetter))}";

    public static IJurisdictionProfile For(string? state, string? county = null) =>
        county is { Length: > 0 } && Profiles.TryGetValue(CountyKey(state, county), out var c) ? c
        : state is { Length: > 0 } && Profiles.TryGetValue(state, out var s) ? s
        : Common;

    /// <summary>The risk flags for a parcel, most serious first.</summary>
    public static List<RiskFlag> Review(PropertyRecord record, DateTime today) =>
        [.. For(record.State, record.County).RiskRules.Select(r => r.Evaluate(record, today)).OfType<RiskFlag>().OrderByDescending(f => f.Severity)];

    /// <summary>Names to search in the official records: every owner on file and the previous owner (a parcel search alone misses older documents).</summary>
    public static List<string> SearchNames(string? owner, string? otherOwners, string? previousOwner = null) =>
        [.. new[] { owner, otherOwners, previousOwner }
            .SelectMany(s => (s ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(n => n.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Assert-based check (no test project yet); run by tools/RegridSampleCheck.</summary>
    public static void SelfCheck()
    {
        var today = new DateTime(2026, 11, 9);
        // the Miami duplex: land-trust deed for $0, 16 owners, an estate, homestead, a $70k building on $367k of land
        var miami = new PropertyRecord
        {
            State = "FL", County = "Miami-Dade County", PropertyType = StructureKind.Structures, Owner = "MICHELLE SOLOMON",
            OtherOwners = "JOHN SOLOMON; JUANITA FRAZIER EST OF", LastTransferDate = new DateTime(2024, 11, 22), LastSalePrice = 0,
            OwnerCount = 16, Homestead = true, ImprovementValue = 70017, LandValue = 367500, AssessedValue = 437517
        };
        var flags = Review(miami, today);
        string[] expected = ["recent-zero-transfer", "many-owners", "estate-owner", "low-building-value", "homestead"];
        Debug.Assert(expected.All(code => flags.Any(f => f.Code == code)), "Miami flags");
        Debug.Assert(flags[0].Severity == RiskSeverity.High, "most serious first");
        Debug.Assert(flags.Single(f => f.Code == "homestead").Detail.Contains("half"), "Florida replaces the common homestead rule");
        Debug.Assert(For("FL").RiskRules.Count(r => r.Code == "homestead") == 1, "a replaced rule is not doubled");
        Debug.Assert(For("IN").RiskRules.Single(r => r.Code == "homestead") is HomesteadRule, "other states keep the common rule");
        Debug.Assert(Review(new PropertyRecord { State = "IN", Owner = "BASEY, JAMES D" }, today).Count == 0, "no data, no flags");

        var list = For("FL").NewChecklist();
        Debug.Assert(list.Select(i => i.Key).Distinct().Count() == list.Count && list.Any(i => i.Key == "fl-municipal-lien-letter"), "Florida adds its item once");
        Debug.Assert(!ReferenceEquals(list[0], For("FL").NewChecklist()[0]), "each pin gets its own checklist");

        var links = For("FL", "Miami-Dade County").Links;
        Debug.Assert(links.Single(l => l.Office == Offices.Appraiser).Url("01-3122-052-2400")!.EndsWith("folio=0131220522400"), "the folio goes in without dashes");
        Debug.Assert(links.All(l => Uri.IsWellFormedUriString(l.Url("0131220522400"), UriKind.Absolute)), "every link is a web address");
        Debug.Assert(links.Single(l => l.Office == Offices.Tax).Url(null) is null && links.First(l => l.Office == Offices.Court).Url(null) is not null, "no parcel: only the search pages");
        Debug.Assert(For("FL", "Miamidade County").NewChecklist().Any(i => i.Key == "fl-municipal-lien-letter") && Review(miami, today).Count == flags.Count, "a county keeps its state's rules");
        Debug.Assert(For("FL").Links.Count == 0 && For("FL", "Broward County").Name == "Florida", "other counties fall back to the state");
        Debug.Assert(SearchNames("MICHELLE SOLOMON", "JOHN SOLOMON; JUANITA FRAZIER EST OF", "michelle solomon").Count == 3, "names split and de-duplicated");
    }
}
