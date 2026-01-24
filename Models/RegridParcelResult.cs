using AstroValleyAssistant.Models.Domain;

namespace AstroValleyAssistant.Models
{
    /// <summary>
    /// Represents the result of scraping a single parcel from Regrid.
    /// </summary>
    public record RegridParcelResult
    {
        public bool NotFound { get; init; }
        public bool IsMultiple { get; init; }
        public List<RegridMatch> Matches { get; init; } = new();
        public PropertyRecord? Record { get; init; }
        public string Query { get; init; } = string.Empty;
        public Exception? Error { get; init; }
    }
}