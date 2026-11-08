using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AstroValley.Domain.Utilities;

/// <summary>
/// Builds the web address of an aerial photo of a parcel from Esri World Imagery (free public service, no key), plus the
/// parcel outline as WPF path data in the picture's own pixels. Same geometry as aerialHtml() in pinpopup.js.
/// </summary>
public static class AerialImage
{
    public const string ServiceUrl = "https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer";

    public record Result(string Url, string OutlinePath);

    /// <summary>Null when the coordinates are missing. <paramref name="parcelLines"/> is the parcel GeoJSON, or empty for a ring at the point.</summary>
    public static Result? Build(string? lat, string? lon, string? parcelLines, int w, int h)
    {
        if (lat.TryParseDouble() is not { } la || lon.TryParseDouble() is not { } lo) return null;

        var rings = Rings(parcelLines);
        var pts = rings.SelectMany(r => r).Select(p => Merc(p.Lon, p.Lat)).ToList();
        if (pts.Count == 0) pts.Add(Merc(lo, la));

        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        // parcel plus margin, never closer than ~120 m wide
        var mpp = Math.Max(Math.Max((maxX - minX) * 1.6 / w, (maxY - minY) * 1.6 / h), 120.0 / w);
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, x0 = cx - mpp * w / 2, y1 = cy + mpp * h / 2;

        var url = FormattableString.Invariant(
            $"{ServiceUrl}/export?bbox={x0},{cy - mpp * h / 2},{cx + mpp * w / 2},{y1}&bboxSR=3857&imageSR=3857&size={w * 2},{h * 2}&format=jpg&f=image");

        var path = new StringBuilder();
        foreach (var ring in rings.Where(r => r.Count > 1))
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var m = Merc(ring[i].Lon, ring[i].Lat);
                path.Append(FormattableString.Invariant($"{(i == 0 ? 'M' : 'L')}{(m.X - x0) / mpp:0.#},{(y1 - m.Y) / mpp:0.#} "));
            }
            path.Append("Z ");
        }
        if (path.Length == 0) // no outline: a ring at the point
            path.Append(FormattableString.Invariant($"M{w / 2.0 - 8},{h / 2.0} A8,8 0 1 0 {w / 2.0 + 8},{h / 2.0} A8,8 0 1 0 {w / 2.0 - 8},{h / 2.0}"));

        return new Result(url, path.ToString().Trim());
    }

    private static (double X, double Y) Merc(double lon, double lat) =>
        (6378137 * lon * Math.PI / 180, 6378137 * Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)));

    // GeoJSON: an array whose first item is a number is a [lon, lat] point; an array of points is a ring; anything else is nesting
    private static List<List<(double Lon, double Lat)>> Rings(string? geoJson)
    {
        var rings = new List<List<(double Lon, double Lat)>>();
        if (string.IsNullOrWhiteSpace(geoJson)) return rings;
        try { using var doc = JsonDocument.Parse(geoJson); Walk(doc.RootElement, rings); }
        catch (JsonException) { rings.Clear(); }
        return rings;
    }

    private static void Walk(JsonElement e, List<List<(double Lon, double Lat)>> rings)
    {
        if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) Walk(p.Value, rings); return; }
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() == 0) return;
        var first = e[0];
        if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() >= 2 && first[0].ValueKind == JsonValueKind.Number)
            rings.Add(e.EnumerateArray().Where(p => p.GetArrayLength() >= 2).Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList());
        else
            foreach (var item in e.EnumerateArray()) Walk(item, rings);
    }
}
