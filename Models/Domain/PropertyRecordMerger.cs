namespace AstroValleyAssistant.Models.Domain
{
    /// <summary>
    /// Provides merge logic between an existing PropertyRecord (RealAuction + old Regrid)
    /// and a new PropertyRecord returned from Regrid.
    /// 
    /// Rules:
    /// - Never overwrite RealAuction fields.
    /// - Only fill fields that are empty or null.
    /// </summary>
    public static class PropertyRecordMerger
    {
        public static PropertyRecord Merge(PropertyRecord original, PropertyRecord regrid)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (regrid == null) throw new ArgumentNullException(nameof(regrid));

            return original with
            {
                // --- Always get the most recent Regrid Url

                RegridUrl = string.IsNullOrWhiteSpace(regrid.RegridUrl)
                    ? original.RegridUrl
                    : regrid.RegridUrl,

                // --- Regrid fields (only overwrite if empty) ---

                City = string.IsNullOrWhiteSpace(original.City)
                    ? regrid.City
                    : original.City,

                Zip = string.IsNullOrWhiteSpace(original.Zip)
                    ? regrid.Zip
                    : original.Zip,

                Acres = original.Acres ?? regrid.Acres,

                Owner = string.IsNullOrWhiteSpace(original.Owner)
                    ? regrid.Owner
                    : original.Owner,

                ZoningCode = string.IsNullOrWhiteSpace(original.ZoningCode)
                    ? regrid.ZoningCode
                    : original.ZoningCode,

                ZoningType = string.IsNullOrWhiteSpace(original.ZoningType)
                    ? regrid.ZoningType
                    : original.ZoningType,

                GeoCoordinates = string.IsNullOrWhiteSpace(original.GeoCoordinates)
                    ? regrid.GeoCoordinates
                    : original.GeoCoordinates,

                ElevationHigh = string.IsNullOrWhiteSpace(original.ElevationHigh)
                    ? regrid.ElevationHigh
                    : original.ElevationHigh,

                ElevationLow = string.IsNullOrWhiteSpace(original.ElevationLow)
                    ? regrid.ElevationLow
                    : original.ElevationLow,

                FloodZone = string.IsNullOrWhiteSpace(original.FloodZone)
                    ? regrid.FloodZone
                    : original.FloodZone,

                BirdseyeUrl = string.IsNullOrWhiteSpace(original.BirdseyeUrl)
                    ? regrid.BirdseyeUrl
                    : original.BirdseyeUrl
            };
        }
    }
}