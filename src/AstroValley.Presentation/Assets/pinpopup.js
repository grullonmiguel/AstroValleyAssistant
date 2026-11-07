// Pin popup HTML, shared by the Home map (homemaplibre.html) and the exported map (pinsexport.html).
function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }

function popupHtml(loc) {
    var d = loc.ExtraDetails || {};
    // Text becomes a link (opened in the default browser) only when WPF sent a URL for it.
    var link = function (url, html, accent) {
        return url ? '<a class="pp-link' + (accent ? ' pp-accent' : '') + '" href="' + esc(url).replace(/"/g, '&quot;') + '" target="_blank">' + html + '</a>' : html;
    };
    var money = function (v) { var n = parseFloat(v); return isNaN(n) ? esc(v) : n.toLocaleString('en-US', { style: 'currency', currency: 'USD' }); };
    // label above value; cells are skipped when the data is missing
    var cell = function (label, value, url) {
        return value ? '<div style="flex:1;min-width:0"><div class="pp-label">' + label + '</div><div class="pp-value">' + link(url, esc(value), true) + '</div></div>' : '';
    };
    var facts = cell('Acres', loc.Acres) + cell('Flood Zone', d['Flood Zone'], loc.FemaUrl) + cell('Owner', d['Owner']);
    var bid = (d['Assessed'] || d['Minimum Bid']) ?
        '<div class="pp-money">' +
            (d['Assessed'] ? '<div style="flex:1"><div class="pp-label">Assessed Value</div><div class="pp-big">' + money(d['Assessed']) + '</div></div>' : '') +
            (d['Minimum Bid'] ? '<div style="flex:1"><div class="pp-label">Minimum Bid</div><div class="pp-big">' + money(d['Minimum Bid']) + '</div></div>' : '') +
        '</div>' : '';
    return '<div style="padding:14px 16px 16px;font-family:\'Segoe UI\',sans-serif">' +
        '<div style="font-size:16px;font-weight:700;color:#30294B;text-transform:uppercase">' + link(loc.GoogleMapsUrl, esc(loc.Address)) + '</div>' +
        // parcel ID on the left, appraiser link (only when the record has one) on the right
        '<div style="display:flex;justify-content:space-between;gap:12px;font-size:12px;color:#777;margin:2px 0 10px">' +
            '<span>' + link(loc.RegridUrl, esc(loc.ParcelID), true) + '</span>' +
            (loc.AppraiserUrl ? '<span>' + link(loc.AppraiserUrl, 'Property Appraiser &#8599;', true) + '</span>' : '') +
        '</div>' +
        auctionBadge(loc) +
        (facts ? '<div style="display:flex;gap:12px;margin-bottom:10px">' + facts + '</div>' : '') +
        bid + '</div>';
}

// ---- pin colour by auction urgency (shared by Home and the exported map) -----------------------------------------
// red <= 7 days (pulses), orange <= 30, green later, grey past / no date.
function pinInfo(loc) {
    if (!loc.AuctionDate) return { color: '#9A9A9A', urgent: false, days: null };
    var d = new Date(loc.AuctionDate), t = new Date();
    d.setHours(0, 0, 0, 0); t.setHours(0, 0, 0, 0);
    var days = Math.round((d - t) / 864e5);
    var color = days < 0 ? '#9A9A9A' : days <= 7 ? '#D64545' : days <= 30 ? '#CD7B2E' : '#2E9E5B';
    return { color: color, urgent: days >= 0 && days <= 7, days: days };
}

function makePin(loc, map) {
    var info = pinInfo(loc);
    var marker = new maplibregl.Marker({ color: info.color })
        .setLngLat([loc.Longitude, loc.Latitude])
        .setPopup(new maplibregl.Popup({ offset: 25, maxWidth: '360px' }).setHTML(popupHtml(loc)))
        .addTo(map);
    if (info.urgent) marker.getElement().classList.add('pin-urgent');
    return marker;
}

// ---- 3D terrain + camera tour (shared) -----------------------------------------------------------------------------
// shortcut: terrain uses free AWS elevation tiles (zoom <= 15); swap the 'dem' source if they ever go away.
var DEM_SOURCE = { type: 'raster-dem', tiles: ['https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png'], encoding: 'terrarium', tileSize: 256, maxzoom: 15 };

// MapLibre cannot drape terrain on the globe, so 3D switches to the flat projection; globe=true restores it afterwards.
function set3d(map, on, globe) {
    if (on) {
        map.setProjection({ type: 'mercator' });
        map.setTerrain({ source: 'dem', exaggeration: 1.4 });
        map.easeTo({ pitch: 60, duration: 1200 });
    } else {
        map.setTerrain(null);
        if (globe) map.setProjection({ type: 'globe' });
        map.easeTo({ pitch: 0, bearing: 0, duration: 800 });
    }
}

var TOUR = { on: false };
// pins: [{marker, lat, lon}]. o: { pad(), is3d(), set3d(on), onState() }. One pass over the pins, any mouse use stops it.
function startTour(map, pins, o) {
    if (TOUR.on || !pins.length) return;
    TOUR.on = true; TOUR.map = map; TOUR.o = o; TOUR.restore3d = !o.is3d();
    if (TOUR.restore3d) o.set3d(true);
    TOUR.user = function (e) { if (e.originalEvent) stopTour(); }; // our own camera moves have no originalEvent
    ['mousedown', 'wheel', 'touchstart'].forEach(function (n) { map.on(n, TOUR.user); });
    var closeAll = function () { pins.forEach(function (p) { if (p.marker.getPopup().isOpen()) p.marker.togglePopup(); }); };
    var i = 0;
    o.onState();
    (function step() {
        if (!TOUR.on) return;
        if (i >= pins.length) { stopTour(); return; }
        var p = pins[i++];
        closeAll();
        map.flyTo({ center: [p.lon, p.lat], zoom: 16.5, pitch: 60, bearing: map.getBearing() + 30, padding: o.pad(), duration: 4000, essential: true });
        // after flyTo: it first cancels any running move (e.g. the 3D tilt) and that fires a moveend we must not take for arrival
        map.once('moveend', function () {
            if (!TOUR.on) return;
            if (!p.marker.getPopup().isOpen()) p.marker.togglePopup();
            map.easeTo({ bearing: map.getBearing() + 40, duration: 5000, easing: function (t) { return t; }, essential: true }); // slow orbit
            TOUR.timer = setTimeout(step, 5500);
        });
    })();
}

function stopTour() {
    if (!TOUR.on) return;
    TOUR.on = false;
    clearTimeout(TOUR.timer);
    ['mousedown', 'wheel', 'touchstart'].forEach(function (n) { TOUR.map.off(n, TOUR.user); });
    TOUR.map.stop();
    if (TOUR.restore3d) TOUR.o.set3d(false);
    TOUR.o.onState();
}

// "Auction · Nov 12, 2026 · in 5 days" pill in the pin's colour
function auctionBadge(loc) {
    var info = pinInfo(loc);
    if (info.days == null) return '';
    var date = new Date(loc.AuctionDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    var rel = info.days === 0 ? 'today' : info.days === 1 ? 'tomorrow' : info.days > 0 ? 'in ' + info.days + ' days' : -info.days + ' days ago';
    return '<div style="margin:0 0 10px"><span style="background:' + info.color + ';color:#fff;border-radius:10px;padding:2px 10px;font-size:11px;font-weight:600">Auction &middot; ' + date + ' &middot; ' + rel + '</span></div>';
}
