using AstroValley.Application.Interfaces.Import;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Utilities;

namespace AstroValley.Infrastructure.Import;

/// <summary>Fallback for any file no other importer recognizes: matches common column names. Register it last.</summary>
public sealed class GenericImporter : IPropertyImporter
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["ParcelId"] = ["parcel", "parcelid", "pid", "parcelnumber"],
        ["Address"] = ["address", "siteaddress", "location", "street", "parcellocation"],
        ["Owner"] = ["owner", "ownername"],
        ["Bid"] = ["minimumbid", "minimunbid", "openingbid", "startingbid", "amountdue"],
        ["Assessed"] = ["assessed", "assessedvalue", "appraised", "appraisedvalue"],
        ["Acres"] = ["acres", "lotsize", "acreage", "areaacres"]
    };

    public string Name => "Generic";

    public bool CanRead(IReadOnlyCollection<string> headers) => true;

    public PropertyRecord Map(IReadOnlyDictionary<string, string> row)
    {
        string? Pick(string key) => Aliases[key].Select(a => row.GetValueOrDefault(a)).FirstOrDefault(v => v is not null);

        return new PropertyRecord
        {
            ParcelId = Pick("ParcelId") ?? "",
            Address = Pick("Address") ?? "",
            Owner = Pick("Owner") ?? "",
            OpeningBid = Pick("Bid").TryParseDecimal(),
            AssessedValue = Pick("Assessed").TryParseDecimal(),
            Acres = Pick("Acres").TryParseDouble()
        };
    }
}
