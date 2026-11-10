// Pin popup HTML, shared by the Home map (homemaplibre.html) and the exported map (pinsexport.html).
function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }

// small inline icons (Material Design Icons paths, 24x24); `stroke` ones are drawn as lines
var ICONS = {
    home: 'M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z',
    acres: 'M3,3H9V5H5V9H3V3M21,3V9H19V5H15V3H21M3,21V15H5V19H9V21H3M21,21H15V19H19V15H21V21Z',
    person: 'M12,12A4,4 0 1,0 8,8A4,4 0 0,0 12,12M12,14C9.33,14 4,15.34 4,18V20H20V18C20,15.34 14.67,14 12,14Z',
    dollar: 'M7,15H9C9,16.08 10.37,17 12,17C13.63,17 15,16.08 15,15C15,13.9 13.96,13.5 11.76,12.97C9.64,12.44 7,11.78 7,9C7,7.21 8.47,5.69 10.5,5.18V3H13.5V5.18C15.53,5.69 17,7.21 17,9H15C15,7.92 13.63,7 12,7C10.37,7 9,7.92 9,9C9,10.1 10.04,10.5 12.24,11.03C14.36,11.56 17,12.22 17,15C17,16.79 15.53,18.31 13.5,18.82V21H10.5V18.82C8.47,18.31 7,16.79 7,15Z',
    trend: 'M16,6L18.29,8.29L13.41,13.17L9.41,9.17L2,16.59L3.41,18L9.41,12L13.41,16L19.71,9.71L22,12V6H16Z',
    star: 'M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z',
    eye: 'M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z',
    skip: 'M5.59,7.41L10.18,12L5.59,16.59L7,18L13,12L7,6L5.59,7.41M16,6H18V18H16V6Z',
    info: 'M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M13,17H11V11H13V17M13,9H11V7H13V9Z',
    note: 'M14,10H19.5L14,4.5V10M5,3H15L21,9V19A2,2 0 0,1 19,21H5C3.89,21 3,20.1 3,19V5C3,3.89 3.89,3 5,3M5,5V19H19V12H12V5H5Z'
};
function ico(name) { return '<svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor"><path d="' + ICONS[name] + '"/></svg>'; }
var WAVES = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M2,8C5,5 8,11 12,8S19,11 22,8M2,14C5,11 8,17 12,14S19,17 22,14M2,20C5,17 8,23 12,20S19,23 22,20"/></svg>';

function popupHtml(loc, message) {
    var d = loc.ExtraDetails || {};
    // Text becomes a link (opened in the default browser) only when WPF sent a URL for it.
    var link = function (url, html) {
        return url ? '<a class="pp-link" href="' + esc(url).replace(/"/g, '&quot;') + '" target="_blank">' + html + '</a>' : html;
    };
    var money = function (n) { return n.toLocaleString('en-US', { style: 'currency', currency: 'USD' }); };
    // a value of 0 (or none) means "not available": shown as -- , and no percentage is calculated
    var num = function (v) { var n = parseFloat(String(v == null ? '' : v).replace(/[^0-9.\-]/g, '')); return isNaN(n) ? 0 : n; };
    var assessed = num(d['Assessed']), minBid = num(d['Minimum Bid']);

    // header: address (Google Maps) and parcel ID (Regrid); stays put while the body below scrolls
    var head = '<div class="pp-head"><div class="pp-title">' + link(loc.GoogleMapsUrl, esc(loc.Address)) + '</div>' +
        '<div class="pp-pid">' + link(loc.RegridUrl, esc(loc.ParcelID)) + '</div></div>';

    // facts row: acres | flood zone (pill, opens FEMA) | owner; a fact is left out when its data is missing
    var fact = function (icon, label, value, grow) {
        return '<div class="pp-fact' + (grow ? ' pp-grow' : '') + '"><div class="pp-flabel">' + icon + label + '</div>' + value + '</div>';
    };
    var facts = (loc.Acres ? fact(ico('acres'), 'Acres', '<div class="pp-fval pp-fbig">' + (isNaN(parseFloat(loc.Acres)) ? esc(loc.Acres) : parseFloat(loc.Acres).toFixed(2)) + '</div>') : '') +
        (d['Flood Zone'] ? fact(WAVES, 'Flood Zone', '<div>' + (loc.FemaUrl ? '<a class="pp-pill pp-link" href="' + esc(loc.FemaUrl).replace(/"/g, '&quot;') + '" target="_blank">' + esc(d['Flood Zone']) + '</a>'
            : '<span class="pp-pill">' + esc(d['Flood Zone']) + '</span>') + '</div>') : '') +
        (d['Owner'] ? fact(ico('person'), 'Owner', '<div class="pp-fval">' + esc(d['Owner']) + '</div>', true) : '');

    // the building, from the county record: "Duplex-4plex · 2 units · 4 bd / 2 ba · 2,110 sqft · built 1941"; each part only when given
    var n = function (k) { var v = parseFloat(d[k]); return isNaN(v) ? null : v; };
    var houseParts = [d['Home Type'] ? '<b>' + esc(d['Home Type']) + '</b>' : null,
        n('Units') > 1 ? n('Units') + ' units' : null,
        n('Beds') || n('Baths') ? (n('Beds') || '?') + ' bd / ' + (n('Baths') || '?') + ' ba' : null,
        n('Living Sqft') ? n('Living Sqft').toLocaleString('en-US') + ' sqft' : null,
        n('Year Built') ? 'built ' + n('Year Built') : null,
        n('Buildings') > 1 ? n('Buildings') + ' buildings' : null].filter(Boolean);
    var house = (houseParts.length ? '<div class="pp-house" title="' + esc(d['Land Use'] || '').replace(/"/g, '&quot;') + '">' + ico('home') + '<span>' + houseParts.join(' &middot; ') + '</span></div>' : '') +
        (d['House Check'] ? '<div class="pp-flag pp-warning">' + esc(d['House Check']) + '</div>' : '') +
        // tinted by severity, the same colours as the risk flags in the due-diligence dialog
        (loc.Risks || []).map(function (f) {
            return '<div class="pp-flag pp-risk ' + ({ High: 'pp-danger', Caution: 'pp-warning' }[f.Severity] || 'pp-info') + '" title="' + esc(f.Detail).replace(/"/g, '&quot;') + '">' + esc(f.Title) + '</div>';
        }).join('') +
        (loc.EstateOwner && !loc.Risks ? '<div class="pp-flag pp-warning">Estate / heirs owner: probate and title risk. Check who can convey before bidding.</div>' : '');

    // slope across the parcel (Regrid high minus low elevation over the longest side): flat is quiet, steep is loud
    var slope = '';
    if (loc.SlopePercent != null) {
        var sp = loc.SlopePercent;
        var tier = sp < 2 ? ['Flat', 'pp-success'] : sp < 5 ? ['Gentle', 'pp-success'] : sp < 10 ? ['Moderate', 'pp-warning'] : sp < 15 ? ['Steep', 'pp-danger'] : ['Very steep', 'pp-danger'];
        slope = '<div class="pp-flag pp-slope ' + tier[1] + '">' + ico('trend') + 'Slope <b>' + sp + '%</b> &middot; ' + tier[0] + '</div>';
    }

    // value box: collapsed when both are missing or 0; one missing shows --
    var cellMoney = function (label, n) {
        return '<div class="pp-mcell"><span class="pp-badge">' + ico('dollar') + '</span><div><div class="pp-label">' + label + '</div>' +
            '<div class="pp-big">' + (n > 0 ? money(n) : '--') + '</div></div></div>';
    };
    var box = (assessed > 0 || minBid > 0) ? '<div class="pp-money">' + cellMoney('Assessed Value', assessed) + cellMoney('Minimum Bid', minBid) + '</div>' : '';
    var equity = '';
    if (assessed > 0 && minBid > 0) {
        var pct = minBid / assessed * 100;
        var label = pct < 1 ? '&lt;1' : pct < 10 ? pct.toFixed(1) : String(Math.round(pct));
        var color = pct <= 25 ? '#2E9E5B' : pct <= 60 ? '#CD7B2E' : '#9A9A9A';
        equity = '<div class="pp-eq">' +
            '<div class="pp-eqtext" style="color:' + color + '">' + ico('trend') + '<span class="pp-eqlabel">Bid is <b>' + label + '%</b> of assessed value</span></div></div>';
    }

    // actions: landlocked check (left) and the property appraiser, when the record has a link (right)
    var access = accessHtml(loc, message);
    var appraiser = loc.AppraiserUrl ? '<a class="pp-btn" href="' + esc(loc.AppraiserUrl).replace(/"/g, '&quot;') + '" target="_blank">Property Appraiser &#8599;</a>' : '';
    var actions = (access || appraiser) ? '<div class="pp-actions">' + access + appraiser + '</div>' : '';

    // comps the user accepted in the comps dialog: estimated market value (land: acres x $/acre, home: median price)
    var comps = '';
    if (loc.Comps && loc.Comps.EstimatedValue > 0) {
        var c = loc.Comps;
        comps = '<div class="pp-eq"><div class="pp-eqtext">' + ico('dollar') + '<span>Est. value <b>' + money(c.EstimatedValue) + '</b> &middot; ' + c.Count + (c.Kind === 'Home' ? ' home' : ' land') + ' comps' +
            (c.Kind === 'Land' ? ' (' + money(c.PricePerAcre) + '/ac)' : '') +
            (c.RangeLow > 0 ? '<br/>Range ' + money(c.RangeLow) + ' – ' + money(c.RangeHigh) : '') +
            (c.Confidence ? '<br/><span title="' + esc(c.ConfidenceText).replace(/"/g, '&quot;') + '">Confidence <b>' + esc(c.Confidence) + '</b></span>' : '') + '</span></div></div>';
    }
    // Find comps only where WPF can open the dialog (Home map), not in the exported map
    var findComps = typeof onFindComps === 'function' ? '<a href="#" class="pp-zoom pp-comps">' + (loc.Comps ? 'Comps' : 'Find comps') + '</a>' : '';
    // due-diligence checklist (Home map only): "Due diligence 3/27" once started
    var cl = loc.Checklist || [], done = cl.filter(function (i) { return i.Status && i.Status !== 'Pending'; }).length;
    var dueDiligence = typeof onDueDiligence === 'function' ? '<a href="#" class="pp-zoom pp-dd">Due diligence' + (cl.length ? ' ' + done + '/' + cl.length : '') + '</a>' : '';

    // sold/active listing details from the Comps export, one label/value row each
    var compRows = '';
    if (loc.CompFacts) {
        var tips = loc.CompTips || {}; // Weight / Outlier rows explain themselves on hover (black tooltip, see pinsexport.html)
        compRows = '<div style="margin:8px 0;font-size:13px;color:#30294B">' + Object.keys(loc.CompFacts).map(function (k) {
            var tip = tips[k] ? ' data-tip="' + esc(tips[k]).replace(/"/g, '&quot;') + '" class="has-tip"' : '';
            return '<div' + tip + ' style="display:flex;justify-content:space-between;gap:12px;padding:3px 0;border-bottom:1px solid #eee"><span style="color:#777">' + esc(k) +
                (tip ? ' &#9432;' : '') + '</span><b>' + esc(loc.CompFacts[k]) + '</b></div>';
        }).join('') + '</div>';
    }
    if (loc.CompKey && typeof PICKER !== 'undefined' && PICKER) { // Comps review map: keep this comp or leave it out
        var out = EXCLUDED[loc.CompKey]; // excluded from the sidebar or an earlier click
        compRows += '<label class="pk"><span class="pk-t">' + (out ? 'Excluded' : 'Included') + '</span><input type="checkbox" ' + (out ? '' : 'checked ') + 'data-key="' + esc(loc.CompKey).replace(/"/g, '&quot;') + '" onchange="togglePick(this)"><i></i></label>';
    }
    if (loc.IsSubject) compRows = '<div style="margin:8px 0;font-weight:700;color:#2E9E5B">&#127968; Subject property</div>' + compRows;

    return head + '<div class="pp-body"><div class="pp-top">' + auctionBadge(loc) + '<div class="pp-links">' + dueDiligence + findComps +
        '<a href="#" class="pp-zoom">Zoom</a></div></div>' + dataQualityHtml(loc) + '<div class="pp-aerial">' + aerialCardHtml(loc) + '</div>' + compRows +
        (facts ? '<div class="pp-facts">' + facts + '</div>' : '') + house + slope + box + equity + comps + statusSection(loc) + actions + '</div>';
}

// ---- pin colour by auction urgency (shared by Home and the exported map) -----------------------------------------
// red <= 7 days (pulses), orange <= 30, green later, grey past / no date.
function pinInfo(loc) {
    if (loc.PinColor) return { color: loc.PinColor, urgent: false, days: null }; // set by the Comps export
    if (!loc.AuctionDate) return { color: '#9A9A9A', urgent: false, days: null };
    var d = new Date(loc.AuctionDate), t = new Date();
    d.setHours(0, 0, 0, 0); t.setHours(0, 0, 0, 0);
    var days = Math.round((d - t) / 864e5);
    var color = days < 0 ? '#9A9A9A' : days <= 7 ? '#D64545' : days <= 30 ? '#CD7B2E' : '#2E9E5B';
    return { color: color, urgent: days >= 0 && days <= 7, days: days };
}

// Pin size follows acreage on a log scale: 0.25 ac = small, 1 ac = normal, 10 ac = 1.45x, 100 ac = 1.9x (capped). No acreage = normal.
function pinScale(loc) {
    if (loc.IsSubject) return 1.7; // the property being valued: always the biggest pin
    var acres = parseFloat(loc.Acres);
    return acres > 0 ? Math.min(1.9, Math.max(0.75, 1 + 0.45 * Math.log10(acres))) : 1;
}

function makePin(loc, map) {
    if (loc.Status === 'Bid') loc.Status = 'Interested'; // "Bid" status was retired; saved pins keep working
    var info = pinInfo(loc), scale = pinScale(loc);
    // pin-popup: pinned to the bottom right of the map instead of next to the pin (see pinpopup.css)
    var popup = new maplibregl.Popup({ offset: Math.round(25 * scale), maxWidth: '372px', className: 'pin-popup' }).setHTML(popupHtml(loc));
    var marker = new maplibregl.Marker({ color: info.color, scale: scale })
        .setLngLat([loc.Longitude, loc.Latitude])
        .setPopup(popup)
        .addTo(map);
    decoratePin(marker, loc);
    popup.on('open', function () {
        wireEditor(popup, loc, marker); aerialCaption(loc, popup);
        // delegated: the popup's content is replaced on every edit, its root element is not
        popup.getElement().addEventListener('click', function (e) {
            var comps = e.target.closest('.pp-comps');
            if (comps) { e.preventDefault(); onFindComps(loc); return; }
            if (e.target.closest('.pp-dd')) { e.preventDefault(); onDueDiligence(loc); return; }
            if (!e.target.closest('.pp-zoom[href="#"]')) return; // not the zoom link
            e.preventDefault();
            var pad = typeof pinPad === 'function' ? pinPad() : { left: 40, top: 40, bottom: 40, right: 40 }; // Home: clear the side panels
            pad.right = 420; // and the popup
            map.flyTo({ center: [loc.Longitude, loc.Latitude], zoom: 18, padding: pad, essential: true });
        });
    });
    return marker;
}

// ---- research status + notes (editable on Home, read-only in the exported map) -----------------------------------
var STATUS_COLORS = { Interested: '#2F80ED', Visited: '#17A2B8', Skip: '#777777' };
var STATUS_ICONS = { Interested: 'star', Visited: 'eye', Skip: 'skip' };

// urgent pulse, status dot on the pin, and a greyed-out look for Skip
function decoratePin(marker, loc) {
    var el = marker.getElement(), st = loc.Status;
    el.classList.toggle('pin-urgent', pinInfo(loc).urgent && st !== 'Skip');
    el.classList.toggle('pin-skip', st === 'Skip');
    el.classList.toggle('pin-undiv', !!loc.UndividedInterest);
    el.title = loc.UndividedInterest ? 'Possible undivided interest (see legal description)' : '';
    if (st && st !== 'Skip' && STATUS_COLORS[st]) { el.setAttribute('data-status', st); el.style.setProperty('--st', STATUS_COLORS[st]); }
    else el.removeAttribute('data-status');
    // green dot, right side: comps were sent back for this pin
    var dot = el.querySelector('.pin-comps-dot'), has = !!(loc.Comps && loc.Comps.EstimatedValue > 0);
    if (has && !dot) { dot = document.createElement('span'); dot.className = 'pin-comps-dot'; dot.title = 'Has comps'; el.appendChild(dot); }
    else if (!has && dot) dot.remove();
    // bubble with the number of missing research items (green tick = complete); the popup lists them
    var dq = el.querySelector('.pin-dq'), miss = missingData(loc);
    if (!dq && miss) { dq = document.createElement('span'); el.appendChild(dq); }
    if (dq && !miss) dq.remove();
    else if (dq) { dq.className = 'pin-dq ' + dqTier(miss.length); dq.textContent = miss.length || '✓'; dq.title = miss.length ? 'Missing: ' + miss.join(', ') : 'Data complete'; }
}

// What research is still missing on a pin; null for comps-export pins, which are not researched. Same checks as the popup shows.
function missingData(loc) {
    if (loc.CompKey || loc.IsSubject) return null;
    var d = loc.ExtraDetails || {}, miss = [];
    if (!loc.Acres && !loc.ParcelLines) miss.push('Regrid data');
    if (!d['Property Type']) miss.push('Property type');
    if (loc.SlopePercent == null) miss.push('Elevation');
    if (!d['Flood Zone']) miss.push('Flood zone');
    if (!loc.AccessStatus) miss.push('Road access');
    if (!(loc.Comps && loc.Comps.EstimatedValue > 0)) miss.push('Comps');
    if (!(loc.BidAmount > 0)) miss.push('Bid');
    if (!(loc.AssessedAmount > 0)) miss.push('Assessed value');
    return miss;
}
function dqTier(n) { return n === 0 ? 'dq-ok' : n <= 3 ? 'dq-warn' : 'dq-bad'; }

function dataQualityHtml(loc) {
    var miss = missingData(loc);
    if (!miss) return '';
    return '<div class="pp-flag pp-dq ' + dqTier(miss.length) + '">' + (miss.length ? '<b>' + miss.length + ' missing:</b> ' + esc(miss.join(', ')) : '<b>&#10003; Data complete</b>') + '</div>';
}

function statusSection(loc) {
    var chip = function (st, on) {
        return '<button type="button" class="pp-st' + (on ? ' on' : '') + '" data-st="' + st + '" style="--c:' + STATUS_COLORS[st] + '">' + ico(STATUS_ICONS[st]) + st + '</button>';
    };
    if (typeof onPinEdit === 'function') // Home: editable
        return '<div class="pp-sec"><div class="pp-sts">' +
            Object.keys(STATUS_COLORS).map(function (st) { return chip(st, loc.Status === st); }).join('') + '</div>' +
            '<div class="pp-notes-wrap">' + ico('note') + '<textarea class="pp-notes" rows="2" placeholder="Notes...">' + esc(loc.Notes) + '</textarea></div></div>';
    if (!loc.Status && !loc.Notes) return '';
    return '<div class="pp-sec">' + (STATUS_COLORS[loc.Status] ? '<div class="pp-sts">' + chip(loc.Status, true) + '</div>' : '') +
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
    var check = root.querySelector('.pp-check');
    if (check) check.onclick = function (e) {
        e.preventDefault();
        root.querySelector('.pp-access').innerHTML = '<span class="pp-label">Checking roads...</span>';
        checkAccess(loc).then(function (r) {
            loc.AccessStatus = r.status; loc.AccessDistanceFt = r.ft; loc.AccessRoad = r.road; loc.AccessChecked = new Date().toISOString().slice(0, 10);
            decoratePin(marker, loc); popup.setHTML(popupHtml(loc)); wireEditor(popup, loc, marker); onPinEdit(loc);
        }).catch(function () { // service down: nothing is saved, offer to try again
            popup.setHTML(popupHtml(loc, 'Could not reach the Census road service.')); wireEditor(popup, loc, marker);
        });
    };
}

// ---- road access check: public Census TIGER roads, looked up per pin when asked (shared) ---------------------------
// shortcut: TIGER shows public roads only (no private roads, driveways or some rural tracks), so "None" is always "possibly".
var ACCESS = {
    url: 'https://tigerweb.geo.census.gov/arcgis/rest/services/TIGERweb/Transportation/MapServer',
    layers: [2, 6, 8],                                    // primary, secondary and local roads
    where: "MTFCC IN ('S1100','S1200','S1400','S1500')",  // main roads, streets, 4WD trails; no private roads, walkways, parking
    roadFt: 100, nearFt: 1000
};
// shown as a styled tooltip on the button (see .pp-check[data-tip] in pinpopup.css); no double quotes in here
var ACCESS_TIP = 'Checks whether a public road touches or comes near this parcel. Data: US Census Bureau TIGER roads (free, looked up only when you click). ' +
    'Private roads and some rural tracks are not included, so treat a landlocked result as a lead to verify, not proof.';
var ACCESS_COLORS = { Road: '#2E9E5B', Near: '#CD7B2E', None: '#D64545' };

function accessText(loc) {
    var ft = loc.AccessDistanceFt == null ? 0 : Math.round(loc.AccessDistanceFt).toLocaleString('en-US');
    if (loc.AccessStatus === 'Road') return 'Road access: ' + (loc.AccessRoad || 'road') + (loc.AccessDistanceFt > 15 ? ' (' + ft + ' ft)' : '');
    if (loc.AccessStatus === 'Near') return 'Nearest road ' + ft + ' ft away' + (loc.AccessRoad ? ' (' + loc.AccessRoad + ')' : '') + ', no frontage';
    if (loc.AccessStatus === 'None') return 'Possibly landlocked: no road within 1,000 ft';
    return '';
}

function accessHtml(loc, message) {
    var editable = typeof onPinEdit === 'function';
    if (!loc.AccessStatus)
        return editable ? '<div class="pp-access">' + (message ? '<span class="pp-err">' + esc(message) + '</span> ' : '') + '<button type="button" class="pp-check" data-tip="' + ACCESS_TIP + '">Check if landlocked</button></div>' : '';
    return '<div class="pp-access"><span class="pp-dot" style="background:' + ACCESS_COLORS[loc.AccessStatus] + '"></span><b>' + esc(accessText(loc)) +
        '</b><div class="pp-label">Checked ' + esc(loc.AccessChecked || '') + (editable ? ' &middot; <a href="#" class="pp-check" data-tip="' + ACCESS_TIP + '">Check again</a>' : '') + '</div></div>';
}

function geomRings(g, out) {
    if (!g) return out;
    if (g.type === 'FeatureCollection') g.features.forEach(function (f) { geomRings(f.geometry, out); });
    else if (g.type === 'Feature') geomRings(g.geometry, out);
    else if (g.type === 'Polygon') g.coordinates.forEach(function (r) { out.push(r); });
    else if (g.type === 'MultiPolygon') g.coordinates.forEach(function (p) { p.forEach(function (r) { out.push(r); }); });
    return out;
}

// shortest distance between two segments (metres); 0 when they cross
function segDist(a, b, c, d) {
    var cross = function (o, p, q) { return (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0]); };
    var d1 = cross(c, d, a), d2 = cross(c, d, b), d3 = cross(a, b, c), d4 = cross(a, b, d);
    if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return 0;
    var pt = function (p, s, e) {
        var dx = e[0] - s[0], dy = e[1] - s[1], len = dx * dx + dy * dy;
        var t = len ? Math.max(0, Math.min(1, ((p[0] - s[0]) * dx + (p[1] - s[1]) * dy) / len)) : 0;
        return Math.hypot(p[0] - (s[0] + t * dx), p[1] - (s[1] + t * dy));
    };
    return Math.min(pt(a, c, d), pt(b, c, d), pt(c, a, b), pt(d, a, b));
}

// Resolves { status: Road | Near | None, ft, road }; rejects when the road service cannot be reached.
function checkAccess(loc) {
    var rings = geomRings((function () { try { return JSON.parse(loc.ParcelLines); } catch (e) { return null; } })(), []);
    if (!rings.length) rings = [[[loc.Longitude, loc.Latitude]]]; // no outline: measure from the pin
    var lons = [], lats = [];
    rings.forEach(function (r) { r.forEach(function (p) { lons.push(p[0]); lats.push(p[1]); }); });
    var minLon = Math.min.apply(null, lons), maxLon = Math.max.apply(null, lons), minLat = Math.min.apply(null, lats), maxLat = Math.max.apply(null, lats);
    var lat0 = (minLat + maxLat) / 2, lon0 = (minLon + maxLon) / 2, kx = Math.cos(lat0 * Math.PI / 180) * 111320, ky = 110540;
    var dLat = 0.0031, dLon = dLat / Math.cos(lat0 * Math.PI / 180); // about 1,000 ft each way beyond the parcel
    var env = [minLon - dLon, minLat - dLat, maxLon + dLon, maxLat + dLat].join(',');
    var toM = function (p) { return [(p[0] - lon0) * kx, (p[1] - lat0) * ky]; };
    var edges = [];
    rings.forEach(function (r) {
        var pts = r.map(toM);
        if (pts.length === 1) edges.push([pts[0], pts[0]]);
        for (var i = 1; i < pts.length; i++) edges.push([pts[i - 1], pts[i]]);
    });
    return Promise.all(ACCESS.layers.map(function (layer) {
        return fetch(ACCESS.url + '/' + layer + '/query?geometry=' + env + '&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects' +
                '&where=' + encodeURIComponent(ACCESS.where) + '&outFields=NAME,MTFCC&returnGeometry=true&outSR=4326&f=geojson')
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(function (d) { if (d.error) throw new Error('service error'); return d.features || []; });
    })).then(function (parts) {
        var best = Infinity, name = null;
        [].concat.apply([], parts).forEach(function (f) {
            var g = f.geometry;
            if (!g) return;
            var lines = g.type === 'LineString' ? [g.coordinates] : g.type === 'MultiLineString' ? g.coordinates : [];
            lines.forEach(function (line) {
                var pts = line.map(toM);
                for (var i = 1; i < pts.length; i++)
                    edges.forEach(function (e) {
                        var m = segDist(e[0], e[1], pts[i - 1], pts[i]);
                        if (m < best) { best = m; name = f.properties.NAME; }
                    });
            });
        });
        var ft = best * 3.28084;
        var status = ft <= ACCESS.roadFt ? 'Road' : ft <= ACCESS.nearFt ? 'Near' : 'None';
        return { status: status, ft: status === 'None' ? null : Math.round(ft / 10) * 10, road: status === 'None' ? null : (name || null) };
    });
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
    var pill = '<span class="pp-auction" style="--c:' + info.color + '">Auction &middot; ' + date + ' &middot; ' + rel + '</span>';
    // same pill; it opens the auction page when the pin knows its URL
    if (loc.AuctionUrl) pill = '<a class="pp-link" style="text-decoration:none" href="' + esc(loc.AuctionUrl).replace(/"/g, '&quot;') + '" target="_blank">' + pill + '</a>';
    return pill;
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

// ---- aerial photo of the parcel: Esri World Imagery, free public service (no key), the same imagery as the Satellite view (shared) ----
// The image is requested straight from the service by the browser, so nothing is stored. The parcel outline is drawn over it.
// shortcut: imagery age varies by area, so the caption shows the capture date from Esri's metadata layers; Esri asks for attribution
// (the caption names it) and bulk or commercial use is not checked against its terms.
var AERIAL = { url: 'https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer', w: 340, h: 210 };

function merc(lon, lat) { return [6378137 * lon * Math.PI / 180, 6378137 * Math.log(Math.tan(Math.PI / 4 + lat * Math.PI / 360))]; }

// photo with the parcel outline, shown inside the popup under the auction pill
function aerialCardHtml(loc) {
    var W = AERIAL.w, H = AERIAL.h, rings = [], pts = [];
    try { if (loc.ParcelLines) rings = geomRings(JSON.parse(loc.ParcelLines), []); } catch (e) { rings = []; }
    rings.forEach(function (r) { r.forEach(function (c) { pts.push(merc(c[0], c[1])); }); });
    if (!pts.length) pts.push(merc(loc.Longitude, loc.Latitude));
    var xs = pts.map(function (p) { return p[0]; }), ys = pts.map(function (p) { return p[1]; });
    var minx = Math.min.apply(null, xs), maxx = Math.max.apply(null, xs), miny = Math.min.apply(null, ys), maxy = Math.max.apply(null, ys);
    var mpp = Math.max((maxx - minx) * 1.6 / W, (maxy - miny) * 1.6 / H, 120 / W); // parcel plus margin, never closer than ~120 m wide
    var cx = (minx + maxx) / 2, cy = (miny + maxy) / 2, x0 = cx - mpp * W / 2, y1 = cy + mpp * H / 2;
    var src = AERIAL.url + '/export?bbox=' + [x0, cy - mpp * H / 2, cx + mpp * W / 2, y1].join(',') + '&bboxSR=3857&imageSR=3857&size=' + (W * 2) + ',' + (H * 2) + '&format=jpg&f=image';
    var shape = rings.map(function (r) {
        return '<polygon points="' + r.map(function (c) { var m = merc(c[0], c[1]); return ((m[0] - x0) / mpp).toFixed(1) + ',' + ((y1 - m[1]) / mpp).toFixed(1); }).join(' ') + '"/>';
    }).join('') || '<circle cx="' + W / 2 + '" cy="' + H / 2 + '" r="8"/>';
    return '<img src="' + src + '" alt="" onerror="if(this.dataset.r){this.parentNode.style.display=&quot;none&quot;}else{this.dataset.r=1;this.src=this.src+&quot;&amp;r=1&quot;}">' +
        '<svg viewBox="0 0 ' + W + ' ' + H + '">' + shape + '</svg><div class="pp-acap">' + esc(loc._aerial || 'Aerial: Esri World Imagery') + '</div>';
}

// capture date and resolution of the imagery at this spot, from Esri's metadata layers (60 cm, then 1.2 m), looked up when the popup opens
function aerialCaption(loc, popup) {
    if (loc._aerial) return;
    var ask = function (layer) {
        return fetch(AERIAL.url + '/' + layer + '/query?geometry=' + loc.Longitude + ',' + loc.Latitude + '&geometryType=esriGeometryPoint&inSR=4326' +
            '&outFields=SRC_DATE2,SRC_RES&returnGeometry=false&f=json').then(function (r) { return r.json(); })
            .then(function (d) { return d.features && d.features[0] && d.features[0].attributes; });
    };
    ask(10).then(function (a) { return a || ask(11); }).then(function (a) {
        if (!a || !a.SRC_DATE2) return;
        var date = new Date(a.SRC_DATE2).toLocaleDateString('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' });
        loc._aerial = 'Aerial: Esri ' + date + (a.SRC_RES ? ' · ' + a.SRC_RES + ' m' : '');
        var root = popup.getElement(), cap = root && root.querySelector('.pp-acap');
        if (cap) cap.textContent = loc._aerial;
    }).catch(function () { /* service down: keep the plain caption */ });
}
