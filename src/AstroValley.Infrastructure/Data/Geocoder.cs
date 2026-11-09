using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstroValley.Application.Interfaces.Data;

namespace AstroValley.Infrastructure.Data;

/// <summary>
/// US Census one-line geocoder first (needs a real street number), then OpenStreetMap Nominatim with the number stripped so a
/// vacant lot ("0 Usina St", "TBD Dogwood Dr") lands on its street. Results, including misses, are kept in a JSON file.
/// shortcut: a lot placed by street name is only roughly right (somewhere on that street), and the file cache never expires.
/// </summary>
public class Geocoder : IGeocoder
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex LotNumber = new(@"^\s*(0|x+|tbd|lot|\d+[-\d]*)\s+|\b(unit|lot|apt|#)\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant", "geocache.json");
    private readonly Dictionary<string, double[]?> _cache;
    private DateTime _lastNominatim = DateTime.MinValue;

    public Geocoder()
    {
        try { _cache = JsonSerializer.Deserialize<Dictionary<string, double[]?>>(File.ReadAllText(_cachePath)) ?? []; }
        catch { _cache = []; } // no file yet, or unreadable: start empty
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AstroValleyAssistant/1.0 (personal research tool)"); // Nominatim requires an identifying agent
        return http;
    }

    public async Task<(double Lat, double Lon)?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var key = address.Trim().ToLowerInvariant();
        if (key.Length == 0) return null;
        if (_cache.TryGetValue(key, out var hit)) return hit is null ? null : (hit[0], hit[1]);

        var failed = false; // a network error is not a miss: do not remember it
        (double, double)? found = null;
        try { found = await Census(address, ct); } catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { failed = true; }
        if (found is null)
        {
            try { found = await Nominatim(address, ct); } catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { failed = true; }
        }
        if (found is null && failed) return null;

        _cache[key] = found is { } f ? [f.Item1, f.Item2] : null;
        try { Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!); File.WriteAllText(_cachePath, JsonSerializer.Serialize(_cache)); }
        catch { /* cache only; keep going */ }
        return found;
    }

    public async Task<string?> ZipAsync(double lat, double lon, CancellationToken ct = default)
    {
        var key = FormattableString.Invariant($"zip:{lat:F4},{lon:F4}");
        if (_cache.TryGetValue(key, out var hit)) return hit is null ? null : ((int)hit[0]).ToString("D5"); // the ZIP is kept as a number in the shared cache; D5 restores a leading zero
        try
        {
            var wait = _lastNominatim.AddMilliseconds(1100) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _lastNominatim = DateTime.UtcNow;
            var url = FormattableString.Invariant($"https://nominatim.openstreetmap.org/reverse?format=json&zoom=18&lat={lat}&lon={lon}");
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
            var zip = doc.RootElement.TryGetProperty("address", out var a) && a.TryGetProperty("postcode", out var p) ? Regex.Match(p.GetString() ?? "", @"\d{5}").Value : "";
            _cache[key] = zip.Length == 5 ? [int.Parse(zip)] : null;
            try { Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!); File.WriteAllText(_cachePath, JsonSerializer.Serialize(_cache)); } catch { }
            return zip.Length == 5 ? zip : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { return null; }
    }

    private static async Task<(double, double)?> Census(string address, CancellationToken ct)
    {
        var url = "https://geocoding.geo.census.gov/geocoder/locations/onelineaddress?benchmark=Public_AR_Current&format=json&address=" + Uri.EscapeDataString(address);
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
        var matches = doc.RootElement.GetProperty("result").GetProperty("addressMatches");
        if (matches.GetArrayLength() == 0) return null;
        var c = matches[0].GetProperty("coordinates");
        return (c.GetProperty("y").GetDouble(), c.GetProperty("x").GetDouble());
    }

    private async Task<(double, double)?> Nominatim(string address, CancellationToken ct)
    {
        var wait = _lastNominatim.AddMilliseconds(1100) - DateTime.UtcNow; // their policy: at most one request per second
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _lastNominatim = DateTime.UtcNow;

        // "0 Usina Unit 256189, Interlachen, FL 32148" -> street "Usina" + state + ZIP. Tested: this form places lots that a free-text query misses,
        // and the state is needed or a common street name can land in another state.
        var parts = Regex.Match(address, @"^(.*?),\s*[^,]+,\s*([A-Za-z]{2})\s+(\d{5})");
        var street = Regex.Replace(LotNumber.Replace(parts.Success ? parts.Groups[1].Value : address, " "), @"\s+", " ").Trim();
        var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=us&" + (parts.Success && street.Length > 0
            ? $"street={Uri.EscapeDataString(street)}&state={parts.Groups[2].Value}&postalcode={parts.Groups[3].Value}"
            : "q=" + Uri.EscapeDataString(street));
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
        if (doc.RootElement.GetArrayLength() == 0) return null;
        var r = doc.RootElement[0];
        return (double.Parse(r.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture), double.Parse(r.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture));
    }
}
