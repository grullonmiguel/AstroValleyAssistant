using System.Diagnostics;
using System.Text.Json;
using AstroValley.Application.Interfaces.Data;
using AstroValley.Domain.Models;
using Microsoft.VisualBasic.FileIO;

namespace AstroValley.Infrastructure.Data;

/// <summary>
/// Pin sets as JSON files in one folder, "{set name}.json".
/// shortcut: reads and rewrites a whole set on every save, on the caller's thread; fine for auction-sized sets (hundreds of pins),
/// move to a background queue if a set reaches several thousand.
/// </summary>
public sealed class JsonPinStore(string folder) : IPinStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null, WriteIndented = true };

    public string Folder => folder;

    public void Save(IEnumerable<MarkerLocation> pins, bool restoreWork = false)
    {
        Directory.CreateDirectory(folder);
        foreach (var set in pins.GroupBy(p => p.SetName))
        {
            var saved = new Dictionary<string, MarkerLocation>();
            foreach (var pin in Read(set.Key, setAsideIfUnreadable: true)) saved[pin.Key] = pin;
            foreach (var pin in set)
            {
                if (restoreWork && saved.TryGetValue(pin.Key, out var earlier)) pin.RestoreWorkFrom(earlier);
                saved[pin.Key] = pin;
            }

            // written beside the file and swapped in, so a crash mid-write leaves the previous save intact
            var path = PathOf(set.Key);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(saved.Values, Json));
            File.Move(path + ".tmp", path, overwrite: true);
        }
    }

    public IReadOnlyList<SavedPinSet> List()
    {
        if (!Directory.Exists(folder)) return [];
        var sets = new List<SavedPinSet>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var pins = Read(name, setAsideIfUnreadable: false);
            if (pins.Count == 0) continue;
            var first = pins[0];
            sets.Add(new SavedPinSet(name, first.State, first.County, first.AuctionDate, pins.Count, File.GetLastWriteTime(file)));
        }
        return [.. sets.OrderByDescending(s => s.SavedAt)];
    }

    public IReadOnlyList<MarkerLocation> Load(string setName) => Read(setName, setAsideIfUnreadable: false);

    public void Delete(string setName)
    {
        if (File.Exists(PathOf(setName)))
            FileSystem.DeleteFile(PathOf(setName), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    // GetFileName: a set name can never point outside the folder
    private string PathOf(string setName) => Path.Combine(folder, Path.GetFileName(setName) + ".json");

    private List<MarkerLocation> Read(string setName, bool setAsideIfUnreadable)
    {
        var path = PathOf(setName);
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<MarkerLocation>>(File.ReadAllText(path), Json) ?? [];
        }
        catch (JsonException ex)
        {
            // a damaged file must not block saving, and must not be silently overwritten either: keep it beside the new one
            Trace.TraceWarning($"Pin set '{setName}' could not be read: {ex.Message}");
            if (setAsideIfUnreadable) File.Move(path, path + ".unreadable", overwrite: true);
            return [];
        }
    }

    /// <summary>Assert-based check (no test project yet); run by tools/RegridSampleCheck.</summary>
    public static void SelfCheck()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AstroValleyPinStoreCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonPinStore(dir);
            MarkerLocation Pin(string id, DateTime? auction = null) => new()
                { ParcelID = id, Latitude = 25.8, Longitude = -80.2, State = "FL", County = "Miami-Dade County", AuctionDate = auction };
            var december = new DateTime(2026, 12, 10);
            Debug.Assert(Pin("1", december).SetName == "FL_Miami-Dade_2026-12-10" && Pin("1").SetName == "FL_Miami-Dade_no-auction", "one set per auction");
            Debug.Assert(new MarkerLocation { County = "A/B County" }.SetName == "A-B_no-auction" && new MarkerLocation().SetName == "Unsorted_no-auction", "a set name is a safe file name");

            var worked = Pin("1", december);
            worked.Notes = "roof gone";
            worked.Status = "Interested";
            store.Save([worked, Pin("2", december), Pin("3")]);
            Debug.Assert(store.List().Count == 2 && store.List().Single(s => s.AuctionDate == december).PinCount == 2, "pins grouped by auction");

            var scrapedAgain = Pin("1", december);
            store.Save([scrapedAgain], restoreWork: true);
            var reloaded = store.Load("FL_Miami-Dade_2026-12-10");
            Debug.Assert(scrapedAgain.Notes == "roof gone" && reloaded.Count == 2 && reloaded.Single(p => p.ParcelID == "1").Status == "Interested",
                "scraping again keeps the research and adds no duplicate");

            scrapedAgain.Notes = "sold";
            store.Save([scrapedAgain]);
            Debug.Assert(store.Load("FL_Miami-Dade_2026-12-10").Single(p => p.ParcelID == "1").Notes == "sold", "an edit replaces the saved pin");

            File.WriteAllText(Path.Combine(dir, "FL_Miami-Dade_no-auction.json"), "{ not json");
            Debug.Assert(store.List().Count == 1, "a damaged file is left out of the list");
            store.Save([Pin("4")]);
            Debug.Assert(File.Exists(Path.Combine(dir, "FL_Miami-Dade_no-auction.json.unreadable")) && store.Load("FL_Miami-Dade_no-auction").Count == 1,
                "a damaged file is set aside, not overwritten");
            Debug.Assert(store.Load(@"..\..\elsewhere").Count == 0, "a set name stays inside the folder");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
