namespace AstroValleyAssistant.Models
{
    /// <summary>
    /// Represents a geographic point to be rendered as a marker on the map.
    /// </summary>
    public record MarkerLocation
    {
        public string Name { get; init; } = "Unknown Location";
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string? Description { get; init; }

        /// <summary>
        /// Validates that the coordinates are within valid geographic bounds.
        /// </summary>
        public bool IsValid => Latitude is >= -90.0 and <= 90.0 &&
                              Longitude is >= -180.0 and <= 180.0;
    }
}
