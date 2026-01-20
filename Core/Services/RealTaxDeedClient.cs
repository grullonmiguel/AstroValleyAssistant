using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Models;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Globalization;
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

        public RealTaxDeedClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            
            // Headers to mimic the browser session seen in your traces
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }

        public async Task<List<AuctionRecord>> GetAuctionsAsync(string fullUrl)
        {
            var allRecords = new List<AuctionRecord>();
            var uri = new Uri(fullUrl);
            var baseDomain = $"{uri.Scheme}://{uri.Host}";
            var query = HttpUtility.ParseQueryString(uri.Query);
            string auctionDateStr = query["AUCTIONDATE"] ?? string.Empty;

            if (!DateTime.TryParse(auctionDateStr, out DateTime auctionDate))
                auctionDate = DateTime.Now;

            // Establish session and Referer
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(fullUrl);
            await _httpClient.GetAsync(fullUrl);

            int currentPage = 1;
            string lastFirstParcelId = string.Empty;

            while (true)
            {
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Mirror the payload differences between Page 1 and subsequent pages
                // Page 1: PageDir=0, doR=1, bypassPage=1
                // Page 2+: PageDir=1, doR=0, bypassPage=0
                int pageDir = (currentPage == 1) ? 0 : 1;
                int doR = (currentPage == 1) ? 1 : 0;
                int bypassPage = (currentPage == 1) ? 1 : 0;

                string ajaxUrl = $"{baseDomain}/index.cfm?zaction=AUCTION&Zmethod=UPDATE&FNC=LOAD" +
                                 $"&AREA=W&PageDir={pageDir}&doR={doR}&bypassPage={bypassPage}&test=1" +
                                 $"&AUCTIONDATE={HttpUtility.UrlEncode(auctionDateStr)}" +
                                 $"&page_num={currentPage}&tx={ts}&_={ts + 1}";

                var response = await _httpClient.GetStringAsync(ajaxUrl);

                using var jsonDoc = JsonDocument.Parse(response);
                string rawRetHtml = jsonDoc.RootElement.GetProperty("retHTML").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(rawRetHtml) || !rawRetHtml.Contains("AITEM_"))
                    break;

                var pageRecords = ParseRetHtml(rawRetHtml, auctionDate, currentPage, baseDomain);

                if (pageRecords.Any())
                {
                    // Strict duplicate check to prevent infinite loops if the server stalls
                    if (pageRecords[0].ParcelId == lastFirstParcelId) break;

                    allRecords.AddRange(pageRecords);
                    lastFirstParcelId = pageRecords[0].ParcelId;

                    currentPage++;
                    // Delay ensures the server has processed the session-state change
                    await Task.Delay(400);
                }
                else break;
            }

            return allRecords;
        }

        private List<AuctionRecord> ParseRetHtml(string retHtml, DateTime auctionDate, int pageNum, string baseDomain)
        {
            var records = new List<AuctionRecord>();

            // Use Regex split to handle irregular <div> structures found in Texas source
            string[] auctionBlocks = Regex.Split(retHtml, @"<div[^>]*id=""AITEM_", RegexOptions.IgnoreCase)
                                          .Where(s => !string.IsNullOrWhiteSpace(s))
                                          .ToArray();

            foreach (var block in auctionBlocks)
            {
                try
                {
                    string currentItem = "id=\"AITEM_" + block;

                    // 1. Identify Identifier
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
                        // Florida Strategy
                        addrLine2 = ExtractValue(currentItem, "@H@CAD_LBL\" scope=\"row\">@F", "", "@G", startIndex: addr1Idx);

                        // Texas Fallback
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
                    Debug.WriteLine($"Row Parsing Error: {ex.Message}");
                }
            }

            return records;
        }

        private string GetValueByLabels(string currentItem, string[] labels, string startDelim, string endDelim, int startIndex = 0)
        {
            foreach (var label in labels)
            {
                // Attempt to extract using the current label in the collection
                string result = ExtractValue(currentItem, label, startDelim, endDelim, startIndex);

                // Return the first non-empty value found
                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
            return string.Empty;
        }

        private string ExtractValue(string source, string label, string startDelim, string endDelim, int startIndex = 0)
        {
            // Find the label starting from the provided index
            int labelIdx = source.IndexOf(label, startIndex, StringComparison.OrdinalIgnoreCase);
            if (labelIdx == -1) return string.Empty;

            // Find the start delimiter relative to the label
            int startIdx = string.IsNullOrEmpty(startDelim)
                ? labelIdx + label.Length
                : source.IndexOf(startDelim, labelIdx);

            if (startIdx == -1) return string.Empty;

            // Adjust start index to move past the delimiter itself
            startIdx += startDelim.Length;

            // Find the end delimiter starting from our new data position
            int endIdx = source.IndexOf(endDelim, startIdx);

            if (endIdx == -1) return source.Substring(startIdx).Trim();
            return source.Substring(startIdx, endIdx - startIdx).Trim();
        }

        private string Clean(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // 1. Decode HTML entities (converts &nbsp; to spaces)
            string text = WebUtility.HtmlDecode(input);

            // 2. Comprehensive pattern to remove noise found in both states:
            // - tabindex="..."
            // - HTML-like tags: <th, <td, </th>, </td>, <tr>, </tr>, <tbody>
            // - RealAuction Delimiters: @CAD_DTA, @CAD_LBL, @G, @F, @C@, etc.
            string pattern = @"(tabindex=""\d+"")|(<[^>]+>)|(@[A-Z_]+(?:"">| |"")?)|(@[A-Z]@?)";
            text = Regex.Replace(text, pattern, " ");

            // 3. Remove any lingering quotes or brackets often left by malformed tags
            text = text.Replace("\"", "").Replace(">", "").Replace("<", "");

            // 4. Collapse multiple spaces into one
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private decimal ParseCurrency(string? input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            // Extract only digits and decimal points [cite: 38, 60]
            string clean = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : 0;
        }

        private readonly Dictionary<string, string[]> _labelMap = new Dictionary<string, string[]>
        {
            { "Id", new[] { "Account Number:", "Alternate Key:", "Parcel ID:" } },
            { "Bid", new[] { "Opening Bid:", "Min. Bid:" } },
            { "Value", new[] { "Assessed Value:", "Adjudged Value:" } },
            { "Address", new[] { "Property Address:", "Property Address:</th><td @CAD_DTA\">" } }
        };
    }
}
