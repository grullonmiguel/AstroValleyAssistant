using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AstroValley.Application.Interfaces.Data;
using AstroValley.Domain.Entities;
using Microsoft.VisualBasic.FileIO;

namespace AstroValley.Infrastructure.Data;

/// <summary>Owned properties as JSON files in one folder, "{Id}.json": a bad write can only hurt one property.</summary>
public sealed class JsonOwnedPropertyStore(string folder) : IOwnedPropertyStore
{
    // enums as words, so a file stays readable and survives a reordered enum
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public string Folder => folder;

    public IReadOnlyList<OwnedProperty> List()
    {
        if (!Directory.Exists(folder)) return [];
        var properties = new List<OwnedProperty>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<OwnedProperty>(File.ReadAllText(file), Json) is { } p)
                    properties.Add(Whole(p) with { Id = Path.GetFileNameWithoutExtension(file) }); // the file name is the identity, also for a file dropped in by hand
            }
            catch (JsonException ex)
            {
                // left in place and out of the list: only a save of that same property can replace it
                Trace.TraceWarning($"Owned property '{Path.GetFileName(file)}' could not be read: {ex.Message}");
            }
        }
        return [.. properties.OrderByDescending(p => p.PurchaseDate ?? DateTime.MinValue).ThenBy(p => p.Record.Address)];
    }

    public void Save(OwnedProperty property)
    {
        Directory.CreateDirectory(folder);
        // written beside the file and swapped in, so a crash mid-write leaves the previous save intact
        var path = PathOf(property.Id);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(property, Json));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public OwnedProperty Import(string path)
    {
        var read = JsonSerializer.Deserialize<OwnedProperty>(File.ReadAllText(path), Json) ?? throw new JsonException("The file is empty.");
        // lists and the record may be absent or null in a hand-written file; a fresh Id keeps an import from replacing a property already here
        var property = Whole(read) with { Id = Guid.NewGuid().ToString("N") };
        Save(property);
        return property;
    }

    // a hand-written file may say null where the app expects a list or the record
    private static OwnedProperty Whole(OwnedProperty p) => p with
    {
        Record = p.Record ?? new(), Expenses = p.Expenses ?? [], Neighbors = p.Neighbors ?? [],
        SaleChecklist = p.SaleChecklist ?? OwnedProperty.NewSaleChecklist(), Contacts = p.Contacts ?? [], Listings = p.Listings ?? [],
        Buyers = p.Buyers ?? [], FinancingTiers = p.FinancingTiers ?? FinancingTier.Defaults(), Status = p.Status ?? OwnedProperty.Statuses[0],
    };

    private static readonly string[] ReplacedTexts = ["Overview", "LocationOverview", "ListingAd"];
    private static readonly string[] FilledWhenEmpty =
    [
        "ConservativeValue", "MarketValue", "OptimisticValue", "AskingPrice", "NegotiationTarget", "QuickSalePrice", "LotWidthFt", "LotDepthFt",
        "CornerLot", "RoadFrontage", "Easements", "DeedType", "Water", "Sewer", "Electricity", "Gas", "Phone", "GisUrl", "PropertyDetailUrl",
    ];
    // the money, the dates and the identity of the purchase are the user's own records: never taken from pasted research
    private static readonly string[] RecordFields = ["ParcelId", "Address", "City", "County", "State", "Zip", "ZoningCode", "ZoningType", "FloodZone", "Owner", "LegalDescription", "AssessedValue", "PropertyType"];
    private static readonly (string List, string Key)[] AddedLists = [("Neighbors", "Name"), ("Contacts", "Office"), ("Buyers", "Name")];

    public (OwnedProperty Property, int Changes) ApplyResearch(OwnedProperty property, string text)
    {
        // Claude's answer usually has words and a code fence around the JSON
        int start = text.IndexOf('{'), end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("There is no JSON in the copied text.");
        var found = JsonNode.Parse(text[start..(end + 1)]) as JsonObject ?? throw new JsonException("The copied JSON is not an object.");
        var mine = JsonSerializer.SerializeToNode(property, Json)!.AsObject();
        var changes = 0;

        static bool Empty(JsonNode? n) => n is null || n.GetValueKind() is JsonValueKind.Null or JsonValueKind.False
            || (n.GetValueKind() == JsonValueKind.String && n.GetValue<string>() is "" or "Unknown");
        void Fill(JsonObject from, JsonObject into, string name, bool replace)
        {
            if (!from.TryGetPropertyValue(name, out var value) || Empty(value) || (!replace && !Empty(into[name]))) return;
            if (JsonNode.DeepEquals(into[name], value)) return;
            into[name] = value!.DeepClone();
            changes++;
        }

        foreach (var name in ReplacedTexts) Fill(found, mine, name, replace: true);
        foreach (var name in FilledWhenEmpty) Fill(found, mine, name, replace: false);
        if (found["Record"] is JsonObject record)
            foreach (var name in RecordFields) Fill(record, mine["Record"]!.AsObject(), name, replace: false);
        foreach (var (list, key) in AddedLists)
        {
            if (found[list] is not JsonArray rows) continue;
            var have = mine[list]!.AsArray();
            string Name(JsonNode? row) => (row as JsonObject)?[key] is { } n && n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>().Trim() : "";
            foreach (var row in rows)
            {
                if (Name(row).Length == 0 || have.Any(h => string.Equals(Name(h), Name(row), StringComparison.OrdinalIgnoreCase))) continue;
                have.Add(row!.DeepClone());
                changes++;
            }
        }
        // reading it back checks every pasted value has the right type (a price that is text, a utility that is not one of the choices)
        return (Whole(mine.Deserialize<OwnedProperty>(Json)!) with { Id = property.Id }, changes);
    }

    public void Export(OwnedProperty property, string path) => File.WriteAllText(path, JsonSerializer.Serialize(property, Json));

    public void Delete(string id)
    {
        if (File.Exists(PathOf(id)))
            FileSystem.DeleteFile(PathOf(id), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    // GetFileName: an id can never point outside the folder
    private string PathOf(string id) => Path.Combine(folder, Path.GetFileName(id) + ".json");

    /// <summary>Assert-based check (no test project yet); run by tools/RegridSampleCheck.</summary>
    public static void SelfCheck()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AstroValleyOwnedCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonOwnedPropertyStore(dir);
            Debug.Assert(store.List().Count == 0, "no folder yet, no properties");

            var mackinaw = new OwnedProperty
            {
                Record = new PropertyRecord { Address = "2916 Mackinaw St", ParcelId = "20 1952 00000", State = "MI", ZoningCode = "R-2" },
                PurchaseDate = new DateTime(2025, 9, 4), PurchasePrice = 1800m, TaxesAtPurchase = 139.22m, DeedFee = 30m,
                LotWidthFt = 88, LotDepthFt = 120, Water = UtilityStatus.AtStreet,
                Expenses = [new(new DateTime(2026, 6, 1), ExpenseCategory.GrassCutCleanup, 60m, "first cut")],
                Neighbors = [new("Amy L Bunker", "2910 Mackinaw St")],
            };
            store.Save(mackinaw);
            store.Save(new OwnedProperty { Record = new PropertyRecord { Address = "839 S 14th St" }, PurchaseDate = new DateTime(2025, 8, 1) });

            var loaded = store.List();
            var back = loaded[0];
            Debug.Assert(loaded.Count == 2 && back.Id == mackinaw.Id, "most recently purchased first");
            Debug.Assert(back.Record.ParcelId == "20 1952 00000" && back.Record.ZoningCode == "R-2" && back.TotalSpent == 2209.22m && back.LotSqft == 10560
                && back.Water == UtilityStatus.AtStreet && back.Expenses.Single().Note == "first cut" && back.Neighbors.Single().Name == "Amy L Bunker",
                "everything typed comes back");
            var text = File.ReadAllText(Path.Combine(dir, mackinaw.Id + ".json"));
            Debug.Assert(text.Contains("\"AtStreet\"") && text.Contains("\"GrassCutCleanup\"") && !text.Contains("TotalSpent"), "enums saved as words, computed values not saved");

            store.Save(back with { Notes = "listed" });
            Debug.Assert(store.List().Count == 2 && store.List()[0].Notes == "listed" && !Directory.EnumerateFiles(dir, "*.tmp").Any(), "an edit replaces the file");

            File.WriteAllText(Path.Combine(dir, "damaged.json"), "{ not json");
            Debug.Assert(store.List().Count == 2 && File.Exists(Path.Combine(dir, "damaged.json")), "a damaged file is left out of the list and left on disk");

            File.Copy(Path.Combine(dir, mackinaw.Id + ".json"), Path.Combine(dir, "copy.json"));
            Debug.Assert(store.List().Count == 3 && store.List().Select(p => p.Id).Distinct().Count() == 3, "a copied file is its own property");

            var exported = Path.Combine(dir, "export.txt");
            store.Export(back, exported);
            var imported = store.Import(exported);
            Debug.Assert(imported.Id != back.Id && imported.TotalSpent == back.TotalSpent && store.List().Count == 4, "an exported file imports as a new property");
            store.Delete(imported.Id);
            File.WriteAllText(exported, "{ \"Record\": null, \"PurchasePrice\": 500 }");
            var sparse = store.Import(exported);
            Debug.Assert(sparse.PurchaseTotal == 550m && sparse.Record.Address == "" && sparse.Expenses.Count == 0, "a hand-written file with gaps still imports");
            store.Delete(sparse.Id);
            File.WriteAllText(exported, "not a property");
            try { store.Import(exported); Debug.Fail("a file that is not a property is refused"); } catch (JsonException) { }
            Debug.Assert(store.List().Count == 3, "a refused import saves nothing");

            // research pasted back from Claude: marketing text replaces, the rest fills blanks, new people are added
            var before = back with { Overview = "old overview", MarketValue = 12200m, Water = UtilityStatus.AtStreet };
            var (after, changed) = store.ApplyResearch(before, """
                Here is the block for your app:
                ```json
                { "Overview": "A corner lot in Saginaw.", "MarketValue": 7500, "AskingPrice": 9900, "Water": "Connected", "Sewer": "AtStreet", "CornerLot": true,
                  "PurchasePrice": 1, "Id": "hijack", "Expenses": [],
                  "Record": { "ZoningCode": "R-9", "FloodZone": "X", "ParcelId": "other" },
                  "Neighbors": [ { "Name": "amy l bunker" }, { "Name": "Kelly Troy", "MailingAddress": "Jacksonville, FL" } ],
                  "Contacts": [ { "Office": "Register of Deeds", "Phone": "(989) 790-5270" } ],
                  "Buyers": [ { "Name": "Local builder", "Type": "Builder" } ] }
                ```
                """);
            Debug.Assert(after.Overview == "A corner lot in Saginaw." && after.MarketValue == 12200m && after.AskingPrice == 9900m && after.Water == UtilityStatus.AtStreet
                && after.Sewer == UtilityStatus.AtStreet && after.CornerLot, "marketing text is replaced, typed values are kept, blanks are filled");
            Debug.Assert(after.Id == back.Id && after.PurchasePrice == 1800m && after.Expenses.Count == 1 && after.Record.ZoningCode == "R-2" && after.Record.FloodZone == "X" && after.Record.ParcelId == "20 1952 00000",
                "the purchase, the expenses and the identity are never taken from pasted research");
            Debug.Assert(after.Neighbors.Count == 2 && after.Neighbors[1].MailingAddress == "Jacksonville, FL" && after.Contacts.Single().Phone == "(989) 790-5270"
                && after.Buyers.Single().Type == BuyerType.Builder && changed == 8 && before.Neighbors.Count == 1, "new people are added once; the original is untouched");
            Debug.Assert(store.ApplyResearch(after, "{ \"Overview\": \"A corner lot in Saginaw.\" }").Changes == 0, "pasting the same thing twice changes nothing");
            foreach (var bad in new[] { "no json here", "{ \"AskingPrice\": \"about ten thousand\" }", "{ \"Gas\": \"Nearby\" }", "[1,2]" })
                try { store.ApplyResearch(back, bad); Debug.Fail("refused: " + bad); } catch (JsonException) { }

            store.Delete(@"..\..\elsewhere"); // must not throw or leave the folder
            store.Delete("copy");
            Debug.Assert(store.List().Count == 2 && !File.Exists(Path.Combine(dir, "copy.json")), "delete removes one property");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
