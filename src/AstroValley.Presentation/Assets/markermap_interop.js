/**
 * AstroValley - MarkerMap Interop Bridge
 * Handles Leaflet rendering, Parcel Geometry, and Google External Links.
 */

let map;
let markerLayer;

function initMap()
{
    if (map) return;

    const satelliteTiles = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19, attribution: 'Tiles © Esri'
    });

    const streetTiles = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19, attribution: '© OpenStreetMap'
    });

    map = L.map('map', {
        center: [37.0902, -95.7129],
        zoom: 4,
        layers: [satelliteTiles]
    });

    const baseMaps = { "Satellite": satelliteTiles, "Street": streetTiles };
    L.control.layers(baseMaps).addTo(map);
    L.control.scale({ position: 'bottomleft' }).addTo(map);

    markerLayer = L.layerGroup().addTo(map);

    //// Add a transparent layer that only shows county boundaries and labels
    //const countyLines = L.tileLayer('https://tiles.stadiamaps.com/tiles/stamen_toner_lines/{z}/{x}/{y}{r}.png', {
    //    opacity: 0.5,
    //    attribution: '© Stadia Maps'
    //}).addTo(map);

    //// Example: Loading a GeoJSON of Florida Counties
    //fetch('https://raw.githubusercontent.com/datasets/geo-boundaries-world-1/master/countries/USA/counties.geojson')
    //    .then(res => res.json())
    //    .then(data =>
    //    {
    //        L.geoJSON(data, {
    //            style: { color: '#666', weight: 1, fillOpacity: 0 },
    //            filter: (feature) => feature.properties.STATE === 'FL' // Only show your state
    //        }).addTo(map);
    //    });
}

function renderMarkers(jsonLocations, clearExisting)
{
    if (!map) initMap();
    if (clearExisting) markerLayer.clearLayers();

    let locations;
    try
    {
        locations = typeof jsonLocations === 'string' ? JSON.parse(jsonLocations) : jsonLocations;
    } catch (e)
    {
        sendErrorToCSharp("JSON Parse failed: " + e.message);
        return;
    }

    if (!locations || locations.length === 0) return;

    // FIX: Ensure this is named 'bounds' and initialized here
    const bounds = [];

    locations.forEach((loc, index) =>
    {
        try
        {
            const lat = parseFloat(loc.Latitude);
            const lon = parseFloat(loc.Longitude);

            if (isNaN(lat) || isNaN(lon))
            {
                throw new Error(`Invalid Coordinates for Row ${index + 1}`);
            }

            // 1. Draw Parcel Boundary (Neon Green Lines)
            if (loc.ParcelLines && loc.ParcelLines.trim() !== "")
            {
                try
                {
                    let geoData = loc.ParcelLines;
                    if (typeof geoData === 'string') geoData = JSON.parse(geoData);

                    // Regrid GeoJSON is [Lon, Lat]
                    const poly = L.geoJSON(geoData, {
                        style: { color: '#39ff14', weight: 3, opacity: 1, fillOpacity: 0.2 }
                    }).addTo(markerLayer);

                    poly.bindPopup(createPopupHtml(loc));
                } catch (innerE)
                {
                    sendErrorToCSharp(`Row ${index + 1} Lines Error: ${innerE.message}`);
                }
            }

            // 2. Add Marker Pin
            const marker = L.marker([lat, lon]).addTo(markerLayer);
            marker.bindPopup(createPopupHtml(loc));

            // Push to the array we defined above
            bounds.push([lat, lon]);

        } catch (rowE)
        {
            sendErrorToCSharp(`Row ${index + 1} Error: ${rowE.message}`);
        }
    });

    // 3. Auto-Zoom using the 'bounds' array
    if (bounds.length > 0)
    {
        try
        {
            map.fitBounds(bounds, { padding: [50, 50], maxZoom: 18 });
        } catch (zoomE)
        {
            console.error("Zoom Error:", zoomE);
        }
    }
}

function createPopupHtml(loc)
{
    // Corrected Template Literals
    const gMaps = `https://www.google.com/maps?q=${loc.Latitude},${loc.Longitude}&z=20`;
    const sView = `https://www.google.com/maps/@?api=1&map_action=pano&viewpoint=${loc.Latitude},${loc.Longitude}`;

    return `
        <div style="min-width: 180px; font-family: sans-serif;">
            <b style="display: block; margin-bottom: 4px;">${loc.Address}</b>
            <div style="display: block; margin-bottom: 4px;">${loc.ParcelID}</div>
            ${loc.Acres ? `<span style="color: #2e7d32; font-weight: bold;">Size: ${loc.Acres} Acres</span><br/>` : ''}
            <hr style="border: 0; border-top: 1px solid #eee; margin: 8px 0;"/>
            <div style="display: flex; justify-content: space-between;">
                <a href="${gMaps}" target="_blank" style="color:#1a73e8; text-decoration:none; font-weight:bold;">📍 Maps</a>
                <a href="${sView}" target="_blank" style="color:#d93025; text-decoration:none; font-weight:bold;">📷 Street View</a>
            </div>
        </div>`;
}

function sendErrorToCSharp(msg)
{
    if (window.chrome && window.chrome.webview)
    {
        window.chrome.webview.postMessage(msg);
    }
    console.error(msg);
}

function focusMarker(lat, lng)
{
    if (!map) return;
    map.flyTo([lat, lng], 18, { animate: true, duration: 1.5 });
}

document.addEventListener('DOMContentLoaded', initMap);