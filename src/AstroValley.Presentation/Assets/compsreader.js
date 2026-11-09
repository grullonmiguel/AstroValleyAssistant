// Reads the listings on the page in the comps dialog's browser; returns [{address, price, acres, sold, url}].
// Ported from the Astro Comps extension (price K/M suffix, lot size in acres or sqft).
(function () {
    var SQFT = 43560, host = location.hostname;

    function money(t) {
        var m = /\$\s*([\d,]+(?:\.\d+)?)\s*([KkMm])?/.exec(t || '');
        if (!m) return 0;
        var n = parseFloat(m[1].replace(/,/g, '')), s = (m[2] || '').toUpperCase();
        return s === 'K' ? n * 1e3 : s === 'M' ? n * 1e6 : n;
    }
    // "0.25 acres lot", "10,454 sqft lot" (a plain "1,800 sqft" is the house, so it needs "lot"), or just "2.5 Acres"
    function acresOf(t) {
        var m = /([\d,]*\.?\d+)\s*(acres?|ac\b|sq\.?\s?ft\.?|sqft|square feet)\s*lot/i.exec(t || '') || /([\d,]*\.?\d+)\s*(acres?|ac\b)/i.exec(t || '');
        if (!m) return 0;
        var n = parseFloat(m[1].replace(/,/g, ''));
        return /^ac/i.test(m[2]) ? n : n / SQFT;
    }
    // off-screen cards (homes.com, zillow) have no rendered text yet, so innerText is empty; textContent still has it
    function txt(el) { return ((el && (el.innerText || el.textContent)) || '').trim(); }
    function fromCards(selector) {
        var out = [];
        document.querySelectorAll(selector).forEach(function (card) {
            var text = txt(card), a = card.querySelector('a[href]'), ad = card.querySelector('address, [data-testid*="address"], [data-test*="address"], .property-name');
            var priceEl = card.querySelector('[data-test="property-card-price"], [data-testid="card-price"], .price-container, .bp-Homecard__Price--value, .priceInfo, [class*="price" i]');
            var price = money(priceEl ? txt(priceEl) : text);
            if (!price) return;
            var acres = acresOf(text);
            if (!acres) { // homes.com land cards: "$17,045 per Acre" gives the lot as price / that, or "Land • 9,583 Sq Ft" is the lot itself
                var pa = /\$\s*([\d,]+(?:\.\d+)?)\s*per\s*acre/i.exec(text), sq = /\bland\b[^\n]*?([\d,]+)\s*sq\.?\s?ft/i.exec(text);
                if (pa && parseFloat(pa[1].replace(/,/g, '')) > 0) acres = price / parseFloat(pa[1].replace(/,/g, ''));
                else if (sq) acres = parseFloat(sq[1].replace(/,/g, '')) / SQFT;
            }
            out.push({
                address: (ad ? txt(ad) : ((text.split('\n').find(function (l) { return /\d+\s+\S+.*,/.test(l); }) || (/\S[^$|]*?,\s*[A-Za-z .'-]+,\s*[A-Z]{2}\s*\d{5}/.exec(text) || [''])[0]))).replace(/\s+/g, ' ').trim(),
                price: price, acres: acres, sold: /\bsold\b(?!\s+summary)/i.test(text), url: a ? a.href : null,
                soldText: (/\bsold\s+([A-Za-z]{3}\s+\d{1,2},\s+\d{4})/i.exec(text) || [])[1] || null
            });
        });
        return out;
    }
    function zillow() {
        try { // the search results Zillow embeds in the page (about 40 per page); stale after in-page navigation, so only used when it matches the URL's page
            var data = JSON.parse(document.getElementById('__NEXT_DATA__').textContent);
            var list = data.props.pageProps.searchPageState.cat1.searchResults.listResults || [];
            var rows = list.map(function (r) {
                var h = (r.hdpData && r.hdpData.homeInfo) || {}, v = h.lotAreaValue || 0;
                var acres = /acre/i.test(h.lotAreaUnit || '') ? v : v / SQFT;
                return { address: r.address, price: h.price || r.unformattedPrice || money(r.price), acres: acres,
                    sold: /sold/i.test(r.statusType || '') || /sold/i.test(h.homeStatus || ''),
                    soldText: h.dateSold ? new Date(h.dateSold).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' }) : null, url: r.detailUrl && r.detailUrl.indexOf('http') !== 0 ? 'https://www.zillow.com' + r.detailUrl : r.detailUrl };
            });
            if (rows.length && document.querySelectorAll('article[data-test="property-card"]').length <= rows.length + 2) return rows;
        } catch (e) { }
        return fromCards('article[data-test="property-card"], li[class*="ListItem"] article');
    }

    if (/zillow\./.test(host)) return zillow();
    if (/homes\.com/.test(host)) return fromCards('li.placard-container, article.placard');
    if (/realtor\.com/.test(host)) return fromCards('[data-testid="property-card"], [data-testid="result-card"], li[data-testid*="card"]');
    if (/redfin\./.test(host)) return fromCards('.HomeCardContainer, .bp-Homecard, [class*="HomeCard"]');
    return fromCards('article, li[class*="card" i]');
})()
