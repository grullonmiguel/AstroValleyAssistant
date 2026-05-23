using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Models;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroValley.Infrastructure.Scraping;

/// <summary>
/// HTTP client for scraping parcel data from Regrid's web interface.
/// Handles authentication, search, match detection, and JSON parsing.
/// </summary>
/// <remarks>
/// Best Practice: This client focuses on Regrid-specific business logic while
/// delegating infrastructure concerns (retry, circuit breaker, logging) to the
/// resilience handler configured in DI. Headers are centralized in App.xaml.cs.
/// </remarks>
public class RegridScraper : IRegridScraper
{
    private readonly HttpClient _httpClient;

    // Internal delay between search → detail to avoid burst behavior
    private const int SearchDetailDelayMs = 300;

    public RegridScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Note: Headers are now configured in App.xaml.cs during DI registration
        // This centralizes configuration and follows HttpClient best practices
    }

    // -------------------------------------------------------------------------
    // AUTHENTICATION
    // -------------------------------------------------------------------------

    /// <summary>
    /// Performs the login handshake with Regrid.
    /// Fetches the login page, extracts CSRF token, and posts credentials.
    /// </summary>
    /// <remarks>
    /// This method handles the multi-step authentication flow:
    /// 1. GET login page to obtain CSRF token
    /// 2. POST credentials with token to establish session
    /// Session cookies are automatically managed by the SocketsHttpHandler's CookieContainer.
    /// </remarks>
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

            // 2. Extract authenticity_token (Rails CSRF protection)
            var match = Regex.Match(loginPageHtml, "name=\"authenticity_token\" value=\"([^\"]+)\"");
            if (!match.Success)
                return false;

            string csrfToken = match.Groups[1].Value;

            // 3. Submit login form with credentials and CSRF token
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

    // -------------------------------------------------------------------------
    // PUBLIC ENTRY POINT #1 — QUERY SCRAPE (parcel ID or address)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Performs a full scrape: search → match detection → detail fetch → JSON parse.
    /// Automatically routes direct URLs to the direct-scrape path.
    /// </summary>
    /// <remarks>
    /// This method orchestrates the multi-step scraping workflow:
    /// 1. Detect if query is a direct URL or search term
    /// 2. Perform search and analyze match count
    /// 3. Handle no matches, single match, or multiple matches
    /// 4. Fetch and parse parcel details
    /// 
    /// Rate limiting (429) is handled via custom exception that bypasses retry logic.
    /// </remarks>
    public async Task<RegridParcelResult?> GetPropertyDetailsAsync(string query, CancellationToken ct = default)
    {
        try
        {
            // 1. If the query is already a full Regrid URL, skip search entirely
            if (Uri.TryCreate(query, UriKind.Absolute, out var directUri) && directUri.Host.Contains("regrid.com"))
            {
                return await ScrapeParcelFromUrlAsync(query, ct).ConfigureAwait(false);
            }

            // 2. Build search URL for parcel ID or address
            string searchUrl = $"https://app.regrid.com/search?query={Uri.EscapeDataString(query)}&context=/us";

            // 3. Perform search request (resilience handler manages retries)
            string searchHtml = await GetHtmlAsync(searchUrl, ct).ConfigureAwait(false);
            Debug.WriteLine($"[REGRID] Search completed for '{query}'.");

            // Prevent rapid search → detail burst (polite throttling)
            await Task.Delay(SearchDetailDelayMs, ct).ConfigureAwait(false);

            // 4. Detect match count from search results
            var match = Regex.Match(searchHtml, @"Found (\d+) matches", RegexOptions.IgnoreCase);
            int matchCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;

            // 5. No matches found
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

            // 6. Multiple matches - return list for user selection
            if (matchCount > 1)
            {
                Debug.WriteLine($"[REGRID] Multiple matches for '{query}'. Count={matchCount}");
                var parsedMatches = await ParseRegridMatchesAsync(searchHtml).ConfigureAwait(false);

                return new RegridParcelResult
                {
                    Query = query,
                    IsMultiple = true,
                    Matches = parsedMatches,
                    Record = new PropertyRecord { RegridUrl = searchUrl }
                };
            }

            // 7. Exactly one match → extract parcel path
            string parcelPath = Regex.Match(searchHtml, @"""category"":""parcel"",""path"":""([^""]+)""")
                                     .Groups[1].Value;

            if (string.IsNullOrEmpty(parcelPath))
                return new RegridParcelResult { Query = query, NotFound = true };

            // 8. Build browser-friendly URL
            string regridUrl = $"https://app.regrid.com/us#t=property&p={parcelPath}";

            // 9. Fetch + parse JSON using shared helper
            return await FetchParcelJsonAsync(parcelPath, regridUrl, ct).ConfigureAwait(false);
        }
        catch (RegridRateLimitException)
        {
            throw; // Let service layer handle 429 logic
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[REGRID ERROR] Query '{query}' failed: {ex}");
            return new RegridParcelResult { Query = query, Error = ex };
        }
    }

    // -------------------------------------------------------------------------
    // PUBLIC ENTRY POINT #2 — DIRECT URL SCRAPE
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scrapes a parcel directly from a full Regrid URL.
    /// Supports both clean URLs and fragment URLs.
    /// </summary>
    /// <remarks>
    /// This method bypasses the search step when the user provides a direct parcel URL.
    /// Useful for re-scraping known parcels or following links from previous searches.
    /// </remarks>
    public async Task<RegridParcelResult?> ScrapeParcelFromUrlAsync(string fullUrl, CancellationToken ct = default)
    {
        try
        {
            Debug.WriteLine($"[REGRID] Direct parcel scrape for '{fullUrl}'");

            // 1. Extract parcel path from URL (fragment or clean)
            string parcelPath = ExtractParcelPathFromUrl(fullUrl);

            if (string.IsNullOrWhiteSpace(parcelPath))
            {
                Debug.WriteLine("[REGRID] Could not extract parcel path from URL.");
                return new RegridParcelResult { Query = fullUrl, NotFound = true };
            }

            // 2. Fetch + parse JSON using shared helper
            return await FetchParcelJsonAsync(parcelPath, fullUrl, ct).ConfigureAwait(false);
        }
        catch (RegridRateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[REGRID ERROR] Direct scrape failed: {ex}");
            return new RegridParcelResult { Query = fullUrl, Error = ex };
        }
    }

    // -------------------------------------------------------------------------
    // PRIVATE HELPER — Extract parcel path from ANY Regrid URL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts the parcel path from either:
    /// 1. Fragment URLs (#t=property&p=/us/...)
    /// 2. Clean URLs (https://app.regrid.com/us/fl/.../12345)
    /// </summary>
    private string ExtractParcelPathFromUrl(string url)
    {
        // Case 1: Fragment URL
        var frag = Regex.Match(url, @"[?#]t=property&p=([^&]+)");
        if (frag.Success)
            return frag.Groups[1].Value;

        // Case 2: Clean URL
        var uri = new Uri(url);
        return uri.AbsolutePath; // e.g. "/us/fl/bay/panama-city/48002"
    }

    // -------------------------------------------------------------------------
    // PRIVATE HELPER — Fetch + parse parcel JSON
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches the parcel JSON and parses it into a PropertyRecord.
    /// Shared by both search-based and direct URL scrapes.
    /// </summary>
    /// <remarks>
    /// Regrid exposes parcel details at: https://app.regrid.com{parcelPath}.json
    /// This method handles the HTTP request and delegates parsing to ParseRegridJson.
    /// </remarks>
    private async Task<RegridParcelResult?> FetchParcelJsonAsync(string parcelPath, string originalUrl, CancellationToken ct)
    {
        // Build the JSON endpoint for the parcel
        string detailUrl = $"https://app.regrid.com{parcelPath}.json";
        Debug.WriteLine($"[REGRID] Fetching detail JSON → {detailUrl}");

        // Perform the HTTP GET (resilience handler manages retry + rate-limit detection)
        string detailJson = await GetHtmlAsync(detailUrl, ct).ConfigureAwait(false);

        // Convert the JSON payload into a strongly-typed PropertyRecord
        var record = ParseRegridJson(detailJson, originalUrl);

        // If parsing failed, return an error result instead of throwing
        if (record == null)
        {
            return new RegridParcelResult
            {
                Query = originalUrl,
                Error = new Exception("Failed to parse JSON.")
            };
        }

        // Successful parse → return the fully populated result
        return new RegridParcelResult
        {
            Query = originalUrl,
            Record = record
        };
    }

    // -------------------------------------------------------------------------
    // JSON PARSING
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses the Regrid detail JSON into a PropertyRecord.
    /// </summary>
    private PropertyRecord? ParseRegridJson(string json, string regridUrl)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("fields", out var f))
            return null;

        LogAvailableKeys(f);

        // Address resolution logic
        var fullAddress = doc.RootElement.GetFormattedAddress();
        if (string.IsNullOrWhiteSpace(fullAddress)) fullAddress = f.GetJsonString("address");
        if (string.IsNullOrEmpty(fullAddress)) fullAddress = doc.RootElement.GetJsonString("headline");

        var parcel = new PropertyRecord
        {
            RegridUrl = regridUrl,
            ParcelId = f.GetJsonString("parcelnumb", "parcelid", "lowparcelid"),
            Address = fullAddress,
            City = f.GetJsonString("scity", "municipality"),
            Zip = f.GetJsonString("szip", "zipcode"),
            Acres = f.GetJsonDouble("ll_gisacre", "acres"),
            Owner = f.GetJsonString("owner", "eo_owner"),
            ZoningCode = f.GetJsonString("zoning", "zoning_description"),
            ZoningType = f.GetJsonString("zoning_type", "zoning_subtype", "usedesc"),
            Latitude = $"{f.GetJsonString("lat")}",
            Longitude = $"{f.GetJsonString("lon")}",
            GeoCoordinates = $"{f.GetJsonString("lat")}, {f.GetJsonString("lon")}",
            ElevationHigh = f.GetJsonString("highest_parcel_elevation"),
            ElevationLow = f.GetJsonString("lowest_parcel_elevation"),
            FloodZone = f.GetJsonString("fema_flood_zone", "fema_flood_zone_subtype", "fema_nri_risk_rating"),
            AssessedValue = GetAssessedValue(f),
            BirdseyeUrl = doc.RootElement.GetJsonString("birdseye"),
            ParcelLines = doc.RootElement.GetJsonString("geometry")
        };

        return parcel;
    }

    // -------------------------------------------------------------------------
    // ASSESSED VALUE EXTRACTION
    // -------------------------------------------------------------------------

    private decimal? GetAssessedValue(JsonElement fields)
    {
        // 1. Check if the value type is 'ASSESSED' or 'MARKET'
        if (fields.TryGetProperty("parvaltype", out var typeProp))
        {
            string typeValue = typeProp.GetString() ?? string.Empty;

            if (typeValue.Equals("ASSESSED", StringComparison.OrdinalIgnoreCase) ||
                typeValue.Equals("MARKET", StringComparison.OrdinalIgnoreCase))
            {
                if (fields.TryGetProperty("parval", out var valProp))
                {
                    if (valProp.ValueKind == JsonValueKind.Number)
                        return valProp.GetDecimal();

                    string raw = valProp.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        string clean = Regex.Replace(raw, @"[^\d.]", "");
                        if (decimal.TryParse(clean, out decimal result))
                            return result;
                    }
                }
            }
        }

        // 2. Fallback keys
        return fields.GetJsonDecimal("total_value", "ll_val_asmt", "total_parcel_value");
    }

    // -------------------------------------------------------------------------
    // HTTP GET WITH RATE LIMIT DETECTION
    // -------------------------------------------------------------------------

    /// <summary>
    /// Performs an HTTP GET request with custom 429 (rate limit) detection.
    /// </summary>
    /// <remarks>
    /// Best Practice: This method is intentionally simple. The resilience handler
    /// (configured in App.xaml.cs) manages retry logic, exponential backoff, circuit
    /// breaker, and timeouts. This method only adds domain-specific logic for detecting
    /// Regrid's rate limiting (HTTP 429), which requires special handling.
    /// 
    /// The resilience handler is configured to NOT retry on 429 responses, allowing
    /// the service layer to implement custom backoff strategies.
    /// </remarks>
    private async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        // Perform the HTTP GET request
        // The resilience handler automatically:
        // - Retries on transient failures (except 429)
        // - Applies exponential backoff with jitter
        // - Enforces circuit breaker pattern
        // - Logs via LoggingDelegatingHandler
        var response = await _httpClient.GetAsync(url, ct);

        // Custom domain logic: Detect Regrid rate-limit response (HTTP 429)
        // This exception bypasses the retry logic and bubbles up to the service layer
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RegridRateLimitException(response);

        // Throw if response is not successful (e.g., 404, 500)
        response.EnsureSuccessStatusCode();

        // Return the HTML/JSON content
        return await response.Content.ReadAsStringAsync(ct);
    }

    // -------------------------------------------------------------------------
    // MULTIPLE MATCH PARSING
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses the Regrid search results page and extracts the list of parcel matches.
    /// Regrid embeds a JavaScript variable named 'hits' that contains a JSON array
    /// of all matching parcels. This method locates that array, parses it, and
    /// converts each entry into a strongly-typed <see cref="RegridMatch"/>.
    /// </summary>
    public async Task<List<RegridMatch>> ParseRegridMatchesAsync(string htmlSource)
    {
        // This keeps the method signature async without forcing unnecessary awaits.
        await Task.CompletedTask;

        var matches = new List<RegridMatch>();

        // ---------------------------------------------------------------------
        // Regrid embeds search results inside a script tag:
        //
        //     var hits = [ { ... }, { ... }, ... ];
        //
        // This regex extracts the entire JSON array assigned to 'hits'.
        // ---------------------------------------------------------------------
        var match = Regex.Match(htmlSource, @"var hits\s*=\s*(\[.*?\]);", RegexOptions.Singleline);

        // If the regex successfully extracted the JSON array, parse it
        if (match.Success)
        {
            string jsonArray = match.Groups[1].Value;

            // Parse the JSON array into a DOM for enumeration
            using var doc = JsonDocument.Parse(jsonArray);

            // Each element in the array represents a single parcel match
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                // The "path" property is the relative URL to the parcel page
                string path = element.GetProperty("path").GetString() ?? string.Empty;

                // Build a strongly-typed match object for the UI
                matches.Add(new RegridMatch(
                    Address: element.GetProperty("headline").GetString() ?? string.Empty,
                    City: element.GetProperty("context").GetString() ?? string.Empty,
                    Owner: element.GetProperty("owner").GetString() ?? string.Empty,
                    FullUrl: $"https://app.regrid.com{path}",
                    ParcelId: element.GetProperty("parcelnumb").GetString() ?? string.Empty
                ));
            }
        }

        // Return all parsed matches (empty list if none found)
        return matches;
    }

    // -------------------------------------------------------------------------
    // DEBUGGING UTILITIES
    // -------------------------------------------------------------------------

    private void LogAvailableKeys(JsonElement fieldsObject)
    {
        foreach (var property in fieldsObject.EnumerateObject())
            Debug.WriteLine($"Found Key: {property.Name} | Value: {property.Value}");
    }
}

// -----------------------------------------------------------------------------
// CUSTOM EXCEPTION FOR 429 HANDLING
// -----------------------------------------------------------------------------

/// <summary>
/// Exception thrown when Regrid returns HTTP 429 (Too Many Requests).
/// This signals rate limiting and requires special handling by the service layer.
/// </summary>
/// <remarks>
/// Best Practice: Domain-specific exceptions allow the service layer to implement
/// custom retry strategies (e.g., longer backoff for rate limits) while keeping
/// the HTTP client focused on detection rather than policy.
/// 
/// The resilience handler is configured to NOT retry on 429 responses, ensuring
/// this exception propagates to the service layer for appropriate handling.
/// </remarks>
public class RegridRateLimitException : Exception
{
    /// <summary>
    /// The number of seconds to wait before retrying, if provided by the server.
    /// </summary>
    public int? RetryAfterSeconds { get; }

    public RegridRateLimitException(HttpResponseMessage response) : base("Regrid returned 429 Too Many Requests")
    {
        // Extract Retry-After header if present (RFC 7231)
        if (response.Headers.TryGetValues("Retry-After", out var values) && 
            int.TryParse(values.FirstOrDefault(), out int seconds))
        {
            RetryAfterSeconds = seconds;
        }
    }
}