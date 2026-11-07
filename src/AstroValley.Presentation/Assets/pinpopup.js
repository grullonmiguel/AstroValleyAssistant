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
        (facts ? '<div style="display:flex;gap:12px;margin-bottom:10px">' + facts + '</div>' : '') +
        bid + '</div>';
}
