using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace AstroValleyAssistant.Core.Services
{
    public class RealTaxDeedClient : IRealTaxDeedClient
    {
        private readonly HttpClient _httpClient;

        // Maps different field labels found across various state and county layouts
        private readonly Dictionary<string, string[]> _labelMap = new Dictionary<string, string[]>
        {
            { "Id", new[] { "Account Number:", "Alternate Key:", "Parcel ID:" } },
            { "Bid", new[] { "Opening Bid:", "Min. Bid:" } },
            { "Value", new[] { "Assessed Value:", "Adjudged Value:" } },
            { "Address", new[] { "Property Address:", "Property Address:</th><td @CAD_DTA\">" } }
        };

        public RealTaxDeedClient(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Essential headers to bypass bot detection and mimic a real browser session
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }

        public async Task<List<AuctionRecord>> GetAuctionsAsync(string url, CancellationToken ct = default, IProgress<int> progress = null)
        {
            var allRecords = new List<AuctionRecord>();
            var uri = new Uri(url);
            var baseDomain = $"{uri.Scheme}://{uri.Host}";
            var query = HttpUtility.ParseQueryString(uri.Query);
            string auctionDateStr = query["AUCTIONDATE"] ?? DateTime.Now.ToString("MM/dd/yyyy");

            DateTime.TryParse(auctionDateStr, out DateTime auctionDate);

            // Initial handshake to establish session cookies and Referer context
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(url);
            await GetWithRetryAsync(url, ct: ct);

            int currentPage = 1;
            string lastFirstParcelId = string.Empty;

            while (true)
            {
                // Check for user cancellation before starting a new page request
                ct.ThrowIfCancellationRequested();

                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // RealAuction logic to signal page advancement to the server
                int pageDir = (currentPage == 1) ? 0 : 1;
                int doR = (currentPage == 1) ? 1 : 0;
                int bypassPage = (currentPage == 1) ? 1 : 0;

                string ajaxUrl = $"{baseDomain}/index.cfm?zaction=AUCTION&Zmethod=UPDATE&FNC=LOAD" +
                                 $"&AREA=W&PageDir={pageDir}&doR={doR}&bypassPage={bypassPage}&test=1" +
                                 $"&AUCTIONDATE={HttpUtility.UrlEncode(auctionDateStr)}" +
                                 $"&page_num={currentPage}&tx={ts}&_={ts + 1}";

                string response = await GetWithRetryAsync(ajaxUrl, ct: ct);
                if (string.IsNullOrWhiteSpace(response)) break;

                using var jsonDoc = JsonDocument.Parse(response);
                string rawRetHtml = jsonDoc.RootElement.GetProperty("retHTML").GetString() ?? string.Empty;

                // Exit if no more auction items are found in the HTML segment
                if (string.IsNullOrWhiteSpace(rawRetHtml) || !rawRetHtml.Contains("AITEM_")) break;

                var pageRecords = ParseRetHtml(rawRetHtml, auctionDate, currentPage, baseDomain);

                if (pageRecords.Any())
                {
                    // Safety break: stops the loop if the server returns the same page twice
                    if (pageRecords[0].ParcelId == lastFirstParcelId) break;

                    allRecords.AddRange(pageRecords);
                    lastFirstParcelId = pageRecords[0].ParcelId;

                    // Report the current count to the UI
                    progress?.Report(allRecords.Count);

                    currentPage++;

                    // Throttling delay to remain polite to the county servers
                    await Task.Delay(400, ct);
                }
                else break;
            }

            return allRecords;
        }

        private List<AuctionRecord> ParseRetHtml(string retHtml, DateTime auctionDate, int pageNum, string baseDomain)
        {
            var records = new List<AuctionRecord>();

            // Flexible split to handle different div attribute order (tabindex, id, etc.)
            string[] auctionBlocks = Regex.Split(retHtml, @"<div[^>]*id=""AITEM_", RegexOptions.IgnoreCase)
                                          .Where(s => !string.IsNullOrWhiteSpace(s))
                                          .ToArray();

            foreach (var block in auctionBlocks)
            {
                try
                {
                    string currentItem = "id=\"AITEM_" + block;

                    // 1. Extract Parcel ID using fallback labels and delimiters
                    string parcelIdRaw = GetValueByLabels(currentItem, _labelMap["Id"], "@F", "@G");
                    if (string.IsNullOrWhiteSpace(parcelIdRaw))
                        parcelIdRaw = GetValueByLabels(currentItem, _labelMap["Id"], "@CAD_DTA\">", "@G");

                    string parcelId = Clean(Regex.Replace(parcelIdRaw, "<.*?>", ""));
                    string appUrl = Regex.Match(parcelIdRaw, @"href=""([^""]+)""").Groups[1].Value;

                    // 2. Extract Prices
                    string openingBidRaw = GetValueByLabels(currentItem, _labelMap["Bid"], "@F", "@G");
                    if (string.IsNullOrWhiteSpace(openingBidRaw))
                        openingBidRaw = GetValueByLabels(currentItem, _labelMap["Bid"], "@CAD_DTA\">", "@G");

                    string assessedValueRaw = GetValueByLabels(currentItem, _labelMap["Value"], "@F", "@G");
                    if (string.IsNullOrWhiteSpace(assessedValueRaw))
                        assessedValueRaw = GetValueByLabels(currentItem, _labelMap["Value"], "@CAD_DTA\">", "@G");

                    // 3. Extract Address Line 1
                    string addrLine1 = GetValueByLabels(currentItem, _labelMap["Address"], "@F", "@G");
                    if (string.IsNullOrWhiteSpace(addrLine1))
                        addrLine1 = ExtractValue(currentItem, "Property Address:</th><td @CAD_DTA\">", "", "@G");

                    // 4. Extract Address Line 2 (City, State, Zip)
                    string addrLine2 = string.Empty;
                    int addr1Idx = currentItem.IndexOf("Property Address:");
                    if (addr1Idx > -1)
                    {
                        // Attempt Florida-style row delimiter first
                        addrLine2 = ExtractValue(currentItem, "@H@CAD_LBL\" scope=\"row\">@F", "", "@G", startIndex: addr1Idx);

                        // Fallback to Texas-style table tag
                        if (string.IsNullOrWhiteSpace(addrLine2))
                            addrLine2 = ExtractValue(currentItem, "</th><td @CAD_DTA\">", "", "@G", startIndex: addr1Idx + 20);
                    }

                    records.Add(new AuctionRecord(
                        ParcelId: parcelId,
                        PropertyAddress: $"{Clean(addrLine1)} {Clean(addrLine2)}".Trim(),
                        OpeningBid: ParseCurrency(openingBidRaw),
                        AssessedValue: ParseCurrency(assessedValueRaw),
                        AuctionDate: auctionDate,
                        PageNumber: pageNum,
                        AppraiserUrl: appUrl
                    ));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Parsing Error: {ex.Message}");
                }
            }
            return records;
        }

        private string GetValueByLabels(string currentItem, string[] labels, string startDelim, string endDelim, int startIndex = 0)
        {
            foreach (var label in labels)
            {
                string result = ExtractValue(currentItem, label, startDelim, endDelim, startIndex);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
            return string.Empty;
        }

        private string ExtractValue(string source, string label, string startDelim, string endDelim, int startIndex = 0)
        {
            int labelIdx = source.IndexOf(label, startIndex, StringComparison.OrdinalIgnoreCase);
            if (labelIdx == -1) return string.Empty;

            int startIdx = string.IsNullOrEmpty(startDelim) ? labelIdx + label.Length : source.IndexOf(startDelim, labelIdx);
            if (startIdx == -1) return string.Empty;

            startIdx += startDelim.Length;
            int endIdx = source.IndexOf(endDelim, startIdx);

            return (endIdx == -1) ? source.Substring(startIdx).Trim() : source.Substring(startIdx, endIdx - startIdx).Trim();
        }

        private string Clean(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string text = WebUtility.HtmlDecode(input);

            // Remove HTML tags, tabindex, and custom @ delimiters
            string pattern = @"(tabindex=""\d+"")|(<[^>]+>)|(@[A-Z_]+(?:"">| |"")?)|(@[A-Z]@?)";
            text = Regex.Replace(text, pattern, " ");

            // Final cleanup of lingering brackets and extra spaces
            text = text.Replace("\"", "").Replace(">", "").Replace("<", "");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private decimal ParseCurrency(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string cleanValue = Regex.Replace(value, @"[^\d.]", "");
            return decimal.TryParse(cleanValue, out decimal result) ? result : 0;
        }

        private async Task<string> GetWithRetryAsync(string url, int maxRetries = 3, CancellationToken ct = default)
        {
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Path A: Success - returns here
                    // HttpClient methods natively support CancellationTokens
                    var response = await _httpClient.GetAsync(url, ct);
                    return await response.Content.ReadAsStringAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // Rethrow immediately if the user cancelled
                    throw;
                }
                catch (Exception)
                {
                    retryCount++;

                    // Path B: Max retries reached - throws here
                    if (retryCount >= maxRetries)
                    {
                        throw;
                    }

                    // Exponential backoff before the next loop iteration
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), ct);
                }
            }

            // Path C: Final fallback - ensures all code paths return a value
            // This handles cases where the loop might terminate without returning or throwing
            return string.Empty;
        }
    }
}