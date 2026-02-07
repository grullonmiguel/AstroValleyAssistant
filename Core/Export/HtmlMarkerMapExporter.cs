using AstroValleyAssistant.Models;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AstroValleyAssistant.Core.Export
{
    /// <summary>
    /// Generates a standalone, self-contained HTML file that renders markers on an OpenStreetMap 
    /// using the Leaflet.js library. No API key required.
    /// </summary>
    public class HtmlMarkerMapExporter : IExporter<IEnumerable<MarkerLocation>, string?>
    {
        private readonly string? _htmlTemplate = @"
        <!DOCTYPE html>
        <html>
        <head>
            <title>Astro Valley - Marker Export</title>
            <meta charset='utf-8' />
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
            <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
            <style>
                body { margin: 0; padding: 0; }
                #map { height: 100vh; width: 100vw; }
                .leaflet-popup-content { font-family: sans-serif; }
            </style>
        </head>
        <body>
            <div id='map'></div>
            <script>
                // Initialize map with a global view
                const map = L.map('map').setView([0, 0], 2);

                // Load OpenStreetMap tiles (Free, No API Key)
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 19,
                    attribution: '&copy; OpenStreetMap contributors'
                }).addTo(map);

                // Data injected from C#
                const locations = {{LOCATIONS_JSON}};

                if (locations.length > 0) {
                    const bounds = [];
                    locations.forEach(loc => {
                        const marker = L.marker([loc.Latitude, loc.Longitude]).addTo(map);
                
                        // Construct popup content
                        let popup = `<b>${loc.Name}</b>`;
                        if (loc.Description) popup += `<br/>${loc.Description}`;
                        marker.bindPopup(popup);

                        bounds.push([loc.Latitude, loc.Longitude]);
                    });

                    // Auto-zoom the map to fit all markers
                    map.fitBounds(bounds, { padding: [50, 50] });
                }
            </script>
        </body>
        </html>";

        /// <summary>
        /// Exports the marker collection to a single HTML file.
        /// </summary>
        /// <param name="data">The list of locations to plot.</param>
        /// <param name="destination">The full file path (e.g., C:\Exports\map.html).</param>
        public async Task ExportAsync(IEnumerable<MarkerLocation> data, string? destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Export destination path cannot be empty.", nameof(destination));

            // Serialize the data to JSON for the JS template
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserve PascalCase for the JS loop
            });

            // Inject data into the template
            string finalHtml = _htmlTemplate!.Replace("{{LOCATIONS_JSON}}", json);

            // Write the file to disk
            await File.WriteAllTextAsync(destination, finalHtml, Encoding.UTF8);
        }
    }
}