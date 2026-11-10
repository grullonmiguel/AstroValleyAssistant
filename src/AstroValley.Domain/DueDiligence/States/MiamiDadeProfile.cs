namespace AstroValley.Domain.DueDiligence;

/// <summary>
/// Miami-Dade County: Florida's rules plus the county's record-office websites. A new county is a class like this one, registered in <see cref="Jurisdictions"/>.
/// Addresses checked on 2026-11-10 with folio 01-3122-052-2400; only the appraiser and the tax collector take the folio in the address.
/// </summary>
public sealed class MiamiDadeProfile : FloridaProfile
{
    public override string Name => "Miami-Dade, Florida";

    public override IReadOnlyList<RecordOfficeLink> Links { get; } =
    [
        new(Offices.Appraiser, "Property Search", "https://apps.miamidadepa.gov/PropertySearch/#/?folio={parcel}",
            "Owners, sales history, assessments and exemptions for this folio."),
        new(Offices.Recorder, "Official Records", "https://onlineservices.miamidadeclerk.gov/officialrecords/",
            "Deeds, mortgages, liens and lis pendens. Search each name and the legal description; registering avoids the search limits."),
        new(Offices.Tax, "Tax account", "https://miamidade.county-taxes.com/public/real_estate/parcels/{parcel}",
            "Amount due, payment history and tax certificates for this folio."),
        new(Offices.Court, "Civil, family and probate cases", "https://www2.miamidadeclerk.gov/ocs/",
            "Foreclosure, probate and judgment cases. Search each name."),
        new(Offices.Court, "Tax deed auction", "https://miamidade.realtaxdeed.com/",
            "Confirm the case status and opening bid on the official auction site."),
        new(Offices.City, "City of Miami lien search", "https://www.miami.gov/Permits-Construction/Property-Information/Run-a-Lien-or-Violation-Search",
            "Only for folios that start with 01 (inside the City of Miami); the fee is $200. Other cities have their own search."),
    ];
}
