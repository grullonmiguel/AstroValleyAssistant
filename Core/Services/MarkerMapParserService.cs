using AstroValleyAssistant.Models;
using ClosedXML.Excel;
using System.IO;
using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Service responsible for converting various input strings and files into MarkerLocation objects.
    /// </summary>
    public class MarkerMapParserService : IMarkerMapParserService
    {
        // Regex to match: Optional Name, then Latitude, then Longitude (comma or tab separated)
        // Matches patterns like: "Main St, 40.712, -74.006" or just "40.712 -74.006"
        private static readonly Regex LatLonRegex = new(
            @"(?:(?<name>.+?)[,\t])?\s*(?<lat>-?\d+\.\d+)\s*[,\t\s]\s*(?<lon>-?\d+\.\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses raw text (e.g., from clipboard or a multi-line textbox).
        /// </summary>
        public IEnumerable<MarkerLocation> ParseRawText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var match = LatLonRegex.Match(line);
                if (match.Success)
                {
                    yield return new MarkerLocation
                    {
                        Name = match.Groups["name"].Success ? match.Groups["name"].Value.Trim() : "Manual Entry",
                        Latitude = double.Parse(match.Groups["lat"].Value),
                        Longitude = double.Parse(match.Groups["lon"].Value)
                    };
                }
            }
        }

        /// <summary>
        /// Determines file type and delegates to the appropriate parser.
        /// </summary>
        public async Task<IEnumerable<MarkerLocation>> ParseFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return Enumerable.Empty<MarkerLocation>();

            string extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".csv" => await ParseCsvAsync(filePath),
                ".xlsx" => await ParseExcelAsync(filePath),
                _ => throw new NotSupportedException($"File extension {extension} is not supported.")
            };
        }

        private async Task<IEnumerable<MarkerLocation>> ParseCsvAsync(string filePath)
        {
            // For simplicity and to avoid extra dependencies, we use the text parser on the file content.
            // For production CSVs with complex quoting, CsvHelper is recommended.
            string content = await File.ReadAllTextAsync(filePath);
            return ParseRawText(content);
        }

        private Task<IEnumerable<MarkerLocation>> ParseExcelAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var locations = new List<MarkerLocation>();
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Assume header row

                foreach (var row in rows)
                {
                    // Basic assumption: Col 1 = Name, Col 2 = Lat, Col 3 = Lon
                    if (double.TryParse(row.Cell(2).GetString(), out double lat) &&
                        double.TryParse(row.Cell(3).GetString(), out double lon))
                    {
                        locations.Add(new MarkerLocation
                        {
                            Name = row.Cell(1).GetString(),
                            Latitude = lat,
                            Longitude = lon
                        });
                    }
                }
                return (IEnumerable<MarkerLocation>)locations;
            });
        }
    }
}
