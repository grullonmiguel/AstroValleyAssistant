using AstroValleyAssistant.Core.Networking;
using AstroValleyAssistant.Models.Domain;
using ClosedXML.Excel;
using System.IO;

namespace AstroValleyAssistant.Core.Export
{
    public class ExcelPropertyExporter : IExporter<IEnumerable<PropertyRecord>, string?>
    {
        private readonly IRegridHttpClient _regridClient;

        public ExcelPropertyExporter(IRegridHttpClient regridClient)
        {
            _regridClient = regridClient;
        }

        public async Task ExportAsync(IEnumerable<PropertyRecord> records, string? filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Property Data");

            // Define column headers for the spreadsheet
            ws.Cell(1, 1).Value = "Parcel ID";
            ws.Cell(1, 2).Value = "Address";
            ws.Cell(1, 3).Value = "Image";

            int row = 2;
            foreach (var record in records)
            {
                // Populate standard text fields
                ws.Cell(row, 1).Value = record.ParcelId;
                ws.Cell(row, 2).Value = record.Address;

                if (!string.IsNullOrEmpty(record.BirdseyeUrl))
                {
                    // Download image using the polite client (handles cookies and throttling)
                    var imageBytes = await _regridClient.DownloadImageAsync(record.BirdseyeUrl);

                    if (imageBytes.Length > 0)
                    {
                        // Embed the image directly into the Excel cell
                        using var ms = new MemoryStream(imageBytes);
                        var picture = ws.AddPicture(ms);
                        picture.MoveTo(ws.Cell(row, 3));

                        // Set row height to accommodate the image preview
                        ws.Row(row).Height = 90;
                    }
                }
                row++;
            }

            // Finalize layout and write to disk
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }
    }
}
