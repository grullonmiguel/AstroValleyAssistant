using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroValleyAssistant.Core.Services
{
    public class RegridScraper : IRegridScraper
    {
        private readonly HttpClient _httpClient;

        // Internal delay between search → detail to avoid burst behavior
        private const int SearchDetailDelayMs = 300;

        public RegridScraper(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Configure browser-like headers once at construction
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8"); 
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://regrid.com/"); 
        }

        /// <summary>
        /// Performs the login handshake with Regrid.
        /// Fetches the login page, extracts CSRF token, and posts credentials.
        /// </summary>
        public async Task<bool> AuthenticateAsync(string email, string password, CancellationToken ct)
        {
            try
            {
                // 1. Load login page to obtain CSRF token
                var response = await _httpClient
                    .GetAsync("https://app.regrid.com/users/sign_in", ct)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var loginPageHtml = await response.Content
                    .ReadAsStringAsync(ct)
                    .ConfigureAwait(false);

                // 2. Extract authenticity_token
                var match = Regex.Match(loginPageHtml, "name=\"authenticity_token\" value=\"([^\"]+)\"");
                
                if (!match.Success) 
                    return false;

                string csrfToken = match.Groups[1].Value;

                // 3. Submit login for
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user[email]", email),
                    new KeyValuePair<string, string>("user[password]", password),
                    new KeyValuePair<string, string>("authenticity_token", csrfToken)
                });

                var postResponse = await _httpClient
                    .PostAsync("https://app.regrid.com/users/sign_in", content, ct)
                    .ConfigureAwait(false);

                return postResponse.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[REGRID AUTH ERROR] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs a full scrape: search → match detection → detail fetch → JSON parse.
        /// Returns null only on unexpected failure.
        /// </summary>
        public async Task<RegridParcelResult?> GetPropertyDetailsAsync(string query, CancellationToken ct = default)
        {
            try
            {
                // 1. Perform search request
                string searchUrl = $"https://app.regrid.com/search?query={Uri.EscapeDataString(query)}&context=/us";

                string searchHtml = await GetWithRetryAsync(searchUrl, ct)
                    .ConfigureAwait(false);

                Debug.WriteLine($"[REGRID] Search completed for '{query}'.");

                // Prevent rapid search → detail burst
                await Task.Delay(SearchDetailDelayMs, ct).ConfigureAwait(false);

                // 2. Detect match count
                var match = Regex.Match(searchHtml, @"Found (\d+) matches", RegexOptions.IgnoreCase);
                int matchCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;

                // Default is NotFound
                if (matchCount == 0)
                {
                    Debug.WriteLine($"[REGRID] No matches found for '{query}'.");
                    return new RegridParcelResult
                    {
                        Query = query,
                        NotFound = true,
                        Record = new PropertyRecord { RegridUrl = searchUrl }
                    };
                }

                // No matches
                if (matchCount > 1)
                {
                    Debug.WriteLine($"[REGRID] Multiple matches for '{query}'. Count={matchCount}");

                    var parsedMatches = await ParseRegridMatchesAsync(searchHtml)
                        .ConfigureAwait(false);

                    return new RegridParcelResult
                    {
                        Query = query,
                        IsMultiple = true,
                        Matches = parsedMatches,
                        Record = new PropertyRecord { RegridUrl = searchUrl }
                    };
                }

                // 3. Extract the parcel path from the 'hits' JS variable
                string parcelPath = Regex.Match(searchHtml, @"""category"":""parcel"",""path"":""([^""]+)""").Groups[1].Value;

                if (string.IsNullOrEmpty(parcelPath))
                    return new RegridParcelResult { Query = query, NotFound = true };

                // 4. Fetch detail JSON
                string detailUrl = $"https://app.regrid.com{parcelPath}.json";
                string detailJson = await GetWithRetryAsync(detailUrl, ct)
                    .ConfigureAwait(false);

                Debug.WriteLine($"[REGRID] Detail JSON fetched for '{query}' → {detailUrl}");

                // Construct browser-friendly URL
                string regridUrl = $"https://app.regrid.com/us#t=property&p={parcelPath}";

                // 5. Parse JSON into PropertyRecord
                var record = ParseRegridJson(detailJson, regridUrl);
                
                if (record == null)
                    return new RegridParcelResult { Query = query, Error = new Exception("Failed to parse JSON.") };

                Debug.WriteLine($"[REGRID] Successfully parsed parcel for '{query}'.");

                return new RegridParcelResult
                {
                    Query = query,
                    Record = record
                };

            }
            catch (RegridRateLimitException)
            {
                throw; // Let the service layer handle 429 logic
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REGRID ERROR] Query '{query}' failed: {ex}");
                return new RegridParcelResult { Query = query, Error = ex };
            }
        }

        /// <summary>
        /// Parses the Regrid detail JSON into a PropertyRecord.
        /// </summary>
        private PropertyRecord? ParseRegridJson(string json, string regridUrl)
        {
            using var doc = JsonDocument.Parse(json);

            // Web app internal JSON uses "fields" as the primary data container
            if (!doc.RootElement.TryGetProperty("fields", out var f)) 
                return null;

            LogAvailableKeys(f);

            // Address resolution logic
            var fullAddress = doc.RootElement.GetFormattedAddress();            
            if (string.IsNullOrWhiteSpace(fullAddress)) 
                fullAddress = f.GetJsonString("address"); // If formatted addres was not provided, get it from the address tab            
            if (string.IsNullOrEmpty(fullAddress)) 
                fullAddress = doc.RootElement.GetJsonString("headline"); // Final fallback to get the address from the 'headline' property

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

        // <summary>
        /// Extracts assessed value using Regrid's inconsistent key patterns.
        /// </summary>
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

        /// <summary>
        /// GET with retry + exponential backoff + 429 detection.
        /// </summary>
        private async Task<string> GetWithRetryAsync(string url, CancellationToken ct)
        {
            Exception? lastException = null;

            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, ct);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        throw new RegridRateLimitException(response);

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(ct);
                }
                catch (RegridRateLimitException ex)
                {
                    Debug.WriteLine($"[REGRID 429] Rate limit hit for URL: {url}. RetryAfter={ex.RetryAfterSeconds ?? -1} seconds.");
                    // Do NOT retry here — let RegridService handle it
                    throw;
                }
                catch (Exception ex) when (retry < 2)
                {
                    lastException = ex;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry + 1)), ct);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            throw new HttpRequestException($"Failed to GET '{url}' after 3 attempts.", lastException);
        }

        /// <summary>
        /// Parses the "hits" array from the search page into a list of RegridMatch objects.
        /// </summary>
        public async Task<List<RegridMatch>> ParseRegridMatchesAsync(string htmlSource)
        {
            await Task.CompletedTask;

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

        /// <summary>
        /// Logs all available keys in the "fields" object for debugging.
        /// </summary>
        private void LogAvailableKeys(JsonElement fieldsObject)
        {
            // This will print every key available for the parcel in the Debug window
            foreach (var property in fieldsObject.EnumerateObject())
                Debug.WriteLine($"Found Key: {property.Name} | Value: {property.Value}");
        }
    }

    /// <summary>
    /// Custom exception for handling Regrid 429 responses.
    /// </summary>
    public class RegridRateLimitException : Exception
    {
        public int? RetryAfterSeconds { get; }

        public RegridRateLimitException(HttpResponseMessage response) : base("Regrid returned 429 Too Many Requests")
        {
            if (response.Headers.TryGetValues("Retry-After", out var values) && int.TryParse(values.FirstOrDefault(), out int seconds))
            {
                RetryAfterSeconds = seconds;
            }
        }
    }
}