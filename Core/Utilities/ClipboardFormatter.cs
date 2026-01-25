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
            "Parcel ID",
            "Address",
            "Owner",
            "Acres",
            "City",
            "Zip",
            "Zoning Code",
            "Zoning Type",
            "Flood Zone",
            "Coordinates",
            "Regrid URL",
            "Google Maps",
            "FEMA Flood"
        }));

            // Data rows
            foreach (var r in records)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
                r.ParcelId,
                r.Address,
                r.Owner,
                r.Acres?.ToString() ?? "",
                r.City,
                r.Zip,
                r.ZoningCode,
                r.ZoningType,
                r.FloodZone,
                r.GeoCoordinates,
                r.RegridUrl,
                UrlBuilder.BuildGoogleMapsUrl(r) ?? "",
                UrlBuilder.BuildFemaFloodUrl(r) ?? ""
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
    }
}