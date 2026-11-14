using System.IO;
using System.Text.Json;
using AstroValley.Domain.Models;

namespace AstroValley.Presentation.Services;

/// <summary>Builds the standalone pins map (Assets/pinsexport.html) from a list of pins; shared by the Home and Comps exports.</summary>
public static class PinsHtml
{
    public static string ToJson(IEnumerable<MarkerLocation> pins) => JsonSerializer.Serialize(pins, new JsonSerializerOptions { PropertyNamingPolicy = null });

    public static async Task<string> BuildAsync(IEnumerable<MarkerLocation> pins, bool imagery, bool picker = false)
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets", "Map");
        // Default JSON encoding escapes '<' etc., so the data can't break out of the <script> tag.
        var pinsJson = ToJson(pins);

        return (await File.ReadAllTextAsync(Path.Combine(assets, "pinsexport.html")))
            .Replace("/*POPUP_CSS*/", await File.ReadAllTextAsync(Path.Combine(assets, "pinpopup.css")))
            .Replace("/*POPUP_JS*/", await File.ReadAllTextAsync(Path.Combine(assets, "pinpopup.js")))
            .Replace("{{IMAGERY}}", imagery ? "true" : "false")
            .Replace("{{PICKER}}", picker ? "true" : "false") // picker: popups get a Keep/Exclude button and a Done button posts the result to the app
            .Replace("{{PINS_JSON}}", pinsJson); // last, so pin text can never be mistaken for a placeholder
    }
}
