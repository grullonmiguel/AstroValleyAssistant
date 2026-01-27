using System.Globalization;
using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Normalizes a column header by lowercasing and removing spaces and underscores.
        /// Used for alias matching during import.
        /// </summary>
        public static string NormalizeHeaders(this string header)
        {
            return header.ToLowerInvariant()
                         .Replace(" ", "")
                         .Replace("_", "");
        }

        /// <summary>
        /// Retrieves a value from a preview row using a column map.
        /// Returns null if the key is missing or empty.
        /// </summary>
        public static string? GetMappedValue(this Dictionary<string, string> row, Dictionary<string, string> map, string canonicalKey)
        {
            return map.TryGetValue(canonicalKey, out var columnKey) && row.TryGetValue(columnKey, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Checks if a detected column matches any alias after normalization.
        /// </summary>
        //public static bool MatchesAlias(this string detected, IEnumerable<string> aliases)
        //{
        //    return aliases.Any(alias => alias.Normalize() == detected);
        //}
        public static bool MatchesAlias(this string detected, IEnumerable<string> aliases)
        {
            var normalizedDetected = detected.NormalizeHeaders();
            return aliases.Any(alias => alias.NormalizeHeaders() == normalizedDetected);
        }

        /// <summary>
        /// Attempts to parse a string into a double using invariant culture.
        /// Returns null if parsing fails.
        /// </summary>
        public static double? TryParseDouble(this string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            if (double.TryParse(input, out value))
                return value;

            return null;
        }

        /// <summary>
        /// Attempts to parse a string into a decimal using invariant culture.
        /// Cleans currency symbols and formatting before parsing.
        /// Returns null if parsing fails.
        /// </summary>
        public static decimal? TryParseDecimal(this string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Replace("$", "")
                         .Replace(",", "")
                         .Trim();

            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            if (decimal.TryParse(input, out value))
                return value;

            return null;
        }


        /// <summary>
        /// Converts a coordinate string (e.g., "29.691032, 20-82.353909") 
        /// into DMS format: 29°41'27.7"N 82°21'14.1"W
        /// </summary>
        public static string ToDmsCoordinates(this string? coordinateString)
        {
             if (string.IsNullOrWhiteSpace(coordinateString)) return string.Empty;

            // Step 1: Clean the string. 
            // Handles the "20-" prefix issue and ensures only numbers, dots, commas, and signs remain.
            string cleaned = coordinateString.Replace("20-", "-");
            var matches = Regex.Matches(cleaned, @"-?\d+\.\d+");

            if (matches.Count < 2) return string.Empty;

            if (double.TryParse(matches[0].Value, out double lat) &&
                double.TryParse(matches[1].Value, out double lon))
            {
                return $"{FormatDms(lat, true)} {FormatDms(lon, false)}";
            }

            return string.Empty;
        }

        private static string FormatDms(double decimalDegree, bool isLatitude)
        {
            var absDegree = Math.Abs(decimalDegree);
            var degrees = (int)Math.Floor(absDegree);
            var minutes = (int)Math.Floor((absDegree - degrees) * 60);
            var seconds = Math.Round(((absDegree - degrees) * 60 - minutes) * 60, 1);

            string direction = isLatitude
                ? (decimalDegree >= 0 ? "N" : "S")
                : (decimalDegree >= 0 ? "E" : "W");

            return $"{degrees}°{minutes}'{seconds}\"{direction}";
        }
    }
}