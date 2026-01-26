using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models.Domain;
using System.Text;
using System.Windows;

namespace AstroValleyAssistant.Core.Utilities
{
    public static class ClipboardFormatter
    {
        /// <summary>
        /// Formats a single PropertyRecord into a tab-separated block.
        /// </summary>
        public static string FormatForGoogleSheets(PropertyRecord record)
        {
            var lines = new List<string>
        {
            $"Parcel ID\t{record.ParcelId}",
            $"Address\t{record.Address}",
            $"Owner\t{record.Owner}",
            $"Acres\t{record.Acres}",
            $"City\t{record.City}",
            $"Zip\t{record.Zip}",
            $"Zoning Code\t{record.ZoningCode}",
            $"Zoning Type\t{record.ZoningType}",
            $"Flood Zone\t{record.FloodZone}",
            $"Coordinates\t{record.GeoCoordinates}",
            $"Regrid URL\t{record.RegridUrl}",
            $"Google Maps\t{UrlBuilder.BuildGoogleMapsUrl(record)}",
            $"FEMA Flood\t{UrlBuilder.BuildFemaFloodUrl(record)}"
        };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Formats an entire list of PropertyRecords into a multi-row,
        /// tab-separated table suitable for Google Sheets.
        /// </summary>
        public static string FormatAllForGoogleSheets(IEnumerable<PropertyRecord> records)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join("\t", new[]
            {
            "Zoning Type",
            "Zoning Code",
            "City",
            "Parcel ID",
            "Regrid URL",
            "Owner",
            "Appraiser",
            "Assessed",
            "Starting Bid",
            "Acres",
            "Address",
            "Flood Zone",
            "FEMA Flood",
            "Google Maps",
            "Coordinates"
        }));

            // Data rows
            foreach (var r in records)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
                r.ZoningType,
                r.ZoningCode,
                r.City,
                r.ParcelId,
                FormatGoogleSheetsUrRL(r.RegridUrl, "Regrid"),
                r.Owner,
                FormatGoogleSheetsUrRL(r.AppraiserUrl, "Appraiser"),
                r.AssessedValue?.ToString() ?? "",
                r.OpeningBid.ToString() ?? "",
                r.Acres?.ToString() ?? "",
                FormatGoogleSheetsUrRL(UrlBuilder.BuildGoogleMapsUrl(r) ?? "", r.Address),
                r.Address,
                FormatGoogleSheetsUrRL(UrlBuilder.BuildFemaFloodUrl(r) ?? "" ?? "",r.FloodZone),
                r.GeoCoordinates,
            }));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Copies the entire list to the clipboard.
        /// </summary>
        public static void CopyAllToClipboard(IEnumerable<PropertyRecord> records)
        {
            Clipboard.SetText(FormatAllForGoogleSheets(records));
        }


        /// <summary>
        /// Creates a Google Sheets compatible hyperlink
        /// </summary>
        private static string FormatGoogleSheetsUrRL(string url, string alias) => string.IsNullOrWhiteSpace(url)
            ? string.Empty : $"=HYPERLINK(\"{url.Replace("\"", "\"\"")}\", \"{alias}\")";
    }
}