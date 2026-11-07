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
    // a value of 0 (or none) means "not available": its cell is left out and no percentage is shown
    var num = function (v) { var n = parseFloat(String(v == null ? '' : v).replace(/[^0-9.\-]/g, '')); return isNaN(n) ? 0 : n; };
    var assessed = num(d['Assessed']), minBid = num(d['Minimum Bid']);
    var equity = '';
    if (assessed > 0 && minBid > 0) {
        var pct = minBid / assessed * 100;
        var label = pct < 1 ? '&lt;1' : pct < 10 ? pct.toFixed(1) : String(Math.round(pct));
        equity = '<div class="pp-eq"><div class="pp-eqbar"><i style="width:' + Math.min(100, pct) + '%;background:' +
            (pct <= 25 ? '#2E9E5B' : pct <= 60 ? '#CD7B2E' : '#9A9A9A') + '"></i></div>Bid is <b>' + label + '%</b> of assessed value</div>';
    }
    var bid = (assessed > 0 || minBid > 0) ?
        '<div class="pp-money">' +
            (assessed > 0 ? '<div style="flex:1"><div class="pp-label">Assessed Value</div><div class="pp-big">' + money(assessed) + '</div></div>' : '') +
            (minBid > 0 ? '<div style="flex:1"><div class="pp-label">Minimum Bid</div><div class="pp-big">' + money(minBid) + '</div></div>' : '') +
            equity +
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
        bid + statusSection(loc) + '</div>';
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

// Pin size follows acreage on a log scale: 0.25 ac = small, 1 ac = normal, 10 ac = 1.45x, 100 ac = 1.9x (capped). No acreage = normal.
function pinScale(loc) {
    var acres = parseFloat(loc.Acres);
    return acres > 0 ? Math.min(1.9, Math.max(0.75, 1 + 0.45 * Math.log10(acres))) : 1;
}

function makePin(loc, map) {
    var info = pinInfo(loc), scale = pinScale(loc);
    var popup = new maplibregl.Popup({ offset: Math.round(25 * scale), maxWidth: '360px' }).setHTML(popupHtml(loc));
    var marker = new maplibregl.Marker({ color: info.color, scale: scale })
        .setLngLat([loc.Longitude, loc.Latitude])
        .setPopup(popup)
        .addTo(map);
    decoratePin(marker, loc);
    popup.on('open', function () { wireEditor(popup, loc, marker); });
    return marker;
}

// ---- research status + notes (editable on Home, read-only in the exported map) -----------------------------------
var STATUS_COLORS = { Interested: '#2F80ED', Visited: '#17A2B8', Bid: '#E0A800', Skip: '#777777' };

// urgent pulse, status dot on the pin, and a greyed-out look for Skip
function decoratePin(marker, loc) {
    var el = marker.getElement(), st = loc.Status;
    el.classList.toggle('pin-urgent', pinInfo(loc).urgent && st !== 'Skip');
    el.classList.toggle('pin-skip', st === 'Skip');
    if (st && st !== 'Skip' && STATUS_COLORS[st]) { el.setAttribute('data-status', st); el.style.setProperty('--st', STATUS_COLORS[st]); }
    else el.removeAttribute('data-status');
}

function statusSection(loc) {
    var chip = function (st) { return '<button type="button" class="pp-st' + (loc.Status === st ? ' on' : '') + '" data-st="' + st + '" style="--c:' + STATUS_COLORS[st] + '">' + st + '</button>'; };
    if (typeof onPinEdit === 'function') // Home: editable
        return '<div class="pp-sec"><div class="pp-label">Status</div><div class="pp-sts">' + Object.keys(STATUS_COLORS).map(chip).join('') + '</div>' +
            '<textarea class="pp-notes" rows="2" placeholder="Notes...">' + esc(loc.Notes) + '</textarea></div>';
    if (!loc.Status && !loc.Notes) return '';
    return '<div class="pp-sec">' + (loc.Status ? '<div class="pp-sts">' + chip(loc.Status).replace('class="pp-st', 'class="pp-st on') + '</div>' : '') +
        (loc.Notes ? '<div class="pp-notes-ro">' + esc(loc.Notes) + '</div>' : '') + '</div>';
}

function wireEditor(popup, loc, marker) {
    if (typeof onPinEdit !== 'function') return;
    var root = popup.getElement();
    if (!root) return;
    root.querySelectorAll('.pp-st').forEach(function (b) {
        b.onclick = function () {
            loc.Status = loc.Status === b.dataset.st ? null : b.dataset.st; // click again to clear
            decoratePin(marker, loc);
            popup.setHTML(popupHtml(loc));
            wireEditor(popup, loc, marker);
            onPinEdit(loc);
        };
    });
    var notes = root.querySelector('.pp-notes');
    if (notes) notes.onchange = function () { loc.Notes = notes.value.trim() || null; onPinEdit(loc); }; // change fires when the box loses focus
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

// ---- due-diligence overlays: FEMA flood zones + USFWS wetlands (shared) -------------------------------------------
// shortcut: public US government ArcGIS services, no key. FEMA draws only at about street level (1:36,000), wetlands from about
// zoom 10; if a service moves, only these two entries change.
var OVERLAYS = {
    flood: { url: 'https://hazards.fema.gov/arcgis/rest/services/public/NFHL/MapServer', layer: 28, attribution: 'Flood zones: FEMA NFHL' },
    wetlands: { url: 'https://fwspublicservices.wim.usgs.gov/wetlandsmapservice/rest/services/Wetlands/MapServer', layer: 0, attribution: 'Wetlands: USFWS NWI' }
};

function overlaySource(key) {
    var o = OVERLAYS[key];
    return { type: 'raster', tileSize: 256, attribution: o.attribution,
        tiles: [o.url + '/export?bbox={bbox-epsg-3857}&bboxSR=3857&imageSR=3857&size=256,256&format=png32&transparent=true&layers=show:' + o.layer + '&f=image'] };
}

function overlayLayer(key) {
    return { id: key, type: 'raster', source: key, layout: { visibility: 'none' }, paint: { 'raster-opacity': 0.65 } };
}

function setOverlay(map, key, on) {
    if (key === 'epa') setEpa(map, on);
    else map.setLayoutProperty(key, 'visibility', on ? 'visible' : 'none');
}

// Click on the map: ask each visible overlay what is at that point and show it in a popup.
function identify(map, lngLat, on) {
    var point = lngLat.lng + ',' + lngLat.lat;
    var query = function (key, fields) {
        var o = OVERLAYS[key];
        return fetch(o.url + '/' + o.layer + '/query?geometry=' + point + '&geometryType=esriGeometryPoint&inSR=4326&spatialRel=esriSpatialRelIntersects' +
                '&returnGeometry=false&outFields=' + fields + '&f=json')
            .then(function (r) { return r.json(); })
            .then(function (d) { return d.features || []; })
            .catch(function () { return null; }); // null = service unreachable
    };
    var row = function (title, body) {
        return '<div style="margin:0 0 8px"><div class="pp-label">' + title + '</div><div style="font-size:13px;color:#30294B">' + body + '</div></div>';
    };
    var jobs = [];
    if (on.flood) jobs.push(query('flood', 'FLD_ZONE,ZONE_SUBTY,SFHA_TF,STATIC_BFE').then(function (f) {
        if (f == null) return row('Flood zone', 'FEMA service not reachable');
        if (!f.length) return row('Flood zone', 'No flood zone mapped here');
        var a = f[0].attributes;
        return row('Flood zone', '<b>' + esc(a.FLD_ZONE) + '</b>' + (a.SFHA_TF === 'T' ? ' &middot; high-risk area (SFHA)' : '') +
            (a.ZONE_SUBTY ? '<br/>' + esc(a.ZONE_SUBTY) : '') + (a.STATIC_BFE != null && a.STATIC_BFE > -9999 ? '<br/>Base flood elevation: ' + a.STATIC_BFE + ' ft' : ''));
    }));
    if (on.wetlands) jobs.push(query('wetlands', 'WETLAND_TYPE,ATTRIBUTE,ACRES').then(function (f) {
        if (f == null) return row('Wetland', 'Wetlands service not reachable');
        if (!f.length) return row('Wetland', 'No mapped wetland here');
        var a = f[0].attributes;
        return row('Wetland', '<b>' + esc(a.WETLAND_TYPE) + '</b>' + (a.ATTRIBUTE ? ' (' + esc(a.ATTRIBUTE) + ')' : '') +
            (a.ACRES != null ? '<br/>' + Number(a.ACRES).toFixed(2) + ' acres' : ''));
    }));
    if (!jobs.length) return;
    var popup = new maplibregl.Popup({ maxWidth: '300px' }).setLngLat(lngLat)
        .setHTML('<div style="padding:12px 14px;font-family:\'Segoe UI\',sans-serif;font-size:12px;color:#777">Checking...</div>').addTo(map);
    Promise.all(jobs).then(function (rows) {
        popup.setHTML('<div style="padding:12px 14px 6px;font-family:\'Segoe UI\',sans-serif">' + rows.join('') + '</div>');
    });
}

// map click handler shared by both pages; clicks on a pin or during the tour are ignored
function wireIdentify(map, getOn) {
    map.on('click', function (e) {
        var on = getOn();
        if ((!on.flood && !on.wetlands) || TOUR.on) return;
        if (e.originalEvent.target.closest && e.originalEvent.target.closest('.maplibregl-marker')) return;
        if (map.getLayer('epa-points') && map.queryRenderedFeatures(e.point, { layers: ['epa-points'] }).length) return; // the dot has its own popup
        identify(map, e.lngLat, on);
    });
}

// ---- EPA red flags: Superfund (NPL) and brownfield sites as dots (shared) -----------------------------------------
// Point data, so the dots are drawn here from the visible area's features (not server images). shortcut: refetched after each
// move, max 1000 per kind, and only from zoom 8 in; if a county ever shows exactly 1000 dots, zoom in further.
var EPA = {
    url: 'https://geodata.epa.gov/arcgis/rest/services/OEI/FRS_INTERESTS/MapServer',
    minZoom: 8,
    kinds: [{ id: 22, kind: 'Superfund (NPL)', color: '#D6249F' }, { id: 0, kind: 'Brownfield', color: '#8B5E3C' }]
};
var EPA_EMPTY = { type: 'FeatureCollection', features: [] };

function epaSource() { return { type: 'geojson', data: EPA_EMPTY, attribution: 'Sites: EPA FRS' }; }

function epaLayer() {
    return { id: 'epa-points', type: 'circle', source: 'epa', layout: { visibility: 'none' }, paint: {
        'circle-radius': ['interpolate', ['linear'], ['zoom'], 8, 4, 15, 9],
        'circle-color': ['match', ['get', 'kind']].concat(EPA.kinds.reduce(function (a, k) { return a.concat([k.kind, k.color]); }, []), ['#8B5E3C']),
        'circle-stroke-color': '#fff', 'circle-stroke-width': 2 } };
}

var epaAbort = null;
function refreshEpa(map) {
    var src = map.getSource('epa');
    if (map.getZoom() < EPA.minZoom) { src.setData(EPA_EMPTY); return; }
    var b = map.getBounds(), env = [b.getWest(), b.getSouth(), b.getEast(), b.getNorth()].join(',');
    if (epaAbort) epaAbort.abort();
    epaAbort = new AbortController();
    Promise.all(EPA.kinds.map(function (k) {
        return fetch(EPA.url + '/' + k.id + '/query?geometry=' + env + '&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects' +
                '&outFields=PRIMARY_NAME,LOCATION_ADDRESS,CITY_NAME,STATE_CODE,FAC_URL&returnGeometry=true&outSR=4326&resultRecordCount=1000&f=geojson', { signal: epaAbort.signal })
            .then(function (r) { return r.json(); })
            .then(function (d) { return (d.features || []).map(function (f) { f.properties.kind = k.kind; return f; }); });
    })).then(function (parts) {
        src.setData({ type: 'FeatureCollection', features: [].concat.apply([], parts) });
    }).catch(function () { /* cancelled by a newer request, or unreachable: keep what is shown */ });
}

function setEpa(map, on) {
    map.setLayoutProperty('epa-points', 'visibility', on ? 'visible' : 'none');
    if (on) refreshEpa(map); else map.getSource('epa').setData(EPA_EMPTY);
}

// refetch after the map moves while the layer is on; a click on a dot shows what the site is
function wireEpa(map, isOn) {
    var timer;
    map.on('moveend', function () { if (isOn()) { clearTimeout(timer); timer = setTimeout(function () { refreshEpa(map); }, 400); } });
    map.on('mouseenter', 'epa-points', function () { map.getCanvas().style.cursor = 'pointer'; });
    map.on('mouseleave', 'epa-points', function () { map.getCanvas().style.cursor = ''; });
    map.on('click', 'epa-points', function (e) {
        var f = e.features[0], p = f.properties;
        var color = EPA.kinds.filter(function (k) { return k.kind === p.kind; }).map(function (k) { return k.color; })[0];
        var place = [p.LOCATION_ADDRESS, p.CITY_NAME, p.STATE_CODE].filter(Boolean).join(', ');
        new maplibregl.Popup({ maxWidth: '300px' }).setLngLat(f.geometry.coordinates).setHTML(
            '<div style="padding:12px 14px;font-family:\'Segoe UI\',sans-serif">' +
            '<span style="background:' + color + ';color:#fff;border-radius:10px;padding:2px 10px;font-size:11px;font-weight:600">' + esc(p.kind) + '</span>' +
            '<div style="font-size:14px;font-weight:700;color:#30294B;margin:8px 0 2px">' + esc(p.PRIMARY_NAME) + '</div>' +
            '<div style="font-size:12px;color:#777">' + esc(place) + '</div>' +
            (p.FAC_URL ? '<div style="margin-top:8px;font-size:12px"><a class="pp-link pp-accent" href="' + esc(p.FAC_URL).replace(/"/g, '&quot;') + '" target="_blank">EPA facility page &#8599;</a></div>' : '') +
            '</div>').addTo(map);
    });
}
