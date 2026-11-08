using System.Globalization;
using AstroValley.Application.Interfaces.Import;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Utilities;

namespace AstroValley.Infrastructure.Import;

/// <summary>
/// Parcel Fair export (CSV or XLSX). Brings over only fields the pin JSON already carries; the Regrid scrape fills the rest.
/// Left out on purpose: rent, occupancy, delinquent year, owner mailing address and other columns not wanted yet.
/// </summary>
public sealed class ParcelFairImporter : IPropertyImporter
{
    public string Name => "Parcel Fair";

    public bool CanRead(IReadOnlyCollection<string> headers) => headers.Contains("parcelfairid");

    public PropertyRecord Map(IReadOnlyDictionary<string, string> row)
    {
        string V(string key) => row.GetValueOrDefault(key)?.Trim() ?? "";

        // "lat,lon"; dropped when it is not two numbers in range. Parcel Fair writes "0,0" for parcels it could not locate
        // (306 of 331 in a Perry County file): kept, they would pin to the Atlantic and Regrid could not fill them.
        var xy = V("coordinates").Split(',', StringSplitOptions.TrimEntries);
        var hasXy = xy.Length == 2 && xy[0].TryParseDouble() is { } lat && xy[1].TryParseDouble() is { } lon
                    && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180 && (lat != 0 || lon != 0);

        var county = V("county");
        if (county.Length > 0 && !county.EndsWith("County", StringComparison.OrdinalIgnoreCase)) county += " County";

        // Raw Parcel Number is the number without spaces, with a leading ' (Excel text marker).
        // shortcut: no spaces is a guess at what Regrid accepts; if it does not, use "parcelnumber" here instead.
        var parcel = V("rawparcelnumber").TrimStart('\'');
        if (parcel.Length == 0) parcel = V("parcelnumber").Replace(" ", "");

        // Parcel Fair splits the address over Address (street, no suffix), City, State and Zip: "3368 Lamphier, Memphis, TN 38122".
        // Many rows (Perry County) have no street at all; those stay empty so Regrid can fill them, not ", IL".
        var stateZip = $"{V("state")} {V("zip")}".Trim();
        var address = V("address").Length == 0 ? "" : string.Join(", ", new[] { V("address"), V("city"), stateZip }.Where(p => p.Length > 0));

        return new PropertyRecord
        {
            ParcelId = parcel,
            Address = address,
            City = V("city"),
            Zip = V("zip"),
            County = county,
            Owner = V("ownername"),
            Acres = V("acres").TryParseDouble(),
            AssessedValue = V("assessedvalue").TryParseDecimal(),   // "N/A" -> null
            OpeningBid = V("amountdue").TryParseDecimal(),          // "N/A" -> null: not every parcel has a minimum bid
            LegalDescription = V("legaldescription"),
            PropertyType = StructureKind.FromParcelType(V("parceltype")),
            AuctionDate = DateTime.TryParse(V("nextauction"), CultureInfo.GetCultureInfo("en-US"), out var date) ? date : default,
            Latitude = hasXy ? xy[0] : "",
            Longitude = hasXy ? xy[1] : "",
            GeoCoordinates = hasXy ? $"{xy[0]}, {xy[1]}" : ""
        };
    }
}
