using System.Globalization;
using AstroValley.Application.Interfaces.Export;
using AstroValley.Domain.Models;
using ClosedXML.Excel;

namespace AstroValley.Infrastructure.Export;

/// <summary>Fills the research template: tax office, appraiser, maps, FEMA, market value, clerk and misc blocks.</summary>
public class PinResearchSheetExporter : IPinSheetExporter
{
    private static readonly XLColor Accent = XLColor.FromHtml("#534B80"), Light = XLColor.FromHtml("#DBD8E9"),
        Backdrop = XLColor.FromHtml("#EDEBF2"), Line = XLColor.FromHtml("#D9D9D9"), Dark = XLColor.FromHtml("#30294B");
    private const int FirstRow = 6;

    // (first column, last column, group title); the columns in between are the template's spacers
    private static readonly (int From, string Title, string[] Cols)[] Groups =
    [
        (3, "", ["Rating", "Comments"]),
        (6, "STEP 1 - TAX OFFICE", ["Type", "Zoning", "City", "Parcel ID"]),
        (11, "STEP 2 - APPRAISER'S OFFICE", ["GIS", "Owner", "Assessed", "Starting Bid", "Acres"]),
        (17, "STEP 3 - GOOGLE MAPS", ["Address", "Light", "Road", "Sewer", "Water"]),
        (23, "STEP 4 - FEMA", ["Flood Zone"]),
        (25, "STEP 5 - MARKET VALUE", ["Avg Price / Acre", "Market Value"]),
        (28, "STEP 6 - CLERK'S OFFICE", ["Liens?", "Amount", "Max Bid"]),
        (32, "MISCELLANEOUS", ["Coordinates", "Elevation"]),
        (35, "LEGAL", ["Legal Description", "Undivided Interest"])
    ];

    // Text that opens its page when clicked; plain text when there is no (valid) URL
    private static void Link(IXLCell cell, string? text, string? url)
    {
        cell.Value = text;
        if (string.IsNullOrEmpty(text) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        cell.SetHyperlink(new XLHyperlink(uri));
        cell.Style.Font.SetFontColor(XLColor.FromHtml("#0563C1")).Font.SetUnderline(); // the usual link blue
    }

    public Task ExportAsync(IEnumerable<MarkerLocation> pins, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Research");
        ws.ShowGridLines = false;

        ws.Style.Font.FontName = "Roboto"; // Excel falls back to another font when Roboto is not installed
        ws.Style.Font.FontSize = 10;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var pinList = pins.ToList();
        var last = Math.Max(pinList.Count + FirstRow - 1, FirstRow + 9);
        const int lastCol = 37;

        // light purple backdrop, a white title band, a purple strip down the left and a short one along the top
        ws.Range(1, 1, last + 2, lastCol).Style.Fill.BackgroundColor = Backdrop;
        ws.Range(2, 2, 2, lastCol - 1).Style.Fill.BackgroundColor = XLColor.White;
        ws.Cell(2, 2).Style.Fill.BackgroundColor = Backdrop;
        ws.Range(1, 1, last + 2, 1).Style.Fill.BackgroundColor = Accent;
        ws.Range(1, 2, 1, 4).Style.Fill.BackgroundColor = Accent;

        ws.Cell(2, 4).Value = pinList.Select(p => p.County).FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "County Name";
        ws.Cell(2, 4).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(Accent);

        // auction date next to the county name; it links to the auction page
        if (pinList.Select(p => p.AuctionDate).FirstOrDefault(d => d.HasValue) is { } auctionDate)
        {
            ws.Range(2, 6, 2, 8).Merge();
            Link(ws.Cell(2, 6), $"Auction {auctionDate:MMM d, yyyy}", pinList.Select(p => p.AuctionUrl).FirstOrDefault(u => !string.IsNullOrEmpty(u)));
            ws.Cell(2, 6).Style.Font.SetFontSize(10);
        }

        using var logo = typeof(PinResearchSheetExporter).Assembly.GetManifestResourceStream("logo.png")!;
        ws.AddPicture(logo).MoveTo(ws.Cell(2, 3), 5, 6).WithSize(35, 28);

        foreach (var (from, title, cols) in Groups)
        {
            var head = ws.Range(4, from, 4, from + cols.Length - 1);
            if (cols.Length > 1 && title != "") head.Merge(); // a one-cell merge is invalid and makes Excel ask to repair the file
            head.FirstCell().Value = title;
            head.Style.Fill.BackgroundColor = Accent;
            head.Style.Font.SetFontColor(XLColor.White).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            for (var i = 0; i < cols.Length; i++) ws.Cell(5, from + i).Value = cols[i];
            var sub = ws.Range(5, from, 5, from + cols.Length - 1);
            sub.Style.Fill.BackgroundColor = Light;
            sub.Style.Font.SetFontColor(Accent).Font.SetBold();
            sub.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        var row = FirstRow;
        var homeValue = new Dictionary<int, decimal>(); // homes: Market Value is the comps' median price, not acres x $/acre
        foreach (var p in pinList)
        {
            var d = p.ExtraDetails;
            if (p.Comps is { } comps)
            {
                if (comps.Kind == "Home" && comps.EstimatedValue is { } homeEstimate) homeValue[row] = homeEstimate;
                else if (comps.PricePerAcre > 0) ws.Cell(row, 25).Value = comps.PricePerAcre; // land: feeds Market Value = Acres x this
            }
            // Skipped or possibly landlocked pins start as C; A and B are for the user to give
            if (p.Status == "Skip" || p.AccessStatus == "None") ws.Cell(row, 3).Value = "C";
            ws.Cell(row, 4).Value = string.Join(" | ", new[] { p.Status, p.Notes, p.AccessText }.Where(s => !string.IsNullOrWhiteSpace(s)));
            ws.Cell(row, 6).Value = d?.GetValueOrDefault("Property Type"); // "Vacant land" / "Structures" / blank (the zoning type is no longer shown here)
            ws.Cell(row, 7).Value = d?.GetValueOrDefault("Zoning");
            ws.Cell(row, 8).Value = d?.GetValueOrDefault("City");
            ws.Cell(row, 9).Value = p.ParcelID;
            Link(ws.Cell(row, 11), p.RegridUrl is null ? null : "Regrid", p.RegridUrl);
            ws.Cell(row, 12).Value = d?.GetValueOrDefault("Owner");
            if (p.AssessedAmount is { } assessed) ws.Cell(row, 13).Value = assessed;
            if (p.BidAmount is { } bid) ws.Cell(row, 14).Value = bid;
            if (double.TryParse(p.Acres, NumberStyles.Float, CultureInfo.InvariantCulture, out var acres)) ws.Cell(row, 15).Value = acres;
            Link(ws.Cell(row, 17), p.Address, p.GoogleMapsUrl);
            Link(ws.Cell(row, 23), d?.GetValueOrDefault("Flood Zone"), p.FemaUrl);
            var elevation = d?.GetValueOrDefault("Elevation");
            if (elevation is not null && p.SlopePercent is { } slope) elevation += $" ({slope}%)";
            ws.Cell(row, 33).Value = elevation;
            if (p.SlopePercent is >= 10) ws.Cell(row, 33).Style.Font.SetFontColor(XLColor.FromHtml("#D64545")).Font.SetBold(); // steep
            ws.Cell(row, 35).Value = d?.GetValueOrDefault("Legal Description");
            if (p.UndividedInterest) ws.Cell(row, 36).Value = "Yes";
            ws.Cell(row, 32).Value = string.Create(CultureInfo.InvariantCulture, $"{p.Latitude:F6}, {p.Longitude:F6}");
            row++;
        }

        // Market Value = Acres x Avg Price / Acre, for the pin rows plus spare rows for the user's own entries
        for (var r = FirstRow; r <= last; r++)
        {
            if (homeValue.TryGetValue(r, out var home)) ws.Cell(r, 26).Value = home;      // home: median price of the comps
            else ws.Cell(r, 26).FormulaA1 = $"=O{r}*Y{r}";        // Market Value = Acres x Avg Price / Acre
            ws.Cell(r, 30).FormulaA1 = $"=Z{r}/(1+100%)-AC{r}";   // Max Bid = Market Value / 2 - lien amount
        }
        ws.Range(FirstRow, 13, last, 14).Style.NumberFormat.Format = "$#,##0.00";
        ws.Range(FirstRow, 25, last, 26).Style.NumberFormat.Format = "$#,##0.00";
        ws.Range(FirstRow, 29, last, 30).Style.NumberFormat.Format = "$#,##0.00";
        ws.Range(FirstRow, 3, last, 3).CreateDataValidation().List("\"A,B,C\"");
        // shortcut: ClosedXML cannot write real Excel checkboxes, so these are a one-click dropdown of box symbols
        foreach (var (a, b) in new[] { (18, 21), (28, 28) }) // Light..Water and Liens?
        {
            var boxes = ws.Range(FirstRow, a, last, b);
            boxes.Value = "☐";
            boxes.Style.Font.SetFontName("Segoe UI Symbol").Font.SetFontSize(12).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            boxes.CreateDataValidation().List("\"☐,☑\"");
        }

        // data blocks: white with thin grey lines (the spacer columns keep the backdrop)
        foreach (var (from, _, cols) in Groups)
        {
            var area = ws.Range(FirstRow, from, last, from + cols.Length - 1);
            area.Style.Fill.BackgroundColor = XLColor.White;
            area.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(Line)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Line);
        }
        var rating = ws.Range(FirstRow, 3, last, 3); // the A / B / C cells
        rating.Style.Fill.BackgroundColor = Light;
        rating.Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetTopBorderColor(Backdrop)
            .Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(Backdrop);
        foreach (var c in new[] { 26, 30 }) // Market Value and Max Bid: tinted from row 5, with white lines between rows
        {
            var tint = ws.Range(5, c, last, c);
            tint.Style.Fill.BackgroundColor = Light;
            tint.Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetTopBorderColor(XLColor.White)
                .Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(XLColor.White);
        }
        ws.Cell(4, 3).Style.Fill.BackgroundColor = Dark; // C4 and D4 are separate cells, as in the template
        ws.Cell(5, 3).Style.Fill.BackgroundColor = Accent;
        ws.Cell(5, 3).Style.Font.FontColor = XLColor.White;
        ws.Range(FirstRow, 15, last, 15).Style.NumberFormat.Format = "0.00";

        foreach (var c in new[] { 2, 5, 10, 16, 22, 24, 27, 31, 34 }) ws.Column(c).Width = 2;
        ws.Columns("C,F:I,K:O,R:U,W,Y:Z,AB:AD,AF:AG,AJ").AdjustToContents(5, last);
        ws.Column(35).Width = 60; // legal descriptions are long; the cell text just runs on
        ws.Range(FirstRow, 36, last, 36).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(FirstRow, 36, last, 36).Style.Font.SetFontColor(XLColor.FromHtml("#D64545")).Font.SetBold(); // flagged
        // widths in Excel units, from pixels: (px - 5) / 7
        ws.Column(1).Width = 2.1; ws.Column(4).Width = 27.9; ws.Column(17).Width = 25; ws.Column(23).Width = 16.4; ws.Column(8).Width = 13.6; ws.Column(12).Width = 26; ws.Column(32).Width = 22;
        ws.SheetView.FreezeRows(5);

        ws.Rows(1, last + 2).Height = 22.5; // 30 px
        ws.Row(1).Height = 12;              // 16 px
        ws.Row(2).Height = 30;              // 40 px
        ws.Row(3).Height = 15.75;           // 21 px
        ws.Range(4, 3, last, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // Rating column
        ws.Range(4, 23, last, 23).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // Flood Zone

        wb.SaveAs(filePath);
        return Task.CompletedTask;
    }
}
