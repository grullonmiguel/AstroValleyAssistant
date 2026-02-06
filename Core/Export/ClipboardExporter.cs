using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models.Domain;
using System.Text;
using System.Windows;

namespace AstroValleyAssistant.Core.Export
{
    /// <summary>
    /// Formats property data into a tab-separated table with Google Sheets formulas
    /// and copies the resulting string to the system clipboard.
    /// </summary>
    public class ClipboardExporter : IExporter<IEnumerable<PropertyRecord>, string?>
    {
        /// <summary>
        /// Transforms records into a TSV string and updates the clipboard.
        /// </summary>
        /// <param name="records">The collection of property data to export.</param>
        /// <param name="destination">Unused for clipboard; passed as null.</param>
        public Task ExportAsync(IEnumerable<PropertyRecord> records, string? destination = null)
        {
            var sb = new StringBuilder();

            // Standard header row matching your original implementation
            sb.AppendLine(string.Join("\t", new[]
            {
                "Zoning Type", "Zoning Code", "City", "Parcel ID", "Regrid URL",
                "Owner", "Appraiser", "Assessed", "Starting Bid", "Acres",
                "Address", "Flood Zone", "Coordinates"
            }));

            foreach (var r in records)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
                    r.ZoningType,
                    r.ZoningCode,
                    r.City,
                    r.ParcelId,
                    FormatGoogleSheetsUrl(r.RegridUrl, "Regrid"),
                    r.Owner,
                    FormatGoogleSheetsUrl(r.AppraiserUrl, "Appraiser"),
                    r.AssessedValue?.ToString() ?? string.Empty,
                    r.OpeningBid.ToString(),
                    r.Acres?.ToString() ?? string.Empty,
                    FormatGoogleSheetsUrl(UrlBuilder.BuildGoogleMapsUrl(r), r.Address),
                    FormatGoogleSheetsUrl(UrlBuilder.BuildFemaFloodUrl(r), r.FloodZone),
                    r.GeoCoordinates
                }));
            }

            Clipboard.SetText(sb.ToString());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Encapsulates the Google Sheets HYPERLINK formula logic.
        /// </summary>
        private string FormatGoogleSheetsUrl(string? url, string? alias)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            // Escape quotes within the URL to prevent breaking the formula string
            string escapedUrl = url.Replace("\"", "\"\"");
            return $"=HYPERLINK(\"{escapedUrl}\", \"{alias}\")";
        }

        /// <summary>
        /// Encapsulates the Google Sheets IMAGE formula logic using Mode 1 (maintain aspect ratio).
        /// </summary>
        private string FormatGoogleSheetsImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            string escapedUrl = url.Replace("\"", "\"\"");
            return $"=IMAGE(\"{escapedUrl}\", 1)";
        }
    }
}