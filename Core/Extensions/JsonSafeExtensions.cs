using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Extensions
{
    public static class JsonSafeExtensions
    {
        public static string GetJsonString(this JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                try
                {
                    if (element.TryGetProperty(key, out var prop) && prop.ValueKind != JsonValueKind.Null)
                    {
                        string? result = prop.ValueKind == JsonValueKind.String
                            ? prop.GetString()
                            : prop.GetRawText();

                        return result?.Trim() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[JsonHelper Error] Key: '{key}' string extraction failed. {ex.Message}");
                }
            }
            return string.Empty;
        }

        public static decimal? GetJsonDecimal(this JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                try
                {
                    if (element.TryGetProperty(key, out var prop) && prop.ValueKind != JsonValueKind.Null)
                    {
                        if (prop.ValueKind == JsonValueKind.Number)
                            return prop.GetDecimal();

                        string rawValue = prop.ValueKind == JsonValueKind.String
                            ? prop.GetString() ?? string.Empty
                            : prop.GetRawText();

                        if (string.IsNullOrWhiteSpace(rawValue)) continue;

                        // Clean currency format (strip $, commas, etc.)
                        string cleanValue = Regex.Replace(rawValue, @"[^\d.]", "");
                        if (decimal.TryParse(cleanValue, out decimal result))
                            return result;

                        Debug.WriteLine($"[JsonHelper Warning] Key '{key}' value '{rawValue}' not a valid decimal.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[JsonHelper Error] Key: '{key}' decimal extraction failed. {ex.Message}");
                }
            }
            return null;
        }

        public static double? GetJsonDouble(this JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                try
                {
                    if (element.TryGetProperty(key, out var prop) && prop.ValueKind != JsonValueKind.Null)
                    {
                        if (prop.TryGetDouble(out double val))
                            return val;

                        if (prop.ValueKind == JsonValueKind.String)
                        {
                            string rawValue = prop.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(rawValue) && double.TryParse(rawValue, out double result))
                                return result;
                        }

                        Debug.WriteLine($"[JsonHelper Warning] Key '{key}' could not be converted to double.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[JsonHelper Error] Key: '{key}' double extraction failed. {ex.Message}");
                }
            }
            return null;
        }

        public static string GetFormattedAddress(this JsonElement element)
        {
            try
            {
                if (element.TryGetProperty("formatted_addresses", out var fa) &&
                    fa.ValueKind == JsonValueKind.Array &&
                    fa.GetArrayLength() > 0)
                {
                    var firstEntry = fa[0];
                    if (firstEntry.ValueKind == JsonValueKind.Array)
                    {
                        // Join all parts (Street, City, State, Zip, Country) into one string
                        var parts = firstEntry.EnumerateArray()
                                              .Select(item => item.GetString()?.Trim())
                                              .Where(s => !string.IsNullOrEmpty(s));

                        return string.Join(" ", parts);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonHelper Error] Full address string extraction failed: {ex.Message}");
            }

            // Fallback to headline if the array extraction fails
            return element.GetJsonString("headline");
        }
    }
}