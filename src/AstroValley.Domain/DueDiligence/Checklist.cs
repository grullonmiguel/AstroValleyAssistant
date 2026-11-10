using System.Text.Json.Serialization;

namespace AstroValley.Domain.DueDiligence;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DueDiligenceStatus { Pending, Clear, Found, Investigate }

/// <summary>One line of the due-diligence checklist on a pin; Status and Note are the user's, the rest comes from the jurisdiction profile.</summary>
public class DueDiligenceItem
{
    public string Key { get; init; } = "";     // stable id, so a state can replace a common item and saved pins keep their answers
    public string Office { get; init; } = "";  // where to search: "Recorder / Clerk", "Tax Collector", "Court", "City / County", "Other"
    public string Title { get; init; } = "";
    public string? Hint { get; init; }
    public DueDiligenceStatus Status { get; set; }
    public string? Note { get; set; }

    public DueDiligenceItem Copy() => new() { Key = Key, Office = Office, Title = Title, Hint = Hint };
}

/// <summary>The checklist every jurisdiction starts from: the "three offices" (recorder, tax collector, appraiser) plus court and city.</summary>
public static class CommonChecklist
{
    private const string Recorder = "Recorder / Clerk", Tax = "Tax Collector", Court = "Court", City = "City / County", Other = "Other";
    private const string NameHint = "Search every owner's name, not only the parcel number: older documents may not carry it.";

    public static IReadOnlyList<DueDiligenceItem> Items { get; } =
    [
        new() { Key = "vesting-deed", Office = Recorder, Title = "Current vesting deed (how title passed)", Hint = "Warranty, quitclaim, trustee or personal representative's deed; note book / page or instrument number." },
        new() { Key = "chain-of-title", Office = Recorder, Title = "Earlier deeds (chain of title)" },
        new() { Key = "mortgages", Office = Recorder, Title = "Mortgages / deeds of trust" },
        new() { Key = "mortgage-changes", Office = Recorder, Title = "Mortgage assignments and modifications" },
        new() { Key = "satisfactions", Office = Recorder, Title = "Satisfactions / releases of each mortgage", Hint = "A mortgage is not a debt if it was satisfied and released." },
        new() { Key = "judgments", Office = Recorder, Title = "Judgment liens", Hint = NameHint },
        new() { Key = "lis-pendens", Office = Recorder, Title = "Lis pendens (pending lawsuits)" },
        new() { Key = "federal-liens", Office = Recorder, Title = "Federal tax liens (IRS)", Hint = NameHint },
        new() { Key = "state-liens", Office = Recorder, Title = "State tax liens", Hint = NameHint },
        new() { Key = "mechanics-liens", Office = Recorder, Title = "Construction / mechanic's liens" },
        new() { Key = "hoa-liens", Office = Recorder, Title = "HOA / condo association liens" },
        new() { Key = "easements", Office = Recorder, Title = "Easements, restrictions and covenants", Hint = "Not debts, but they limit use: access, drainage, utilities, deed restrictions." },
        new() { Key = "leases", Office = Recorder, Title = "Recorded leases / memoranda of lease" },
        new() { Key = "tax-history", Office = Tax, Title = "Tax payment history and amounts owed" },
        new() { Key = "tax-certificates", Office = Tax, Title = "Tax certificates and tax deed applications" },
        new() { Key = "special-assessments", Office = Tax, Title = "Special assessments (paving, sidewalks, drainage, utilities)" },
        new() { Key = "tax-deed-case", Office = Court, Title = "Tax deed case: judgment, notices, status, past cancellations" },
        new() { Key = "foreclosure", Office = Court, Title = "Foreclosure filings" },
        new() { Key = "probate", Office = Court, Title = "Probate / estate cases for any owner" },
        new() { Key = "bankruptcy", Office = Court, Title = "Bankruptcy filings by any owner", Hint = "An open bankruptcy can stop or undo a sale." },
        new() { Key = "code-liens", Office = City, Title = "Code enforcement liens and open violations" },
        new() { Key = "building-cases", Office = City, Title = "Unsafe structure, open / expired permits, demolition orders" },
        new() { Key = "utility-liens", Office = City, Title = "Utility liens (water, sewer, trash)" },
        new() { Key = "forfeiture", Office = Other, Title = "Federal or state forfeiture claims" },
        new() { Key = "occupancy", Office = Other, Title = "Occupancy: vacant, owner or tenant (eviction risk)" },
        new() { Key = "title-company", Office = Other, Title = "Title company: will it insure a resale after this tax deed?" },
    ];
}
