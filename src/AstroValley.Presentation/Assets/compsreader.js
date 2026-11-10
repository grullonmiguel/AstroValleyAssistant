// Reads the listings on the page in the comps dialog's browser; resolves to [{address, price, acres, sold, url, ...}].
// Ported from the Astro Comps extension (price K/M suffix, lot size in acres or sqft); Zillow card data, list scrolling and
// Redfin ad/nearby exclusions follow the Dirt Flippers Land Analyzer extension. Async: run it with an evaluate that awaits.
(async function () {
    var SQFT = 43560, host = location.hostname;

    function money(t) {
        var m = /\$\s*([\d,]+(?:\.\d+)?)\s*([KkMm])?/.exec(t || '');
        if (!m) return 0;
        var n = parseFloat(m[1].replace(/,/g, '')), s = (m[2] || '').toUpperCase();
        return s === 'K' ? n * 1e3 : s === 'M' ? n * 1e6 : n;
    }
    // "0.25 acres lot", "10,454 sqft lot" (a plain "1,800 sqft" is the house, so it needs "lot"), or just "2.5 Acres"
    function acresOf(t) {
        var m = /([\d,]*\.?\d+)\s*(acres?|ac\b|sq\.?\s?ft\.?|sqft|square feet)\s*\(?lot/i.exec(t || '') || /([\d,]*\.?\d+)\s*(acres?|ac\b)/i.exec(t || '');
        if (!m) return 0;
        var n = parseFloat(m[1].replace(/,/g, ''));
        return /^ac/i.test(m[2]) ? n : n / SQFT;
    }
    // Off-screen cards have no rendered text, so innerText is empty. textContent runs adjacent elements together
    // ("Pending416 Le Blanc Court", "Sold$200,000"), so join the leaf elements with spaces instead.
    function txt(el) {
        if (!el) return '';
        if (el.innerText && el.innerText.trim()) return el.innerText.trim();
        var leaves = [].filter.call(el.querySelectorAll('*'), function (n) { return !n.childElementCount; }).map(function (n) { return n.textContent.trim(); }).filter(Boolean);
        return (leaves.length ? leaves.join(' ') : el.textContent || '').trim();
    }
    // copies only the values that are set, so a sparse source never wipes a filled one
    function fill(out, src) { for (var k in src) if (src[k]) out[k] = src[k]; return out; }
    function path(u) { return (u || '').replace(/^https?:\/\/[^/]+/, '').split('?')[0]; }
    var sleep = function (ms) { return new Promise(function (r) { setTimeout(r, ms); }); };

    // One card read from its text; price 0 when it has none (a placeholder Zillow has not filled in yet).
    function cardRow(card) {
        var text = txt(card), a = card.querySelector("a[role='link'], a[href]"), ad = card.querySelector('address, [data-testid*="address"], [data-test*="address"], .property-name, .bp-Homecard__Address, [data-testid="property-address"]');
        var priceEl = card.querySelector('[data-test="property-card-price"], [data-testid="property-card-price"], [data-testid="property-price"], [data-testid="card-price"], .price-container, .bp-Homecard__Price--value, .priceInfo, [class*="price" i]');
        var price = money(priceEl ? txt(priceEl) : text);
        // the stats line only: descriptions also mention acres, and the house's sqft must not be read as the lot
        var stats = card.querySelector('.bp-Homecard__Stats, [data-testid="property-card-details"], [data-test="property-card-details"]');
        var acres = acresOf(stats ? txt(stats) : text);
        if (!acres && price) { // homes.com land cards: "$17,045 per Acre" gives the lot as price / that, or "Land • 9,583 Sq Ft" is the lot itself
            var pa = /\$\s*([\d,]+(?:\.\d+)?)\s*per\s*acre/i.exec(text), sq = /\bland\b[^\n]*?([\d,]+)\s*sq\.?\s?ft/i.exec(text);
            if (pa && parseFloat(pa[1].replace(/,/g, '')) > 0) acres = price / parseFloat(pa[1].replace(/,/g, ''));
            else if (sq) acres = parseFloat(sq[1].replace(/,/g, '')) / SQFT;
        }
        // the house's floor area: a square-foot figure that is not labelled lot, and not a "Land" card (whose only size is the lot)
        var living = /\bland\b/i.test(stats ? txt(stats) : text) ? null : /([\d,]{3,})\s*(?:sq\.?\s?ft\.?|sqft)(?!\s*\(?lot)/i.exec(stats ? txt(stats) : text);
        var soldOn = /\bsold\s*([A-Za-z]{3})\s+(\d{1,2}),\s*(\d{4})/i.exec(text); // trulia runs it together: SOLDNOV 2, 2026
        // "3 bds | 2 ba" / "3 beds 2 baths" on the stats line; a mobile or manufactured home is a different market from a site-built one
        var st = stats ? txt(stats) : text, beds = /(\d+)\s*(?:bds?|beds?|bd)\b/i.exec(st), baths = /(\d+(?:\.\d+)?)\s*(?:ba|baths?)\b/i.exec(st);
        return {
            addrFrom: ad ? 'card' : 'guess',
            address: (ad ? txt(ad) : ((text.split('\n').find(function (l) { return /\d+\s+\S+.*,/.test(l); }) || (/\S[^$|]*?,\s*[A-Za-z .'-]+,\s*[A-Z]{2}\s*\d{5}/.exec(text) || [''])[0]))).replace(/\s+/g, ' ').trim(),
            price: price, acres: acres, sold: !!soldOn || /\bsold\b(?!\s+summary)/i.test(text), url: a ? a.href : null,
            soldText: soldOn ? soldOn[1] + ' ' + soldOn[2] + ', ' + soldOn[3] : null, homeSqft: living ? parseInt(living[1].replace(/,/g, ''), 10) : null,
            beds: beds ? parseInt(beds[1], 10) : null, baths: baths ? parseFloat(baths[1]) : null,
            homeType: /manufactured|mobile home/i.test(text) ? 'MANUFACTURED' : /multi-family|duplex|triplex|fourplex|quadplex/i.test(text) ? 'MULTI_FAMILY'
                : /townho(use|me)/i.test(text) ? 'TOWNHOUSE' : /\bcondo|\bvilla\b/i.test(text) ? 'CONDO' : null,
            photo: photoOf(card)
        };
    }
    // the listing's photo link (not downloaded here): an off-screen card often keeps it in data-src / srcset until it scrolls into view
    function photoOf(card) {
        var img = card.querySelector('img');
        if (!img) return null;
        var src = [img.getAttribute('src'), img.getAttribute('data-src'), img.getAttribute('data-lazy-src'), (img.getAttribute('srcset') || '').split(' ')[0]]
            .find(function (u) { return u && /^https?:\/\//.test(u) && !/\.svg(\?|$)/i.test(u); });
        return src || null;
    }
    function fromCards(selector, keep) {
        return checked([].filter.call(document.querySelectorAll(selector), keep || function () { return true; })
            .map(cardRow).filter(function (r) { return r.price > 0; }));
    }
    // A comp to check before trusting it: no link to the listing, no address, or an address guessed from the card's text.
    function checked(rows) {
        return rows.map(function (r) {
            r.verify = !r.url ? 'No link to the listing' : !r.address ? 'No address on the card' : r.addrFrom === 'guess' ? 'Address guessed from the card text' : null;
            delete r.addrFrom;
            return r;
        });
    }

    // ---- Zillow --------------------------------------------------------------------------------------------------------
    // Zillow's own data for one home (same shape in the page JSON and on each card); only the fields it gave.
    function homeInfo(h) {
        if (!h) return {};
        return {
            price: h.price, homeSqft: h.livingArea, lat: h.latitude, lon: h.longitude,
            beds: h.bedrooms, baths: h.bathrooms, homeType: h.homeType, // SINGLE_FAMILY, MANUFACTURED, LOT, CONDO, TOWNHOUSE, ...
            acres: h.lotAreaValue > 0 ? (/acre/i.test(h.lotAreaUnit || '') ? h.lotAreaValue : h.lotAreaValue / SQFT) : 0,
            sold: /sold/i.test(h.homeStatus || ''),
            soldText: h.dateSold ? new Date(h.dateSold).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' }) : null,
            address: h.streetAddress ? [h.streetAddress, h.city, (h.state || '') + ' ' + (h.zipcode || '')].join(', ').trim() : null,
            addrFrom: h.streetAddress ? 'data' : null
        };
    }
    var ZILLOW_CARDS = 'article[data-test="property-card"], article.property-card, li[class*="ListItem"] article';

    // tells the app how many listings have been found so far (shown above the browser)
    function progress(found, expected) {
        try { window.chrome.webview.postMessage({ type: 'readProgress', found: found, expected: isFinite(expected) ? expected : null }); } catch (e) { }
    }

    async function zillow() {
        // after a page load (e.g. coming back from the property's own page) the cards draw a moment later
        for (var i = 0; i < 25 && !document.querySelector(ZILLOW_CARDS); i++) await sleep(200);
        // The page JSON goes stale when the search changes in-page (filters, map move), so it only enriches cards it matches by URL.
        var byUrl = {};
        try {
            var data = JSON.parse(document.getElementById('__NEXT_DATA__').textContent);
            (data.props.pageProps.searchPageState.cat1.searchResults.listResults || []).forEach(function (r) {
                var url = r.detailUrl && r.detailUrl.indexOf('http') !== 0 ? 'https://www.zillow.com' + r.detailUrl : r.detailUrl;
                byUrl[path(url)] = fill({ address: r.address, addrFrom: r.address ? 'data' : null, price: r.unformattedPrice || money(r.price), url: url, sold: /sold/i.test(r.statusType || ''), photo: r.imgSrc }, homeInfo(r.hdpData && r.hdpData.homeInfo));
            });
        } catch (e) { }

        var seen = {};
        var read = function () {
            document.querySelectorAll(ZILLOW_CARDS).forEach(function (card) {
                var row = cardRow(card);
                var link = card.querySelector('a[data-test="property-card-link"], a[data-test="property-card-address-link"], a[href*="/homedetails/"]');
                if (link) row.url = link.href;
                fill(row, byUrl[path(row.url)] || {});
                // each card carries its own data (never stale): data-hdp-srp-context = URL-encoded JSON with homeInfo
                try { fill(row, homeInfo(JSON.parse(decodeURIComponent(card.getAttribute('data-hdp-srp-context'))).homeInfo)); } catch (e) { }
                var key = path(row.url) || row.address;
                if (key) seen[key] = fill(seen[key] || {}, row);
            });
            return Object.keys(seen).length;
        };
        await scrollList(read);
        var rows = Object.keys(seen).map(function (k) { return seen[k]; }).filter(function (r) { return r.price > 0; });
        return checked(rows.length ? rows : Object.keys(byUrl).map(function (k) { return byUrl[k]; })); // no cards at all: the page JSON is all there is
    }

    // Zillow only draws the cards near the screen, so step down the results list (and back) and read after each step.
    // Stops at the "N results" count, or when the bottom is reached and nothing new turned up three times.
    async function scrollList(read) {
        var box = ['#search-page-list-container', '[data-testid="search-page-list-container"]', '#grid-search-results'].map(function (s) { return document.querySelector(s); })
            .find(function (e) { return e && e.scrollHeight > e.clientHeight + 50 && /auto|scroll/.test(getComputedStyle(e).overflowY); }) || document.scrollingElement;
        var heading = [].map.call(document.querySelectorAll('h1, h2, h3, [role="heading"], span'), function (e) { return /^([\d,]+)\s+(?:results?|homes?)$/i.exec(e.textContent.trim()); }).find(Boolean);
        var expected = heading ? parseInt(heading[1].replace(/,/g, ''), 10) : Infinity;
        var start = box.scrollTop, until = Date.now() + 20000, same = 0;
        box.scrollTop = 0;
        await sleep(300);
        var count = read();
        progress(count, expected);
        while (count < expected && same < 3 && Date.now() < until) {
            box.scrollTop += Math.max(350, box.clientHeight * 0.7);
            await sleep(600);
            var now = read(), bottom = box.scrollHeight - box.clientHeight - box.scrollTop <= 24;
            progress(now, expected);
            same = bottom && now === count ? same + 1 : 0;
            count = now;
        }
        box.scrollTop = start;
    }

    // ---- Redfin --------------------------------------------------------------------------------------------------------
    // Inline ads reuse the HomeCardContainer classes; cards from "Nearby homes that match your criteria" on are outside the search.
    var REDFIN_ADS = '[data-ad], [data-rf-test-name="inline-display-ad"], .InlineResultStaticPlacement, .advertisement, .sponsored';
    function redfin() {
        var stop = [].find.call(document.querySelectorAll('h1, h2, h3, h4, h5, h6, [role="heading"], div, p, span'), function (n) {
            return n.childElementCount <= 1 && /^(?:nearby homes that match your criteria|end of (?:search )?results\.?)$/i.test(n.textContent.replace(/\s+/g, ' ').trim());
        });
        return fromCards('.HomeCardContainer', function (c) {
            return !c.closest(REDFIN_ADS) && !(stop && (c.contains(stop) || stop.compareDocumentPosition(c) & Node.DOCUMENT_POSITION_FOLLOWING));
        });
    }

    // After Analyze opens a search the page reports "loaded" before its cards are drawn: wait (up to 5 s) for the first card.
    async function ready(selector) {
        for (var i = 0; i < 25 && !document.querySelector(selector); i++) await sleep(200);
        return selector;
    }

    if (/zillow\./.test(host)) return await zillow();
    if (/homes\.com/.test(host)) return fromCards(await ready('li.placard-container, article.placard'));
    if (/realtor\.com/.test(host)) return fromCards(await ready('[data-testid="property-card"], [data-testid="result-card"], li[data-testid*="card"]'));
    if (/trulia\./.test(host)) return fromCards(await ready('li[data-testid^="srp-home-card"]'));
    if (/redfin\./.test(host)) { await ready('.HomeCardContainer'); return redfin(); }
    return fromCards(await ready('article, li[class*="card" i]'));
})()
