using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Extensions
{
    public static class StringExtensions
    {
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