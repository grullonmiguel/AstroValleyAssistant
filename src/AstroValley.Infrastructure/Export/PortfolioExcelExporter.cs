using AstroValley.Application.Interfaces.Export;
using AstroValley.Domain.Entities;
using ClosedXML.Excel;

namespace AstroValley.Infrastructure.Export;

/// <summary>
/// My Properties as a workbook: one row per property, every expense line, and what was spent per year by category
/// (the sheet for the accountant; the purchase itself is its own category in the year it was bought).
/// </summary>
public class PortfolioExcelExporter : IPortfolioExporter
{
    private static readonly XLColor Accent = XLColor.FromHtml("#534B80");
    private const string Money = "$#,##0.00";

    public Task ExportAsync(IEnumerable<OwnedProperty> data, string? destination)
    {
        var properties = data.ToList();
        using var workbook = new XLWorkbook();

        var list = workbook.Worksheets.Add("Properties");
        Header(list, "Lot #", "Address", "City", "State", "Parcel ID", "Purchased By", "Status", "Purchase Date", "Acres", "Size", "Zoning", "Assessed",
            "Purchase Total", "Expenses", "Total Spent", "Market Value", "Minimum Sale", "Asking Price", "Sale Date", "Sale Price", "Closing Costs", "Net Profit");
        var row = 2;
        foreach (var p in properties)
        {
            var r = p.Record;
            var col = 1;
            void Text(string? value) => list.Cell(row, col++).Value = value ?? "";
            void Number(decimal? value, string format = Money) { var c = list.Cell(row, col++); if (value is { } v) { c.Value = v; c.Style.NumberFormat.Format = format; } }
            void Date(DateTime? value) { var c = list.Cell(row, col++); if (value is { } v) { c.Value = v; c.Style.DateFormat.Format = "m/d/yyyy"; } }
            Text(p.LotNumber); Text(r.Address); Text(r.City); Text(r.State); Text(r.ParcelId); Text(p.PurchasedBy); Text(p.Status); Date(p.PurchaseDate);
            Number((decimal?)p.Acres, "0.00"); Text(p is { LotWidthFt: { } w, LotDepthFt: { } d } ? $"{w:0.##}x{d:0.##}" : ""); Text(r.ZoningCode); Number(r.AssessedValue);
            Number(p.PurchaseTotal); Number(p.ExpensesTotal); Number(p.TotalSpent); Number(p.MarketValue); Number(p.MinimumSale); Number(p.AskingPrice);
            Date(p.SaleDate); Number(p.SalePrice); Number(p.SalePrice is null ? null : p.SaleClosingCosts); Number(p.NetProfit);
            row++;
        }
        list.Columns().AdjustToContents();

        var lines = workbook.Worksheets.Add("Expenses");
        Header(lines, "Property", "Parcel ID", "Date", "Year", "Category", "Amount", "Tax Year", "Note");
        row = 2;
        foreach (var (p, date, category, amount, taxYear, note) in Lines(properties).OrderBy(l => l.Date))
        {
            lines.Cell(row, 1).Value = Name(p);
            lines.Cell(row, 2).Value = p.Record.ParcelId;
            lines.Cell(row, 3).Value = date; lines.Cell(row, 3).Style.DateFormat.Format = "m/d/yyyy";
            lines.Cell(row, 4).Value = date.Year;
            lines.Cell(row, 5).Value = category;
            lines.Cell(row, 6).Value = amount; lines.Cell(row, 6).Style.NumberFormat.Format = Money;
            if (taxYear is { } ty) lines.Cell(row, 7).Value = ty;
            lines.Cell(row, 8).Value = note;
            row++;
        }
        lines.Columns().AdjustToContents();

        // year down the side, category across, a total on each
        var byYear = workbook.Worksheets.Add("By Year");
        var all = Lines(properties).ToList();
        var categories = all.Select(l => l.Category).Distinct().OrderBy(c => c == "Purchase" ? "" : c).ToList();
        Header(byYear, ["Year", .. categories, "Total"]);
        row = 2;
        foreach (var year in all.GroupBy(l => l.Date.Year).OrderBy(g => g.Key))
        {
            byYear.Cell(row, 1).Value = year.Key;
            for (var i = 0; i < categories.Count; i++)
                if (year.Where(l => l.Category == categories[i]).Sum(l => l.Amount) is var sum and not 0)
                { byYear.Cell(row, i + 2).Value = sum; byYear.Cell(row, i + 2).Style.NumberFormat.Format = Money; }
            byYear.Cell(row, categories.Count + 2).Value = year.Sum(l => l.Amount);
            byYear.Cell(row, categories.Count + 2).Style.NumberFormat.Format = Money;
            byYear.Cell(row, categories.Count + 2).Style.Font.Bold = true;
            row++;
        }
        byYear.Columns().AdjustToContents();

        workbook.SaveAs(destination ?? throw new ArgumentNullException(nameof(destination)));
        return Task.CompletedTask;
    }

    private static string Name(OwnedProperty p) => p.Record.Address is { Length: > 0 } a ? a : p.Record.ParcelId;

    // every dollar out: the purchase (on its date; a property with no purchase date is left out of the purchase line) and each expense
    private static IEnumerable<(OwnedProperty P, DateTime Date, string Category, decimal Amount, int? TaxYear, string Note)> Lines(IEnumerable<OwnedProperty> properties)
    {
        foreach (var p in properties)
        {
            if (p.PurchaseDate is { } bought && p.PurchaseTotal != 0)
                yield return (p, bought, "Purchase", p.PurchaseTotal, null, "Bid, buyer's premium, taxes at purchase, deed fee");
            foreach (var e in p.Expenses)
                yield return (p, e.Date, e.Category.ToString(), e.Amount, e.TaxYear, e.Note);
        }
    }

    /// <summary>Assert-based check (no test project yet); run by tools/RegridSampleCheck.</summary>
    public static void SelfCheck()
    {
        var path = Path.Combine(Path.GetTempPath(), "AstroValleyPortfolioCheck-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var mackinaw = new OwnedProperty
            {
                Record = new PropertyRecord { Address = "2916 Mackinaw St", ParcelId = "20 1952 00000" },
                PurchaseDate = new DateTime(2025, 9, 4), PurchasePrice = 1800m, TaxesAtPurchase = 139.22m, DeedFee = 30m,
                Expenses = [new(new DateTime(2026, 2, 14), ExpenseCategory.PropertyTax, 150m, taxYear: 2025), new(new DateTime(2026, 6, 1), ExpenseCategory.GrassCutCleanup, 60m), new(new DateTime(2025, 10, 1), ExpenseCategory.Signs, 25m)],
            };
            new PortfolioExcelExporter().ExportAsync([mackinaw, new OwnedProperty()], path).GetAwaiter().GetResult();
            using var book = new XLWorkbook(path);
            var list = book.Worksheet("Properties");
            System.Diagnostics.Debug.Assert(list.LastRowUsed()!.RowNumber() == 3 && list.Cell(2, 15).GetValue<decimal>() == 2384.22m, "one row per property, with its total spent");
            System.Diagnostics.Debug.Assert(book.Worksheet("Expenses").LastRowUsed()!.RowNumber() == 5 && book.Worksheet("Expenses").Cell(2, 5).GetString() == "Purchase", "the purchase and every expense, oldest first");
            var years = book.Worksheet("By Year");
            System.Diagnostics.Debug.Assert(years.Cell(1, 2).GetString() == "Purchase" && years.Cell(2, 1).GetValue<int>() == 2025 && years.Cell(2, years.LastColumnUsed()!.ColumnNumber()).GetValue<decimal>() == 2174.22m
                && years.Cell(3, years.LastColumnUsed()!.ColumnNumber()).GetValue<decimal>() == 210m, "spending by year adds up");
        }
        finally { File.Delete(path); }
    }

    private static void Header(IXLWorksheet ws, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++) ws.Cell(1, i + 1).Value = titles[i];
        var range = ws.Range(1, 1, 1, titles.Length);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = Accent;
        ws.SheetView.FreezeRows(1);
    }
}
