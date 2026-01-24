using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Models.Domain;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Centralized helper for building external URLs such as Google Maps,
    /// FEMA Flood Maps, Regrid, and Appraiser links.
    /// </summary>
    public static class UrlBuilder
    {
        public static string BuildAppraiserUrl(PropertyRecord record)
            => record.AppraiserUrl;

        public static string BuildRegridSearchUrl(string parcelId)
            => $"https://app.regrid.com/search?query={Uri.EscapeDataString(parcelId)}&context=/us";

        public static string? BuildGoogleMapsUrl(PropertyRecord record)
        {
            string? dms = record.GeoCoordinates.ToDmsCoordinates();
            string? query = dms ?? (!string.IsNullOrWhiteSpace(record.Address)
                ? Uri.EscapeDataString(record.Address)
                : null);

            return query == null
                ? null
                : $"https://www.google.com/maps/search/?api=1&query={query}";
        }

        public static string? BuildFemaFloodUrl(PropertyRecord record)
        {
            string? dms = record.GeoCoordinates.ToDmsCoordinates();
            string? query = dms ?? (!string.IsNullOrWhiteSpace(record.Address)
                ? Uri.EscapeDataString(record.Address)
                : null);

            if (query == null)
                return null;

            return dms != null
                ? $"https://msc.fema.gov/portal/search?AddressQuery={query}"
                : $"https://msc.fema.gov/portal/search?address={query}";
        }
    }
}
