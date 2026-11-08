using AstroValley.Domain.Utilities;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

namespace AstroValley.Infrastructure.Import;

/// <summary>Reads a .csv or .xlsx into rows keyed by normalized header (lower case, no spaces or underscores), ready for an IPropertyImporter.</summary>
public static class SpreadsheetReader
{
    public static (List<string> Headers, List<Dictionary<string, string>> Rows) Read(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => ReadCsv(path),
            ".xlsx" => ReadExcel(path),
            _ => throw new NotSupportedException("Unsupported file type. Use .csv or .xlsx.")
        };

    private static (List<string>, List<Dictionary<string, string>>) ReadCsv(string path)
    {
        using var parser = new TextFieldParser(path) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");

        var headers = (parser.EndOfData ? [] : parser.ReadFields() ?? []).Select(h => h.NormalizeHeaders()).ToList();
        var rows = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.All(string.IsNullOrWhiteSpace)) continue;
            var row = new Dictionary<string, string>();
            for (var i = 0; i < Math.Min(fields.Length, headers.Count); i++) row[headers[i]] = fields[i];
            rows.Add(row);
        }
        return (headers, rows);
    }

    private static (List<string>, List<Dictionary<string, string>>) ReadExcel(string path)
    {
        using var workbook = new XLWorkbook(path);
        var ws = workbook.Worksheets.First();

        var headerCells = ws.Row(1).CellsUsed().ToList();
        var headers = headerCells.Select(c => c.GetString().NormalizeHeaders()).ToList();
        var rows = new List<Dictionary<string, string>>();
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var row = new Dictionary<string, string>();
            for (var i = 0; i < headerCells.Count; i++) row[headers[i]] = ws.Cell(r, headerCells[i].Address.ColumnNumber).GetString();
            if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(row);
        }
        return (headers, rows);
    }
}
