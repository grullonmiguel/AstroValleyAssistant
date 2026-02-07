using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Defines strategies for extracting marker data from various input sources.
    /// </summary>
    public interface IMarkerMapParserService
    {
        /// <summary>
        /// Parses raw text input. 
        /// Supports formats: "Name, Lat, Lon" or just "Lat, Lon".
        /// </summary>
        /// <param name="text">The raw string to parse.</param>
        /// <returns>A collection of successfully parsed MarkerLocations.</returns>
        IEnumerable<MarkerLocation> ParseRawText(string text);

        /// <summary>
        /// Parses a file (CSV or XLSX) into marker locations.
        /// </summary>
        /// <param name="filePath">The full path to the source file.</param>
        /// <returns>A collection of MarkerLocations found in the file.</returns>
        Task<IEnumerable<MarkerLocation>> ParseFileAsync(string filePath);
    }
}
