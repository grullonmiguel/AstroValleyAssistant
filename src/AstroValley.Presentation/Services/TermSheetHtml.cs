using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Media;
using AstroValley.Domain.Entities;

namespace AstroValley.Presentation.Services;

/// <summary>
/// The owner-financing term sheet for a property as one printable web page (print to PDF from the browser): logo, contact line,
/// the financing offers side by side, the 0% cash option and the notes. Same layout as the Google Sheet it replaces.
/// </summary>
public static class TermSheetHtml
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Build(OwnedProperty p, string contactLine, string logoHtml)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        static string Money(decimal d) => d.ToString("C2", Us);
        var quotes = p.FinancingQuotes;
        var r = p.Record;
        var address = string.Join(", ", new[] { r.Address, r.City, $"{r.State} {r.Zip}".Trim() }.Where(s => s.Length > 0));

        var html = new StringBuilder();
        html.Append("""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Term sheet</title><style>
            body{font-family:Segoe UI,Roboto,Arial,sans-serif;color:#222;max-width:820px;margin:32px auto;padding:0 24px;font-size:15px}
            .logo{text-align:center;color:#544B7E} .logo svg{height:84px;width:84px;fill:currentColor}
            .contact{text-align:center;letter-spacing:.3em;text-transform:uppercase;font-size:12px;padding:10px 0;border-bottom:3px solid #544B7E;margin-bottom:28px}
            .facts td{padding:6px 0} .facts td:first-child{width:130px} .facts td+td{border-bottom:1px solid #ccc;min-width:320px}
            table.terms{width:100%;border-collapse:collapse;margin:28px 0} .terms th{background:#544B7E;color:#fff;font-weight:400;text-transform:uppercase;padding:10px 12px;text-align:right}
            .terms td{padding:9px 12px;text-align:right;border-bottom:1px solid #ddd} .terms td:first-child,.terms th:first-child{text-align:left} .terms td:first-child{background:#ECEAF3}
            h2{color:#544B7E;font-size:16px;margin:26px 0 8px} ul{margin:0;padding-left:20px;line-height:1.6}
            @media print{body{margin:0 auto}}
            </style></head><body>
            """);
        html.Append($"<div class=\"logo\">{logoHtml}</div>");
        html.Append($"<div class=\"contact\">{E(contactLine)}</div>");
        html.Append("<table class=\"facts\">");
        html.Append($"<tr><td>Address:</td><td>{E(address)}</td></tr>");
        html.Append($"<tr><td>Parcel ID:</td><td>{E(r.ParcelId)}</td></tr>");
        if (p.Acres is { } acres) html.Append($"<tr><td>Lot size:</td><td>{acres.ToString("0.##", Us)} acres{(p is { LotWidthFt: { } w, LotDepthFt: { } d } ? $" ({w:0.##} x {d:0.##} ft)" : "")}</td></tr>");
        if (p.FinancingBasis is { } price) html.Append($"<tr><td>Price:</td><td>{Money(price)}</td></tr>");
        html.Append("</table>");

        if (quotes.Count > 0)
        {
            void Row(string label, Func<FinancingQuote, string> value) =>
                html.Append($"<tr><td>{label}</td>{string.Concat(quotes.Select(q => $"<td>{E(value(q))}</td>"))}</tr>");
            html.Append($"<table class=\"terms\"><tr><th></th>{string.Concat(quotes.Select(q => $"<th>{E(q.Name)}</th>"))}</tr>");
            Row("Down Payment", q => q.DownPercent.ToString("0.##", Us) + "%");
            Row("APR (Interest Rate)", q => q.Apr.ToString("0.00", Us) + "%");
            Row("Loan Term (Months)", q => q.Months.ToString(Us));
            Row("Purchase Price", q => Money(q.Price));
            Row("Down Payment Amount", q => Money(q.DownPayment));
            Row("Amount Financed", q => Money(q.Financed));
            Row("Estimated Monthly Payment", q => Money(q.Monthly));
            Row("Prepayment", _ => "No penalty");
            html.Append("</table>");
        }

        html.Append("""
            <h2>Special Option: 0% Interest – Fast Track</h2><ul>
            <li>0% APR is only available to buyers who pay the full purchase amount in cash at the time of sale.</li>
            <li>Full Payment Required: Buyer must pay 100% of the purchase price at closing to qualify.</li>
            <li>APR: 0%</li><li>Term: Not applicable (since balance is paid in full at closing)</li><li>No prepayment penalty</li></ul>
            <h2>Notes:</h2><ul>
            <li>All terms are subject to final verification, underwriting, and mutual agreement.</li>
            <li>This term sheet is for discussion only; binding obligations will be established by formal contract documents reviewed and signed by both parties.</li>
            <li>Payment calculations can be customized for any specific property value.</li></ul>
            </body></html>
            """);
        return html.ToString();
    }

    /// <summary>The app's two-part logo as inline SVG, from the theme's geometries; empty when they are not there.</summary>
    public static string LogoSvg()
    {
        try
        {
            var size = System.Windows.Application.Current.TryFindResource("Size.Icon.Large") is double s && s > 0 ? s : 32;
            var overlap = 10 / size; // the header draws the second part 10 px over the first at that size
            var parts = new[] { "Icon.Logo1", "Icon.Logo2" }.Select(k => System.Windows.Application.Current.TryFindResource(k)).OfType<Geometry>().Select((g, i) =>
            {
                var b = g.Bounds;
                var d = g.ToString(CultureInfo.InvariantCulture);
                if (d.StartsWith('F')) d = d[2..].TrimStart(); // WPF's fill-rule prefix ("F1") is not SVG
                return FormattableString.Invariant(
                    $"<svg viewBox=\"{b.X} {b.Y} {b.Width} {b.Height}\"{(i > 0 ? $" style=\"margin-left:{-overlap * 84:0}px\"" : "")}><path d=\"{d}\"/></svg>");
            });
            return string.Concat(parts);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException)
        {
            return ""; // a sheet without the logo beats no sheet
        }
    }
}
