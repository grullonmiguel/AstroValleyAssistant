namespace AstroValley.Domain.Entities;

/// <summary>
/// Provides merge logic between an existing PropertyRecord (RealAuction + old Regrid)
/// and a new PropertyRecord returned from Regrid.
/// Rules:
/// - Never overwrite RealAuction fields.
/// - Only fill fields that are empty or null.
/// - The assessed value and acres count as empty when the source gave 0 (RealAuction parses a missing value as 0), so Regrid's fills them.
///   "N/A" never gets here: importers turn it into null.
/// </summary>
public static class PropertyRecordMerger
{
    public static PropertyRecord Merge(PropertyRecord original, PropertyRecord regrid)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(regrid);

        return original with
        {
            RegridUrl = string.IsNullOrWhiteSpace(regrid.RegridUrl)
                ? original.RegridUrl
                : regrid.RegridUrl,

            Address = string.IsNullOrWhiteSpace(original.Address)
                ? regrid.Address
                : original.Address,

            City = string.IsNullOrWhiteSpace(original.City)
                ? regrid.City
                : original.City,

            Zip = string.IsNullOrWhiteSpace(original.Zip)
                ? regrid.Zip
                : original.Zip,

            Acres = original.Acres is > 0 ? original.Acres : regrid.Acres ?? original.Acres,

            AssessedValue = original.AssessedValue is > 0 ? original.AssessedValue : regrid.AssessedValue ?? original.AssessedValue,

            Owner = string.IsNullOrWhiteSpace(original.Owner)
                ? regrid.Owner
                : original.Owner,

            ZoningCode = string.IsNullOrWhiteSpace(original.ZoningCode)
                ? regrid.ZoningCode
                : original.ZoningCode,

            ZoningType = string.IsNullOrWhiteSpace(original.ZoningType)
                ? regrid.ZoningType
                : original.ZoningType,

            Latitude = string.IsNullOrWhiteSpace(original.Latitude)
                ? regrid.Latitude
                : original.Latitude,

            Longitude = string.IsNullOrWhiteSpace(original.Longitude)
                ? regrid.Longitude
                : original.Longitude,

            GeoCoordinates = string.IsNullOrWhiteSpace(original.GeoCoordinates)
                ? regrid.GeoCoordinates
                : original.GeoCoordinates,

            ElevationHigh = string.IsNullOrWhiteSpace(original.ElevationHigh)
                ? regrid.ElevationHigh
                : original.ElevationHigh,

            ElevationLow = string.IsNullOrWhiteSpace(original.ElevationLow)
                ? regrid.ElevationLow
                : original.ElevationLow,

            ParcelLines = string.IsNullOrWhiteSpace(original.ParcelLines)
                ? regrid.ParcelLines
                : original.ParcelLines,

            FloodZone = string.IsNullOrWhiteSpace(original.FloodZone)
                ? regrid.FloodZone
                : original.FloodZone,

            LegalDescription = string.IsNullOrWhiteSpace(original.LegalDescription)
                ? regrid.LegalDescription
                : original.LegalDescription,

            PropertyType = string.IsNullOrWhiteSpace(original.PropertyType)
                ? regrid.PropertyType
                : original.PropertyType,

            BirdseyeUrl = string.IsNullOrWhiteSpace(original.BirdseyeUrl)
                ? regrid.BirdseyeUrl
                : original.BirdseyeUrl,

            // house facts: what the record already had wins, as above
            UseDescription = string.IsNullOrWhiteSpace(original.UseDescription) ? regrid.UseDescription : original.UseDescription,
            HomeType = string.IsNullOrWhiteSpace(original.HomeType) ? regrid.HomeType : original.HomeType,
            Beds = original.Beds ?? regrid.Beds,
            Baths = original.Baths ?? regrid.Baths,
            LivingSqft = original.LivingSqft ?? regrid.LivingSqft,
            Units = original.Units ?? regrid.Units,
            YearBuilt = original.YearBuilt ?? regrid.YearBuilt,
            BuildingCount = original.BuildingCount ?? regrid.BuildingCount,
            OtherOwners = string.IsNullOrWhiteSpace(original.OtherOwners) ? regrid.OtherOwners : original.OtherOwners,
            State = string.IsNullOrWhiteSpace(original.State) ? regrid.State : original.State,
            County = string.IsNullOrWhiteSpace(original.County) ? regrid.County : original.County,
            LastTransferDate = original.LastTransferDate ?? regrid.LastTransferDate,
            LastSalePrice = original.LastSalePrice ?? regrid.LastSalePrice,
            OwnerCount = original.OwnerCount ?? regrid.OwnerCount,
            Homestead = original.Homestead ?? regrid.Homestead,
            ImprovementValue = original.ImprovementValue ?? regrid.ImprovementValue,
            LandValue = original.LandValue ?? regrid.LandValue,
            PreviousOwner = string.IsNullOrWhiteSpace(original.PreviousOwner) ? regrid.PreviousOwner : original.PreviousOwner
        };
    }
}

