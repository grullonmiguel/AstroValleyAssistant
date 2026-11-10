namespace AstroValley.Domain.DueDiligence;

/// <summary>
/// A record office's website in one jurisdiction. Office matches a checklist office (see <see cref="Offices"/>), so the link sits with its checklist lines.
/// "{parcel}" in the template is the parcel number where the site takes one in its address; the rest open the search page.
/// </summary>
public record RecordOfficeLink(string Office, string Title, string UrlTemplate, string? Hint = null)
{
    private const string ParcelToken = "{parcel}";

    /// <summary>The address for one parcel, or null when the site needs a parcel number and there is none.</summary>
    public string? Url(string? parcelId)
    {
        if (!UrlTemplate.Contains(ParcelToken)) return UrlTemplate;
        // shortcut: sends letters and digits only ("01-3122-052-2400" -> "0131220522400"), add a second token when a county's site wants the dashes
        var parcel = string.Concat((parcelId ?? "").Where(char.IsLetterOrDigit));
        return parcel.Length == 0 ? null : UrlTemplate.Replace(ParcelToken, parcel);
    }
}
