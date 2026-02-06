using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models.Domain;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;

namespace AstroValleyAssistant.Core.Utilities
{
    public static class ClipboardFormatter
    {
        /// <summary>
        /// Formats a single PropertyRecord into a tab-separated block.
        /// </summary>
        public static string FormatForGoogleSheets(PropertyRecord record)
        {
            var lines = new List<string>
        {
            $"Parcel ID\t{record.ParcelId}",
            $"Address\t{record.Address}",
            $"Owner\t{record.Owner}",
            $"Acres\t{record.Acres}",
            $"City\t{record.City}",
            $"Zip\t{record.Zip}",
            $"Zoning Code\t{record.ZoningCode}",
            $"Zoning Type\t{record.ZoningType}",
            $"Flood Zone\t{record.FloodZone}",
            $"Coordinates\t{record.GeoCoordinates}",
            $"Regrid URL\t{record.RegridUrl}",
            $"Google Maps\t{UrlBuilder.BuildGoogleMapsUrl(record)}",
            $"FEMA Flood\t{UrlBuilder.BuildFemaFloodUrl(record)}"
        };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Formats an entire list of PropertyRecords into a multi-row,
        /// tab-separated table suitable for Google Sheets.
        /// </summary>
        public static string FormatAllForGoogleSheets(IEnumerable<PropertyRecord> records)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join("\t", new[]
            {
            "Zoning Type",
            "Zoning Code",
            "City",
            "Parcel ID",
            "Regrid URL",
            "Owner",
            "Appraiser",
            "Assessed",
            "Starting Bid",
            "Acres",
            "Address",
            "Flood Zone",
            "FEMA Flood",
            "Google Maps",
            "Coordinates",
            "Image"
        }));

            // Data rows
            foreach (var r in records)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
                    r.ZoningType,
                    r.ZoningCode,
                    r.City,
                    r.ParcelId,
                    FormatGoogleSheetsUrRL(r.RegridUrl, "Regrid"),
                    r.Owner,
                    FormatGoogleSheetsUrRL(r.AppraiserUrl, "Appraiser"),
                    r.AssessedValue?.ToString() ?? "",
                    r.OpeningBid.ToString() ?? "",
                    r.Acres?.ToString() ?? "",
                    FormatGoogleSheetsUrRL(UrlBuilder.BuildGoogleMapsUrl(r) ?? "", r.Address),
                    r.Address,
                    FormatGoogleSheetsUrRL(UrlBuilder.BuildFemaFloodUrl(r) ?? "" ?? "",r.FloodZone),
                    r.GeoCoordinates,
                    r.BirdseyeUrl,
                    FormatGoogleSheetsImage(r.BirdseyeUrl) 
                }));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Copies the entire list to the clipboard.
        /// </summary>
        public static void CopyAllToClipboard(IEnumerable<PropertyRecord> records)
        {
            Clipboard.SetText(FormatAllForGoogleSheets(records));
        }

        /// <summary>
        /// Creates a Google Sheets compatible hyperlink
        /// </summary>
        private static string FormatGoogleSheetsUrRL(string url, string alias) => string.IsNullOrWhiteSpace(url)
            ? string.Empty : $"=HYPERLINK(\"{url.Replace("\"", "\"\"")}\", \"{alias}\")";

        /// <summary>
        /// Creates a Google Sheets compatible IMAGE formula.
        /// </summary>
        private static string FormatGoogleSheetsImage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            // Mode 1: Resizes the image to fit inside the cell while maintaining aspect ratio
            return $"=IMAGE(\"{url.Replace("\"", "\"\"")}\", 1)";
        }

        public static async Task ExportToExcelWithImagesAsync(IEnumerable<PropertyRecord> records, string filePath)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Property Data");

            // Standard headers...
            int row = 2;

            using var client = new HttpClient();
            ConfigureClient(client);

            foreach (var r in records)
            {
                // Fill Excel text cells...

                if (!string.IsNullOrWhiteSpace(r.BirdseyeUrl))
                {
                    try
                    {
                        // Delay 500ms - 1.5s between requests to be polite to the server
                        await Task.Delay(new Random().Next(500, 1500));

                        byte[] imageBytes = await client.GetByteArrayAsync(r.BirdseyeUrl);
                        using var ms = new MemoryStream(imageBytes);

                        var picture = ws.AddPicture(ms);
                        picture.Name = $"Img_{r.ParcelId}";
                        picture.MoveTo(ws.Cell(row, 4));

                        ws.Row(row).Height = 90;
                        picture.Width = 120;
                        picture.Height = 85;
                    }
                    catch (Exception ex)
                    {
                        ws.Cell(row, 4).Value = "Download Error";
                        Debug.WriteLine($"Failed for {r.ParcelId}: {ex.Message}");
                    }
                }
                row++;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        private static void ConfigureClient(HttpClient client)
        {
            client.DefaultRequestHeaders.Clear();

            // Identity & Capabilities
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            // Browser Security Headers (Crucial for modern bot detection)
            client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"144\", \"Google Chrome\";v=\"144\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
            client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
            client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            client.DefaultRequestHeaders.Add("sec-fetch-site", "none");

            // THE SECRET SAUCE: The Cookies
            // Paste the entire string from your "cookie": "..." snippet below
            string sessionCookies = "_CEFT=Q%3D%3D%3D; _cfuvid=TCr74.rtTD3vXj_Ug_OpHMF1K0vCud95.yWYeMsNZQI-1770079931291-0.0.1.1-604800000; cebs=1; _ce.clock_data=171761%2C66.176.109.49%2C1%2C8e253f85246590342756399a57054cb8%2CChrome%2CUS; _gid=GA1.2.684964734.1770080105; _ga=GA1.2.607350804.1756328968; cebsp_=3; _ga_NGWML8455J=GS2.1.s1770082367$o94$g0$t1770082367$j60$l0$h0; _ce.s=v~1b4b22ce2b17b06765016015216eb3910668d8b4~lcw~1770082367050~vir~returning~lva~1770080126775~vpv~10~v11ls~f990dc60-009a-11f1-ba1b-0927514588e7~v11.cs~392098~v11.s~f990dc60-009a-11f1-ba1b-0927514588e7~v11.vs~1b4b22ce2b17b06765016015216eb3910668d8b4~v11.fsvd~eyJ1cmwiOiJhcHAucmVncmlkLmNvbS91cyIsInJlZiI6IiIsInV0bSI6W119~v11.sla~1770080103719~gtrk.la~ml5xc7kb~lcw~1770082367052; user.id=BAgw--79292e56d5866cc41199e6f92f32032686d8e164; user.expires_at=BAgw--79292e56d5866cc41199e6f92f32032686d8e164; _session_id=7a8e35ecd5d318d2bd2331704a4e60ce";

            client.DefaultRequestHeaders.Add("Cookie", sessionCookies);
        }
    }
}