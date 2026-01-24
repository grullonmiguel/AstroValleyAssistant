using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Services
{
    public class RegridClient : IRegridClient
    {
        private readonly HttpClient _httpClient;

        public RegridClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
           
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8"); 
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://regrid.com/"); 
        }

        public async Task<bool> AuthenticateAsync(string email, string password, CancellationToken ct)
        {
            try
            {
                // 1. Handshake: Get the login page with browser-like headers
                var response = await _httpClient.GetAsync("https://app.regrid.com/users/sign_in", ct);
                response.EnsureSuccessStatusCode();

                var loginPageHtml = await response.Content.ReadAsStringAsync(ct);

                // 2. Extract CSRF token using Regex
                var match = Regex.Match(loginPageHtml, "name=\"authenticity_token\" value=\"([^\"]+)\"");
                if (!match.Success) return false;

                string csrfToken = match.Groups[1].Value;

                // 3. Post credentials
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user[email]", email),
                    new KeyValuePair<string, string>("user[password]", password),
                    new KeyValuePair<string, string>("authenticity_token", csrfToken)
                });

                var postResponse = await _httpClient.PostAsync("https://app.regrid.com/users/sign_in", content, ct);
                return postResponse.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                 Debug.WriteLine($"Auth Network Error: {ex.Message}");
                return false;
            }
        }

        public async Task<RegridSearchResult?> GetPropertyDetailsAsync(string query, CancellationToken ct = default)
        {
            try
            {
                // 1. Initial Search (Replaces browser navigation to the search URL)
                string searchUrl = $"https://app.regrid.com/search?query={Uri.EscapeDataString(query)}&context=/us";
                string searchHtml = await GetWithRetryAsync(searchUrl, ct);

                // Check if matches were found using your h4 logic
                var match = Regex.Match(searchHtml, @"Found (\d+) matches", RegexOptions.IgnoreCase);
                int matchCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;

                // Default is NotFound
                if (matchCount == 0)
                    return new RegridSearchResult();

                // Multiple matches Found
                if (matchCount > 1)
                {
                    var matches = ParseRegridMatchesAsync(searchHtml);
                    return new RegridSearchResult { IsMultiple = true, Matches = matches.Result };
                }

                // Extract the parcel path from the 'hits' JS variable
                string parcelPath = Regex.Match(searchHtml, @"""category"":""parcel"",""path"":""([^""]+)""").Groups[1].Value;

                if (string.IsNullOrEmpty(parcelPath)) return null;

                // THE "CLICK": Adding .json to the path replicates the JS data fetch
                string detailUrl = $"https://app.regrid.com{parcelPath}.json";
                string detailJson = await GetWithRetryAsync(detailUrl, ct);

                // Construct the browser-friendly URL for the UI record
                string browserUrl = $"https://app.regrid.com/us#t=property&p={parcelPath}";

                var returnRecord = ParseRegridJson(detailJson, browserUrl);
                return new RegridSearchResult { Record = returnRecord };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extraction Error: {ex.Message}");
                return null;
            }
        }

        private PropertyRecord ParseRegridJson(string json, string regridUrl)
        {
            using var doc = JsonDocument.Parse(json);

            // Web app internal JSON uses "fields" as the primary data container
            if (!doc.RootElement.TryGetProperty("fields", out var f)) return null;

            LogAvailableKeys(f);

            // Get formatted address
            var fullAddress = doc.RootElement.GetFormattedAddress();
            // If formatted addres was not provided, get it from the address tab
            if (string.IsNullOrWhiteSpace(fullAddress)) fullAddress = f.GetJsonString("address");
            // Final fallback to get the address from the 'headline' property
            if (string.IsNullOrEmpty(fullAddress)) fullAddress = doc.RootElement.GetJsonString("headline");

            return new PropertyRecord
            {
                RegridUrl =         regridUrl,
                ParcelId =          f.GetJsonString("parcelnumb", "parcelid", "lowparcelid"),
                Address =           fullAddress,
                City =              f.GetJsonString("scity", "municipality"),
                Zip =               f.GetJsonString("szip", "zipcode"),
                Acres =             f.GetJsonDouble("ll_gisacre", "acres"),
                Owner =             f.GetJsonString("owner", "eo_owner"),
                ZoningCode =        f.GetJsonString("zoning", "zoning_description"),
                ZoningType =        f.GetJsonString("zoning_type", "zoning_subtype" , "usedesc" ),
                GeoCoordinates =    $"{f.GetJsonString("lat")}, {f.GetJsonString("lon")}",
                ElevationHigh =     f.GetJsonString("highest_parcel_elevation"),
                ElevationLow =      f.GetJsonString("lowest_parcel_elevation"),
                FloodZone =         f.GetJsonString("fema_flood_zone", "fema_flood_zone_subtype", "fema_nri_risk_rating" ),
                AssessedValue =     GetAssessedValue(f),
                BirdseyeUrl =       doc.RootElement.GetJsonString("birdseye")
            };
        }

        private decimal? GetAssessedValue(JsonElement fields)
        {
            // 1. Check if the value type is 'ASSESSED'
            if (fields.TryGetProperty("parvaltype", out var typeProp))
            {
                string typeValue = typeProp.GetString() ?? string.Empty;

                // Only proceed if this is an assessed value 
                if (typeValue.Equals("ASSESSED", StringComparison.OrdinalIgnoreCase) ||
                    typeValue.Equals("MARKET", StringComparison.OrdinalIgnoreCase))
                {
                    // 2. Hunt for the numerical value using the discovered 'parval' key 
                    if (fields.TryGetProperty("parval", out var valProp))
                    {
                        if (valProp.ValueKind == JsonValueKind.Number)
                             return valProp.GetDecimal();
                        
                        string raw = valProp.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            // Clean and parse currency format [cite: 45]
                            string clean = Regex.Replace(raw, @"[^\d.]", "");
                            if (decimal.TryParse(clean, out decimal result))
                                 return result;
                }
                    }
                }
            }

            // 3. Fallback to standard labels if 'parvaltype' logic isn't present [cite: 66]
            return fields.GetJsonDecimal("total_value", "ll_val_asmt", "total_parcel_value");
        }

        private void LogAvailableKeys(JsonElement fieldsObject)
        {
            // This will print every key available for the parcel in the Debug window
            foreach (var property in fieldsObject.EnumerateObject())
                Debug.WriteLine($"Found Key: {property.Name} | Value: {property.Value}");
        }

        private async Task<string> GetWithRetryAsync(string url, CancellationToken ct)
        {
            int retryCount = 0;
            while (retryCount < 3)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, ct);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(ct);
                }
                catch when (retryCount < 3)
                {
                    retryCount++;
                    // Do not spam the server
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), ct);
                }
            }
            return string.Empty;
        }

        public async Task<List<RegridMatch>> ParseRegridMatchesAsync(string htmlSource)
        {
            var matches = new List<RegridMatch>();

            // Extracts the JSON array assigned to 'var hits' in the script tag
            var match = Regex.Match(htmlSource, @"var hits\s*=\s*(\[.*?\]);", RegexOptions.Singleline);

            if (match.Success)
            {
                string jsonArray = match.Groups[1].Value;

                using var doc = JsonDocument.Parse(jsonArray);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    string path = element.GetProperty("path").GetString() ?? string.Empty;

                    matches.Add(new RegridMatch(
                        Headline: element.GetProperty("headline").GetString() ?? string.Empty,
                        Context: element.GetProperty("context").GetString() ?? string.Empty,
                        Owner: element.GetProperty("owner").GetString() ?? string.Empty,
                        FullUrl: $"https://app.regrid.com{path}"
                    ));
                }
            }

            return matches;
        }
    }
}
