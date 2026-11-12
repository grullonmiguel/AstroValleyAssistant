using System.Diagnostics;
using System.Text.Json;
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
                    properties.Add(p with { Id = Path.GetFileNameWithoutExtension(file) }); // the file name is the identity, also for a file dropped in by hand
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
