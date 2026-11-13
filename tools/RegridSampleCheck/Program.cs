// Runs the app's real Regrid parsing on every sample JSON in data/ (or the folder given) and prints what it found,
// then runs the scoring and home-type self-checks. See data/parcel-research-log.md.
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using AstroValley.Domain.DueDiligence;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Models;
using AstroValley.Infrastructure.Scraping;

var folder = args.Length > 0 ? args[0] : Directory.Exists("data") ? "data" : "../../data";
var fails = 0;
Trace.Listeners.Clear();
Trace.Listeners.Add(new FailCounter(() => fails++));

// ParseRegridJson is private and uses no injected services, so it can run on an uninitialized scraper
var scraper = (RegridScraper)RuntimeHelpers.GetUninitializedObject(typeof(RegridScraper));
var parse = typeof(RegridScraper).GetMethod("ParseRegridJson", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new MissingMethodException("RegridScraper.ParseRegridJson was renamed; update this tool");

foreach (var file in Directory.GetFiles(folder, "*.json").Order())
{
    PropertyRecord? r;
    try { r = (PropertyRecord?)parse.Invoke(scraper, [File.ReadAllText(file), file]); }
    catch (Exception ex) { Console.WriteLine($"{Path.GetFileName(file)}: could not parse ({(ex.InnerException ?? ex).Message})"); continue; }
    if (r is null) { Console.WriteLine($"{Path.GetFileName(file)}: no \"fields\" object"); continue; }

    var conflict = HomeKind.VacantConflict(r.PropertyType, r.Beds, r.LivingSqft, r.YearBuilt) is not null;
    Console.WriteLine($"""
        {Path.GetFileName(file)}  ({r.Address})
          property type: {Show(r.PropertyType)}   home type: {Show(r.HomeType)}   land use: {Show(r.UseDescription)}
          beds {Show(r.Beds)} / baths {Show(r.Baths)}   sqft {Show(r.LivingSqft)}   units {Show(r.Units)}   built {Show(r.YearBuilt)}   buildings {Show(r.BuildingCount)}
          owner: {Show(r.Owner)}   other owners: {Show(r.OtherOwners)}
          state {Show(r.State)}   last transfer {Show(r.LastTransferDate?.ToString("yyyy-MM-dd"))} for {Show(r.LastSalePrice)}   owners {Show(r.OwnerCount)}   homestead {Show(r.Homestead)}   building {Show(r.ImprovementValue)} / land {Show(r.LandValue)}
          flags: {(conflict ? "VACANT-LAND CONFLICT " : "")}{string.Join(", ", Jurisdictions.Review(r, DateTime.Today).Select(f => $"[{f.Severity}] {f.Title}"))}
          profile: {Jurisdictions.For(r.State, r.County).Name}   names to search: {string.Join(" | ", Jurisdictions.SearchNames(r.Owner, r.OtherOwners, r.PreviousOwner))}
        """);
}

CompScoring.SelfCheck();
HomeKind.SelfCheck();
Jurisdictions.SelfCheck();
AstroValley.Infrastructure.Data.JsonPinStore.SelfCheck();
OwnedProperty.SelfCheck();
AstroValley.Infrastructure.Data.JsonOwnedPropertyStore.SelfCheck();
AstroValley.Infrastructure.Export.PortfolioExcelExporter.SelfCheck();
Console.WriteLine(fails == 0 ? "Self-checks passed." : $"Self-checks FAILED: {fails}");
return fails == 0 ? 0 : 1;

static string Show(object? v) => v is null || $"{v}".Length == 0 ? "-" : $"{v}";

class FailCounter(Action onFail) : TraceListener
{
    public override void Fail(string? message) { onFail(); Console.WriteLine("ASSERT FAILED: " + message); }
    public override void Fail(string? message, string? detail) => Fail(message);
    public override void Write(string? message) { }
    public override void WriteLine(string? message) { }
}
