using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using AstroValley.Application.Interfaces.Data;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AstroValley.Presentation.ViewModels;

public partial class SiteOption(string name) : ObservableObject
{
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected;
}

/// <summary>The property a comps search is for. Coordinates are optional (a manual entry has none).</summary>
public record CompsSubject(string? Address, double? Acres, string? PropertyType, double? Latitude, double? Longitude, string? Zip = null,
    int? HomeSqft = null, int? Beds = null, double? Baths = null, string? HomeType = null, // house facts from the county record (Regrid), when known
    MarkerLocation? Pin = null); // the Home map pin it came from: the comps map shows its full popup for the subject

/// <summary>
/// The Comps tab: a browser (homes.com, Zillow, ...) plus a sidebar that reads the listings on screen, filters them and
/// gives a market value. Opened by hand it just shows the outcome; opened from a pin or a grid row it can send the value back.
/// </summary>
public partial class CompsViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Comp> _all = [];
    private readonly HashSet<string> _removed = [];
    private readonly HashSet<string> _kept = []; // outliers the user chose to count anyway
    private Action<CompsResult>? _onReturn;
    private ObservableObject? _returnTo;
    private double? _lat, _lon;
    private string? _coordsFor;   // the address _lat/_lon belong to (a pin's, or a typed one placed for the Zillow search)
    private string? _searchKey;   // the property the browser's last site search was built for (see SubjectKey)
    /// <summary>The property's coordinates, only while the address is still the one they were found for.</summary>
    private (double Lat, double Lon)? Coords => _lat is { } la && _lon is { } lo && _coordsFor == Address ? (la, lo) : null;
    private string SubjectKey => $"{Address}|{ZipText}|{IsHome}|{IsMobile}|{IsMultiFamily}|{IsCondo}";
    private string? _type;
    private (double Lat, double Lon)? _subject; // where the property was placed; the export's first pin
    private MarkerLocation? _subjectPin;         // the Home pin the comps were opened from (null from a grid row or a typed address)

    public static readonly (string Name, string Url)[] Sites =
    [
        ("Homes.com", "https://www.homes.com/"),
        ("Zillow", "https://www.zillow.com/homes/recently_sold/"),
        ("Realtor", "https://www.realtor.com/"),
        ("Redfin", "https://www.redfin.com/"),
        ("Trulia", "https://www.trulia.com/")
    ];

    // View hooks (set by CompsView once its browser is ready)
    public Func<Task<string>>? ReadPage { get; set; }
    public Action<string>? NavigateTo { get; set; }
    public Func<string, string, Task<string?>>? ReadPageAt { get; set; } // opens a URL, runs a script on it, returns its JSON
    public Func<bool>? IsOnZillow { get; set; }
    public Func<Task>? GoBackAsync { get; set; } // goes back and waits for that page to load
    public Action? GoBackRequested { get; set; }
    public string? PendingUrl { get; private set; }
    public string StartUrl => PendingUrl ?? Sites[0].Url;

    public ObservableCollection<Comp> Visible { get; } = [];

    /// <summary>The site buttons; the one being visited is highlighted.</summary>
    public ObservableCollection<SiteOption> SiteOptions { get; } = [.. Sites.Select(s => new SiteOption(s.Name) { IsSelected = s.Name == "Homes.com" })];
    partial void OnSiteChanged(string value) { foreach (var o in SiteOptions) o.IsSelected = o.Name == value; NotifySubjectLink(); }

    /// <summary>A comp's address opens the listing in the default browser, not in the embedded one.</summary>
    public void OpenListing(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = u.AbsoluteUri, UseShellExecute = true });
        else Message = "This listing has no link to open."; // shows when the site gave no URL for the card
    }

    // the property being valued; editable so any random property can be typed in
    [ObservableProperty] private string _address = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinAcresTip), nameof(MaxAcresTip))] private string _acresText = "";

    // The Min / Max labels above the acres boxes fill a comparable size band around this lot when clicked (never automatically):
    // about 0.7x-1.3x the lot, the usual starting band for land comps; widen it by hand when there are too few sales.
    private const double BandLow = 0.7, BandHigh = 1.3;
    private string Band(double f) => (PinAcres * f).ToString(PinAcres * f < 1 ? "0.###" : "0.##", CultureInfo.InvariantCulture);
    public string MinAcresTip => PinAcres > 0 ? $"Click to enter {Band(BandLow)} acres ({BandLow:P0} of this {PinAcres:0.###} ac lot)" : "Enter the property's acres to use this";
    public string MaxAcresTip => PinAcres > 0 ? $"Click to enter {Band(BandHigh)} acres ({BandHigh:P0} of this {PinAcres:0.###} ac lot)" : "Enter the property's acres to use this";

    [RelayCommand]
    private void FillAcres(string which)
    {
        if (PinAcres <= 0) { Message = "Enter the property's acres first; Min / Max fill a range around it."; return; }
        if (which == "Min") MinAcres = Band(BandLow); else MaxAcres = Band(BandHigh);
    }
    [ObservableProperty] private string _zipText = "";
    [ObservableProperty] private bool _isHome;

    public string[] SortOptions { get; } = ["Best match", "Featured", "Distance: Nearest first", "Price: Low to High", "Price: High to Low", "Acres: Small to Large", "Acres: Large to Small", "Newest Sold (Date)", "Address: A-Z"];
    [ObservableProperty] private string _sort = "Best match";

    // set when opened from a pin or grid row: the value can be sent back to it
    [ObservableProperty] private bool _hasTarget;

    [ObservableProperty] private bool _useMedian = true;
    // Value from the best few comps only (appraisal practice: 3-5 strong comps beat many weak ones); the rest stay listed, faded.
    // Comps chosen with "Include anyway" always count and take a slot.
    private const int TopCount = 5;
    [ObservableProperty] private bool _useTopFive = true;
    [ObservableProperty] private string _minPrice = "", _maxPrice = "", _minAcres = "", _maxAcres = "", _minPpa = "", _maxPpa = "";
    [ObservableProperty] private string _site = "Homes.com";
    [ObservableProperty] private string _message = "Search and filter on the site, then press Analyze.";
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasComps), nameof(ShowConfidence))] private int _count;
    [ObservableProperty] private int _totalCount; // everything read from the site, before the filter box and map exclusions
    public bool HasComps => Count > 0; // the sort box and View on Map only make sense with comps listed
    [ObservableProperty] private decimal _avgPpa, _medianPpa, _avgPrice, _medianPrice, _estimate;
    [ObservableProperty] private string _ppaText = ""; // the $ / acre used; editable
    [ObservableProperty] private string _estimateText = "--";

    public CompsViewModel(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    private double PinAcres => double.TryParse(AcresText, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;

    /// <summary>Opens the Comps tab for a property; <paramref name="onReturn"/> receives the value when the user sends it back.</summary>
    public void Open(CompsSubject subject, Action<CompsResult> onReturn)
    {
        var main = _serviceProvider.GetRequiredService<MainViewModel>();
        _returnTo = main.CurrentViewModel is CompsViewModel ? _returnTo : main.CurrentViewModel;
        _distanceCts?.Cancel();
        IsMapVisible = false;
        _onReturn = onReturn;
        HasTarget = true;
        (_lat, _lon, _type) = (subject.Latitude, subject.Longitude, subject.PropertyType);
        _coordsFor = subject.Address ?? "";
        _lookedUpFor = null;
        ForgetSubjectLinks();
        Address = subject.Address ?? "";
        ZipText = subject.Zip ?? "";
        if (ZipText.Length == 0) ZipText = Place().Zip ?? "";                // the address may end in one
        if (ZipText.Length == 0 && _lat is not null) _ = FillZipAsync();     // else look it up from the coordinates
        AcresText = subject.Acres is > 0 ? subject.Acres.Value.ToString("0.####", CultureInfo.InvariantCulture) : "";
        // a home when something stands on it and it is not a vacant lot / common area / commercial kind (empty kind = unknown, still a home)
        var kind = subject.HomeType ?? "";
        IsHome = subject.PropertyType == "Structures" && (kind.Length == 0 || HomeKind.IsHome(kind));
        // the county's house facts fill the boxes (they then count as entered, so the Zillow lookup is skipped);
        // never for a vacant lot, whose record may still describe a house that is gone
        var useFacts = IsHome; // a building stands and the kind is a home, or unknown (e.g. "RESIDENTIAL-IMPROVED")
        HomeSqftText = useFacts && subject.HomeSqft is > 0 ? subject.HomeSqft.Value.ToString(CultureInfo.InvariantCulture) : "";
        BedsText = useFacts && subject.Beds is > 0 ? subject.Beds.Value.ToString(CultureInfo.InvariantCulture) : "";
        BathsText = useFacts && subject.Baths is > 0 ? subject.Baths.Value.ToString("0.#", CultureInfo.InvariantCulture) : "";
        IsMobile = kind == HomeKind.Mobile;
        IsMultiFamily = HomeKind.IsMultiUnit(kind);
        IsCondo = kind == HomeKind.Condo;
        IsEditingSubject = Address.Length == 0; // opened from a pin / row: show the summary; Edit opens the boxes
        _all.Clear();
        _removed.Clear();
        _kept.Clear();
        _subject = null;
        _subjectPin = subject.Pin;
        Message = "Search and filter on the site, then press Analyze.";
        Refresh();
        main.CurrentViewModel = this;
        if (_lat is not null) _ = GoSite("Zillow"); // Zillow can be centred on the property; other sites start where the browser was
    }

    // Zillow recently-sold search: around the coordinates when known, else by the address; same property type, 90 days. No lot-size range: the user sets that on the page.
    // shortcut: Zillow's filter keys are undocumented (written from memory), so a filter may be ignored; adjust it on the page.
    private string ZillowUrl()
    {
        object Yes = new { value = true }, No = new { value = false };
        var f = new Dictionary<string, object> { ["rs"] = Yes, ["fsba"] = No, ["fsbo"] = No, ["nc"] = No, ["cmsn"] = No, ["auc"] = No, ["fore"] = No, ["pmf"] = No, ["pf"] = No, ["doz"] = new { value = "90" } };
        // homes: houses and townhouses; or only mobile / manufactured homes, or only multi-family (duplex and up), when the property is one
        if (IsHome)
        {
            var single = !IsMobile && !IsMultiFamily && !IsCondo;
            f["land"] = No; f["sf"] = single ? Yes : No; f["tow"] = single || IsCondo ? Yes : No; f["manu"] = IsMobile ? Yes : No; f["mf"] = IsMultiFamily ? Yes : No;
            f["con"] = IsCondo ? Yes : No; f["apa"] = No; f["apco"] = No;
        }
        else { f["land"] = Yes; foreach (var k in new[] { "sf", "tow", "mf", "con", "apa", "manu", "apco" }) f[k] = No; }
        object state;
        if (Coords is { } c)
        {
            var (lat, lon) = c;
            double d = 0.03, dx = d / Math.Cos(lat * Math.PI / 180);
            state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = "", mapBounds = new { west = lon - dx, east = lon + dx, south = lat - d, north = lat + d }, filterState = f, isListVisible = true };
        }
        else state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = Address.Length > 0 ? Address : Place().Zip ?? "", filterState = f, isListVisible = true };
        return "https://www.zillow.com/homes/recently_sold/?searchQueryState=" + Uri.EscapeDataString(JsonSerializer.Serialize(state));
    }

    // City, state and ZIP from "street, City, ST 12345" (or the ZIP box); the sold-listing URLs below are built from them, like the extension does.
    private (string? City, string? State, string? Zip) Place()
    {
        // "street, City, ST 12345" or "street, City ST 12345" (the comma before the state is optional; ZIP+4 is fine)
        var m = System.Text.RegularExpressions.Regex.Match(Address ?? "", @",\s*([^,]+?),?\s+([A-Za-z]{2})[\s,-]+(\d{5})");
        var zip = ZipText.Trim().Length == 5 ? ZipText.Trim() : m.Success ? m.Groups[3].Value
            : System.Text.RegularExpressions.Regex.Match(Address ?? "", @"\b(\d{5})(?:-\d{4})?\s*$") is { Success: true } z ? z.Groups[1].Value : null;
        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value.ToUpperInvariant(), zip) : (null, null, zip);
    }

    // Recently sold, last 6 months, land only when valuing land. Without a ZIP (or city for Homes.com) a site opens on its home page.
    // shortcut: Realtor's URL with a ZIP is untested here (that site cannot be opened from my tooling); Homes.com needs "City, ST" in the address.
    private string SiteUrl(string name)
    {
        var (city, state, zip) = Place();
        var land = !IsHome;
        var home = Sites.First(s => s.Name == name).Url;
        switch (name)
        {
            case "Zillow": return Coords is not null || Address.Length > 0 || zip is not null ? ZillowUrl() : home;
            case "Homes.com" when city is not null && state is not null:
                return $"https://www.homes.com/{city.ToLowerInvariant().Replace(' ', '-')}-{state.ToLowerInvariant()}/sold/?{(land ? "property_type=32&" : "")}sd=4";
            case "Realtor" when zip is not null: return $"https://www.realtor.com/realestateandhomes-search/{zip}{(land ? "/type-land" : "")}/soldwithin-6";
            case "Redfin" when zip is not null: return $"https://www.redfin.com/zipcode/{zip}/filter/{(land ? "property-type=land," : "")}include=sold-6mo";
            case "Trulia" when zip is not null: return $"https://www.trulia.com/sold/{zip}_zip/{(land ? "LOT%7CLAND_type/" : "")}6_srl/";
            default: return home;
        }
    }

    [RelayCommand]
    private async Task GoSite(string name)
    {
        PendingUrl = await PrepareSearchAsync(name);
        NavigateTo?.Invoke(PendingUrl); // before the browser is ready, the view loads PendingUrl when it starts
    }

    /// <summary>
    /// The search URL for the property in the boxes. A typed-in address is placed first (geocoded), so Zillow opens centred on it
    /// like a pin's; without that, Zillow kept showing whatever area the browser last had.
    /// </summary>
    private async Task<string> PrepareSearchAsync(string site)
    {
        Site = site;
        if (site == "Zillow" && Coords is null && HasValidAddress)
        {
            try
            {
                if (await _serviceProvider.GetRequiredService<IGeocoder>().GeocodeAsync(Address) is { } p)
                    (_lat, _lon, _coordsFor) = (p.Lat, p.Lon, Address);
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException) { } // not placed: Zillow searches by the address text instead
        }
        _searchKey = SubjectKey;
        return SiteUrl(site);
    }

    // view hook: navigate and wait until the page has loaded
    public Func<string, Task>? NavigateAsync { get; set; }

    [RelayCommand]
    private void GoBack() => GoBackRequested?.Invoke();

    [RelayCommand]
    private async Task Analyze()
    {
        if (ReadPage is null) return;
        IsMapVisible = false; // Analyze reads the browser
        IsScraping = true;
        try
        {
            if (CurrentSite() is { } onSite) Site = onSite; // the provider of this Analyze: its link and photo show on the property card

            // 1. the property itself first (before the search: what it fills, e.g. House or Duplex, decides which listings to search for), so the cards are weighed against its size, beds and baths from the start
            try { await LookUpSubjectAsync(); }
            catch (Exception ex) { _subjectNote = "Could not look the property up on Zillow: " + ex.Message; }
            finally { HideBrowser = false; }

            // then, if the boxes now describe a different property than the page was searched for, search again (empty boxes = read the page as it is,
            //    e.g. a ZIP and filters typed straight into Zillow)
            if ((Address.Length > 0 || ZipText.Trim().Length == 5) && SubjectKey != _searchKey && NavigateAsync is not null)
            {
                var site = Site;
                Step($"Searching {site} for this property...");
                var url = await PrepareSearchAsync(site);
                if (url != Sites.First(s => s.Name == site).Url) // the site's home page means "not enough to search with"
                {
                    var note = _subjectNote;
                    Clear(); // the comps read so far were for the previous property
                    if (_lookedUpFor != Address) ForgetSubjectLinks(); // and so were its links (a lookup just now keeps this one's)
                    _subjectNote = note;
                    await NavigateAsync(url);
                }
            }

            // 2. the cards
            await ReadCardsAsync();
            if (HasValidAddress && !_subjectLinks.ContainsKey(Site) && _all.FirstOrDefault(IsSubjectListing) is { } own)
                SetSubjectLink(Site, own.Url, own.PhotoUrl); // the property is among this site's listings
            if (_subjectNote.Length > 0) Message = _subjectNote + " " + Message;

            // 3. distances (the list fills in as they arrive)
            await MeasureDistancesAsync();
        }
        finally { IsScraping = false; Step(""); }
    }

    // Progress in the app's shared ScrapeOverlay (it binds these names). WPF cannot draw over a WebView2, so it covers the comps list,
    // and the browser's own area only while the browser is hidden (visiting the property's page).
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowListOverlay))] private bool _isScraping;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowListOverlay))] private bool _hideBrowser;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private bool _isProgressIndeterminate = true;
    [ObservableProperty] private double _progressValue;
    public bool ShowListOverlay => IsScraping && !HideBrowser;

    private void Step(string text, int done = 0, int? total = null)
    {
        ProgressText = text;
        IsProgressIndeterminate = total is not > 0;
        ProgressValue = total is > 0 ? Math.Min(100, done * 100.0 / total.Value) : 0;
    }

    /// <summary>Called by the reader while it scrolls Zillow's list.</summary>
    public void OnReadProgress(int found, int? expected) =>
        Step(expected is > 0 ? $"Reading listings: {found} of {expected}" : $"Reading listings: {found} found", found, expected);

    private async Task ReadCardsAsync()
    {
        try
        {
            Step("Reading listings...");
            Message = "Reading the page (Zillow: scrolling through the results)...";
            var json = await ReadPage!();
            // ExecuteScriptAsync returns JSON
            var rows = JsonSerializer.Deserialize<List<RawComp>>(json) ?? [];
            int added = 0, merged = 0;
            foreach (var r in rows)
            {
                if (r.price <= 0) continue; // no lot size is fine: houses are valued by price, land rows without one are left out of the stats
                var comp = new Comp(r.address, (decimal)r.price, r.acres, r.sold, r.url, r.soldText, r.homeSqft, r.lat, r.lon, r.verify, r.beds, r.baths, r.homeType, r.photo);
                if (_all.Any(c => Key(c) == Key(comp))) continue;
                if (_all.Any(c => SameSale(c, comp))) { merged++; continue; } // one duplex sale listed under "1613" and "1613-1615"
                _all.Add(comp);
                added++;
            }
            // also comps read before this rule, or by an earlier Analyze: keep the first of each pair
            var earlier = _all.Where((c, i) => _all.Take(i).Any(x => SameSale(x, c))).ToList();
            foreach (var c in earlier) _all.Remove(c);
            merged += earlier.Count;
            _mergeNote = merged > 0 ? $"{merged} listed twice (same sale under two addresses) counted once." : "";
            Message = rows.Count == 0 ? "No listings found on this page. Open a results page and try again."
                : $"Read {rows.Count} listings: {added} added, {rows.Count(r => r.price <= 0)} skipped for no price; {rows.Count(r => r.price > 0 && r.acres <= 0)} have no lot size (used for houses only)."
                    + (merged > 0 ? $" {merged} listed twice (same sale under two addresses) counted once." : "") + $" Total {_all.Count}.";
        }
        catch (Exception ex) { Message = "Could not read the page: " + ex.Message; }
        Refresh();
    }

    // Zillow's property page embeds the home's facts (public records): __NEXT_DATA__ -> componentProps.gdpClientCache (a JSON string) -> .property
    private const string PropertyFactsScript = """
        (function () { try {
            var cp = JSON.parse(document.getElementById('__NEXT_DATA__').textContent).props.pageProps.componentProps;
            var c = typeof cp.gdpClientCache === 'string' ? JSON.parse(cp.gdpClientCache) : cp.gdpClientCache;
            var p = Object.values(c)[0].property;
            return { street: p.streetAddress, zip: p.zipcode, beds: p.bedrooms, baths: p.bathrooms, sqft: p.livingArea, homeType: p.homeType, yearBuilt: p.yearBuilt,
                     lot: p.lotAreaValue || p.lotSize || null, lotUnit: p.lotAreaUnits || null,
                     url: location.href.split('?')[0],
                     photo: p.hiResImageLink || p.desktopWebHdpImageLink || (document.querySelector('meta[property="og:image"]') || {}).content || null };
        } catch (e) { return null; } })()
        """;
    private record ZillowHome(string? street, string? zip, int? beds, double? baths, int? sqft, string? homeType, int? yearBuilt, double? lot, string? lotUnit, string? url, string? photo);

    // --- the property's own listing on each site (link + photo), kept per site and swapped when the browser moves to another site ---
    private readonly Dictionary<string, (string? Url, string? Photo)> _subjectLinks = new();
    public string? SubjectUrl => _subjectLinks.GetValueOrDefault(Site).Url;
    public string? SubjectPhoto => _subjectLinks.GetValueOrDefault(Site).Photo;
    public string SubjectLinkText => $"Open on {Site} ↗";
    private void SetSubjectLink(string site, string? url, string? photo)
    {
        if (url is null && photo is null) return;
        _subjectLinks[site] = (url, photo);
        NotifySubjectLink();
    }
    private void NotifySubjectLink()
    {
        OnPropertyChanged(nameof(SubjectUrl)); OnPropertyChanged(nameof(SubjectPhoto)); OnPropertyChanged(nameof(SubjectLinkText));
    }
    private void ForgetSubjectLinks() { _subjectLinks.Clear(); NotifySubjectLink(); }

    [RelayCommand]
    private void OpenSubjectListing() => OpenListing(SubjectUrl);

    /// <summary>The site the browser is on (by its host), or null for a page that is not one of the listing sites.</summary>
    public Func<string?>? CurrentHost { get; set; }
    private string? CurrentSite() => CurrentHost?.Invoke() is { } host
        ? Sites.Select(s => s.Name).FirstOrDefault(n => host.Contains(new Uri(Sites.First(x => x.Name == n).Url).Host.Replace("www.", ""), StringComparison.OrdinalIgnoreCase))
        : null;

    // the property itself among the cards just read (it sold or is listed on this site): same street number and street name
    private bool IsSubjectListing(Comp c) => StreetNumber(c.Address) is { Length: > 0 } n && n == StreetNumber(Address) && StreetName(c.Address) == StreetName(Address);
    private static readonly HashSet<string> Directions = ["n", "s", "e", "w", "ne", "nw", "se", "sw"];
    private static string StreetName(string? address) =>
        (address ?? "").Split(',')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(t => System.Text.RegularExpressions.Regex.Replace(t.ToLowerInvariant(), @"(?<=\d)(st|nd|rd|th)$|[^a-z0-9]", ""))
            .FirstOrDefault(t => t.Length > 0 && !Directions.Contains(t)) ?? "";
    private string? _lookedUpFor; // the address already looked up, so a repeat Analyze does not leave the results page again
    private string _subjectNote = "", _mergeNote = ""; // kept in the status line after the distances finish // what the Zillow lookup filled in; kept in the status line after the distances finish

    /// <summary>
    /// A house with an address but no sqft yet, read on Zillow: open the property's own Zillow page, fill only the empty Sqft / Beds /
    /// Baths boxes (and Mobile home), then go back to the results. Nothing is filled when Zillow lands on a different property.
    /// </summary>
    private async Task LookUpSubjectAsync()
    {
        _subjectNote = "";
        // Fills only what is missing (acres, House, sqft, beds, baths, mobile / duplex); anything typed stays as typed. Runs once per address.
        var missing = PinAcres <= 0 || !IsHome || HomeSqftText.Trim().Length == 0 || BedsText.Trim().Length == 0 || BathsText.Trim().Length == 0;
        if (!(missing || !_subjectLinks.ContainsKey("Zillow")) || !HasValidAddress || _lookedUpFor == Address || ReadPageAt is null || IsOnZillow?.Invoke() != true) return;
        _lookedUpFor = Address;
        var zip = Place().Zip;
        var text = System.Text.RegularExpressions.Regex.Replace(Address.Contains(zip!) ? Address : $"{Address} {zip}", @"(\d{5})-\d{4}", "$1"); // ZIP+4 -> ZIP
        var url = $"https://www.zillow.com/homes/{System.Text.RegularExpressions.Regex.Replace(text, "[^A-Za-z0-9,]+", "-").Trim('-')}_rb/";
        Message = "Looking up the property's size, beds and baths on Zillow...";
        Step("Looking up the property on Zillow...");
        HideBrowser = true; // the browser leaves the results for a moment; show the progress panel instead
        var json = await ReadPageAt(url, PropertyFactsScript);
        Step("Going back to the results...");
        if (GoBackAsync is not null) await GoBackAsync(); // the cards are read next, so wait for the results page to load again
        var h = json is null ? null : JsonSerializer.Deserialize<ZillowHome>(json);
        if (h is null) { _subjectNote = "Zillow did not open a single page for this address (no match, or a check to solve); enter the sqft from the appraiser."; return; }

        var number = System.Text.RegularExpressions.Regex.Match(Address, @"^\s*(\d+)").Groups[1].Value;
        if (!(h.street ?? "").StartsWith(number + " ") || h.zip is not null && h.zip != zip)
        {
            _subjectNote = $"Zillow matched {h.street}, {h.zip}, not this property; nothing filled.";
            return;
        }
        SetSubjectLink("Zillow", h.url, h.photo);
        var filled = new List<string>();
        if (PinAcres <= 0 && h.lot > 0)
        {
            var acres = (h.lotUnit ?? "").Contains("acre", StringComparison.OrdinalIgnoreCase) ? h.lot.Value : h.lot.Value / 43560;
            AcresText = acres.ToString("0.####", CultureInfo.InvariantCulture);
            filled.Add($"{acres:0.###} acres");
        }
        // a building Zillow calls a home ticks House (and the kind) unless the boxes already say otherwise; a vacant lot ("LOT") leaves House alone
        var isHomeType = h.homeType is { Length: > 0 } t && t != "LOT";
        if (!IsHome && isHomeType) { IsHome = true; filled.Add("house"); }
        if (IsHome && !IsMobile && !IsMultiFamily && !IsCondo)
        {
            if (h.homeType is "CONDO" or "TOWNHOUSE") { IsCondo = true; filled.Add("condo / villa / townhouse"); }
            if (h.homeType == "MANUFACTURED") { IsMobile = true; filled.Add("mobile home"); }
            if (h.homeType == "MULTI_FAMILY") { IsMultiFamily = true; filled.Add("duplex / multi-family"); }
        }
        if (IsHome)
        {
            if (HomeSqftText.Trim().Length == 0 && h.sqft > 0) { HomeSqftText = h.sqft.Value.ToString(CultureInfo.InvariantCulture); filled.Add($"{h.sqft:N0} sqft"); }
            if (BedsText.Trim().Length == 0 && h.beds > 0) { BedsText = h.beds.Value.ToString(CultureInfo.InvariantCulture); filled.Add($"{h.beds} bd"); }
            if (BathsText.Trim().Length == 0 && h.baths > 0) { BathsText = h.baths.Value.ToString("0.#", CultureInfo.InvariantCulture); filled.Add($"{h.baths:0.#} ba"); }
        }
        _subjectNote = filled.Count == 0 ? "Zillow's page had nothing to add to what you entered."
            : $"Filled from Zillow's page: {string.Join(", ", filled)}{(h.yearBuilt > 0 ? $" (built {h.yearBuilt})" : "")}. Check against the appraiser.";
    }

    private record RawComp(string? address, double price, double acres, bool sold, string? url, string? soldText, int? homeSqft, double? lat, double? lon, string? verify, int? beds, double? baths, string? homeType, string? photo);
    // The same sale under two addresses (a duplex as "1613 NW 45th St" and "1613-1615 NW 45th St"): same price, same sale date, same street number.
    // Sale dates only need to be close: the two records of one duplex sale can carry different dates. A card without a date still matches.
    // shortcut: 60 days is a guess; two units at one street number selling for the exact same price within 60 days would also merge.
    private static bool SameSale(Comp a, Comp b) =>
        a.Sold && b.Sold && a.Price == b.Price
        && (a.SoldDate is not { } da || b.SoldDate is not { } db || Math.Abs((da - db).TotalDays) <= 60)
        && StreetNumber(a.Address) is { Length: > 0 } n && n == StreetNumber(b.Address);
    private static string StreetNumber(string? address) => System.Text.RegularExpressions.Regex.Match(address ?? "", @"^\s*(\d+)").Groups[1].Value;

    private static string Key(Comp c) => (c.Address ?? c.Url ?? "") + "|" + (c.Sold ? "S" : "A");

    [RelayCommand]
    private void Clear()
    {
        IsMapVisible = false; // also covers New property and leaving to the pin / row
        _distanceCts?.Cancel();
        _all.Clear();
        _removed.Clear();
        _kept.Clear();
        _subjectNote = _mergeNote = "";
        Message = "Cleared.";
        Refresh();
    }

    [RelayCommand]
    private void Remove(Comp comp)
    {
        _removed.Add(Key(comp));
        _kept.Remove(Key(comp));
        Refresh();
    }

    /// <summary>An outlier's "Include anyway": it counts toward the value again.</summary>
    [RelayCommand]
    private void Include(Comp comp)
    {
        _kept.Add(Key(comp));
        Refresh();
    }

    // left out of the value: removed by hand / on the map, or an outlier not kept
    private bool Excluded(Comp c) => _removed.Contains(Key(c)) || (c.Outlier is not null && !_kept.Contains(Key(c)));

    /// <summary>Forgets the property and any pin / row it was opened from, for a fresh manual search.</summary>
    [RelayCommand]
    private void NewProperty()
    {
        _onReturn = null;
        _returnTo = null;
        (_lat, _lon, _type) = (null, null, null);
        _coordsFor = _searchKey = _lookedUpFor = null;
        ForgetSubjectLinks();
        _subjectPin = null;
        HasTarget = false;
        Address = AcresText = ZipText = HomeSqftText = BedsText = BathsText = "";
        IsHome = IsMobile = IsMultiFamily = IsCondo = false;
        IsEditingSubject = true; // a typed-in property starts with the boxes open
        Clear();
    }

    private async Task FillZipAsync()
    {
        try
        {
            var zip = await _serviceProvider.GetRequiredService<IGeocoder>().ZipAsync(_lat!.Value, _lon!.Value);
            if (zip is not null && ZipText.Length == 0) ZipText = zip;
        }
        catch (OperationCanceledException) { }
    }

    private CancellationTokenSource? _distanceCts;

    private static double Miles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.8; // earth radius in miles
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Distance from the property to each comp: Zillow gives coordinates, the other sites are placed by address (Census, then OpenStreetMap).
    /// Runs in the background and refreshes the list as it goes; the property's own coordinates come from the pin or its address.
    /// </summary>
    private async Task MeasureDistancesAsync()
    {
        _distanceCts?.Cancel();
        var cts = _distanceCts = new CancellationTokenSource();
        var geo = _serviceProvider.GetRequiredService<IGeocoder>();
        try
        {
            (double Lat, double Lon)? home = !HasSubject ? null : Coords is { } placed ? placed
                : Address.Length > 0 ? await geo.GeocodeAsync(Address, cts.Token) : null;
            _subject = home;

            // with a property: every comp gets a distance; without one (a ZIP or a search typed into the site): comps are only placed, for the map
            var todo = _all.Where(c => home is not null ? c.Miles is null : c.Lat is null && c.FoundLat is null).ToList();
            Message = home is null ? $"No property to compare to: listing the {_all.Count} sales as found. Placing {todo.Count} on the map..."
                : $"Measuring distance for {todo.Count} comps...";
            var done = 0;
            foreach (var c in todo)
            {
                Step($"Measuring distances: {done} of {todo.Count}", done, todo.Count);
                (double Lat, double Lon)? p = c.Lat is { } y && c.Lon is { } x ? (y, x)
                    : c.Address is { Length: > 0 } a ? await geo.GeocodeAsync(a, cts.Token) : null;
                if (p is { } q)
                {
                    if (home is { } h) c.Miles = Math.Round(Miles(h.Lat, h.Lon, q.Lat, q.Lon), 1);
                    c.FoundLat = q.Lat; c.FoundLon = q.Lon;
                }
                if (++done % 5 == 0) Refresh();
            }
            Refresh();
            Message = (home is null ? $"No property to compare to: {_all.Count} sales listed as found (no best match or Top 5)."
                : $"Distance found for {_all.Count(c => c.Miles is not null)} of {_all.Count} comps.") + (_mergeNote.Length > 0 ? " " + _mergeNote : "") + (_subjectNote.Length > 0 ? " " + _subjectNote : "");
        }
        catch (OperationCanceledException) { } // a newer Analyze, Clear or property took over
    }

    private static readonly HashSet<string> Triggers = [nameof(IsHome), nameof(Sort), nameof(UseMedian),
        nameof(MinPrice), nameof(MaxPrice), nameof(MinAcres), nameof(MaxAcres), nameof(MinPpa), nameof(MaxPpa), nameof(AcresText),
        nameof(HomeSqftText), nameof(BedsText), nameof(BathsText), nameof(IsMobile), nameof(IsMultiFamily), nameof(IsCondo), nameof(UseTopFive), nameof(Address), nameof(ZipText)]; // address / ZIP: a property to compare to or not // acres: size match is rescored

    // --- the sidebar's property card: a summary, with the boxes behind Edit ---
    [ObservableProperty] private bool _isEditingSubject = true;
    public string SubjectTitle => Address.Split(',')[0].Trim() is { Length: > 0 } street ? street : "No property yet";
    public string SubjectLine2 => string.Join(" • ", new[]
    {
        // the rest of the address ("FL- 33142-0000" -> "FL 33142"), or the Zip box when the address has none
        Address.Contains(',') ? System.Text.RegularExpressions.Regex.Replace(Address[(Address.IndexOf(',') + 1)..], @"(\d{5})-\d{4}|-(?=\s)", "$1").Trim() : ZipText.Trim(),
        PinAcres > 0 ? $"{PinAcres:0.####} acres" : null
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string SubjectLine3 => !IsHome ? "Land" : string.Join(" • ", new[]
    {
        SubjectBeds is { } b ? $"{b} beds" : null,
        SubjectBaths is { } ba ? $"{ba:0.#} baths" : null,
        SubjectSqft is { } sq ? $"{sq:N0} sqft" : null,
        IsMobile ? "Mobile home" : IsMultiFamily ? "Duplex / multi" : IsCondo ? "Condo / villa" : "House"
    }.Where(x => x is not null));
    private static readonly HashSet<string> SubjectInputs = [nameof(Address), nameof(AcresText), nameof(ZipText), nameof(HomeSqftText),
        nameof(BedsText), nameof(BathsText), nameof(IsHome), nameof(IsMobile), nameof(IsMultiFamily), nameof(IsCondo)];

    /// <summary>
    /// A property to compare the comps to: a full address (street number + ZIP) or a pin's coordinates. Without one (just a ZIP, or a search
    /// typed straight into the site) the comps are listed as found: no best-match order, no Top 5, no confidence; the map has no subject pin.
    /// </summary>
    public bool HasSubject => HasValidAddress || Coords is not null;
    public bool ShowConfidence => HasComps && HasSubject;

    // --- filters: "(2 active)" in the header, and one button to empty them ---
    public int ActiveFilterCount => new[] { MinAcres, MaxAcres, MinPrice, MaxPrice, MinPpa, MaxPpa }.Count(v => !string.IsNullOrWhiteSpace(v));
    public string FiltersSummary => ActiveFilterCount == 0 ? "(none)" : $"({ActiveFilterCount} active)";

    [RelayCommand]
    private void ClearFilters() => MinAcres = MaxAcres = MinPrice = MaxPrice = MinPpa = MaxPpa = "";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } sn && SubjectInputs.Contains(sn))
        {
            OnPropertyChanged(nameof(SubjectTitle)); OnPropertyChanged(nameof(SubjectLine2)); OnPropertyChanged(nameof(SubjectLine3));
            OnPropertyChanged(nameof(HasSubject)); OnPropertyChanged(nameof(ShowConfidence));
        }
        if (e.PropertyName is nameof(MinAcres) or nameof(MaxAcres) or nameof(MinPrice) or nameof(MaxPrice) or nameof(MinPpa) or nameof(MaxPpa))
            OnPropertyChanged(nameof(FiltersSummary));
        if (e.PropertyName is { } n && Triggers.Contains(n)) Refresh();
        if (e.PropertyName is nameof(PpaText) or nameof(Address) or nameof(ZipText)) UpdateEstimate(); // address / ZIP: a home needs a valid one
    }

    private static bool In(double v, string min, string max) =>
        (!double.TryParse(min.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) || v >= lo) &&
        (!double.TryParse(max.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var hi) || v <= hi);

    // passes the filter boxes (a hand- or map-excluded comp still does; it is dropped separately)
    private bool Passes(Comp c) => (IsHome || c.Acres > 0)
        && In((double)c.Price, MinPrice, MaxPrice) && (c.Acres <= 0 ? MinAcres + MaxAcres + MinPpa + MaxPpa == "" : In(c.Acres, MinAcres, MaxAcres) && In((double)c.PricePerAcre, MinPpa, MaxPpa));

    private bool _mapReady; // the map page has loaded and understands showOnly / setPick

    /// <summary>Makes the open map match the filters (hides filtered-out pins) and the exclusions (red pins).</summary>
    private void SyncMap()
    {
        if (!IsMapVisible || !_mapReady || RunMapScript is null) return;
        if (!IsMapLoading) // "13 of 24 pins": pins that pass the filters and are switched on, of all pins on the map
        {
            static bool Placed(Comp c) => (c.Lat ?? c.FoundLat) is not null && (c.Lon ?? c.FoundLon) is not null;
            MapStatus = $"{_all.Count(c => Placed(c) && Passes(c) && !Excluded(c))} of {_all.Count(Placed)} pins";
        }
        _ = RunMapScript($"showOnly({JsonSerializer.Serialize(_all.Where(Passes).Select(Key))});"
            + $"orderList({JsonSerializer.Serialize(Visible.Select(Key))});" // the map's list follows the sort box
            + $"setTop({JsonSerializer.Serialize(UseTopFive && HasSubject ? Visible.Where(c => !c.IsExcluded).Select(Key) : [])});" // Top 5 pins turn blue
            + string.Concat(_all.Select(c => $"setPick({JsonSerializer.Serialize(Key(c))},{(Excluded(c) || c.OutsideTop ? "true" : "false")});")));
    }

    private void Refresh()
    {
        // listed: passes the filters and not removed by hand; outliers stay listed (dimmed) so the reason shows, but do not count
        var listed = _all.Where(c => !_removed.Contains(Key(c)) && Passes(c)).ToList();
        CompScoring.Score(listed, PinAcres, IsHome, DateTime.Today, SubjectSqft, SubjectBeds, SubjectBaths, IsMobile, IsMultiFamily, IsCondo);
        foreach (var c in _all) c.OutsideTop = false;
        if (UseTopFive && HasSubject)
        {
            var counted = listed.Where(c => !Excluded(c)).ToList();
            var slots = Math.Max(0, TopCount - counted.Count(c => _kept.Contains(Key(c))));
            foreach (var c in counted.Where(c => !_kept.Contains(Key(c))).OrderByDescending(c => c.Weight).Skip(slots)) c.OutsideTop = true;
        }
        foreach (var c in listed) c.IsExcluded = Excluded(c) || c.OutsideTop;
        var shown = listed.Where(c => !c.IsExcluded).ToList();
        RemovedCount = _all.Count(Excluded);
        Visible.Clear();
        IEnumerable<Comp> ordered = Sort switch
        {
            "Best match" => HasSubject ? listed.OrderBy(c => c.IsExcluded).ThenByDescending(c => c.Weight) : listed, // no property: the site's order
            "Featured" => listed, // the order the site listed them
            "Distance: Nearest first" => listed.OrderBy(c => c.Miles ?? double.MaxValue), // comps that could not be placed go last
            "Price: Low to High" => listed.OrderBy(c => c.Price),
            "Price: High to Low" => listed.OrderByDescending(c => c.Price),
            "Acres: Small to Large" => listed.OrderBy(c => c.Acres),
            "Acres: Large to Small" => listed.OrderByDescending(c => c.Acres),
            "Newest Sold (Date)" => listed.OrderByDescending(c => c.SoldDate),
            "Address: A-Z" => listed.OrderBy(c => c.Address),
            _ => listed
        };
        foreach (var c in ordered) Visible.Add(c);
        SyncMap();

        Count = shown.Count;
        TotalCount = _all.Count;
        // Size and rate columns: lot acres and $/acre for land, the house's sqft and $/sqft for homes.
        // Medians are weighted (close, recent, similar comps pull harder); averages stay plain.
        var sized = shown.Where(c => IsHome ? c.HomeSqft > 0 : c.Acres > 0).ToList();
        var rates = sized.Select(c => (IsHome ? c.PricePerSqft : c.PricePerAcre, c.Weight)).ToList();
        var prices = shown.Select(c => (c.Price, c.Weight)).ToList();
        var sizes = sized.Select(c => IsHome ? c.HomeSqft!.Value : c.Acres).ToList();
        AvgPpa = rates.Count > 0 ? Math.Round(rates.Average(x => x.Item1)) : 0;
        MedianPpa = CompScoring.WeightedPercentile(rates, 0.5);
        var sizeFormat = IsHome ? "N0" : "0.00";
        AvgSizeText = sizes.Count > 0 ? sizes.Average().ToString(sizeFormat) : "--";
        MedianSizeText = sizes.Count > 0 ? ((double)CompsResult.Median(sizes.Select(s => (decimal)s))).ToString(sizeFormat) : "--";
        AvgPrice = shown.Count > 0 ? Math.Round(shown.Average(c => c.Price)) : 0;
        MedianPrice = CompScoring.WeightedPercentile(prices, 0.5);
        // the middle half of the weighted comps, as a value range: a rate for land and sized homes, a price for homes without a size
        var byRate = !IsHome || SubjectSqft > 0;
        _range = byRate ? (CompScoring.WeightedPercentile(rates, 0.25), CompScoring.WeightedPercentile(rates, 0.75))
            : (CompScoring.WeightedPercentile(prices, 0.25), CompScoring.WeightedPercentile(prices, 0.75));
        (ConfidenceLevel, ConfidenceText) = Count > 0 ? CompScoring.Confidence(shown, DateTime.Today) : ("", "");
        PpaText = (UseMedian ? MedianPpa : AvgPpa).ToString("0", CultureInfo.InvariantCulture); // also refreshes the estimate
        UpdateEstimate();
    }

    private (decimal Low, decimal High) _range;
    [ObservableProperty] private string _rangeText = "";
    [ObservableProperty] private string _confidenceLevel = "", _confidenceText = "";
    // what a rate is multiplied by: the lot's acres for land, the house's sqft for homes (1 = a home valued by price)
    private decimal RateUnits => !IsHome ? (decimal)PinAcres : SubjectSqft > 0 ? SubjectSqft.Value : 1;
    private (decimal Low, decimal High) RangeValue => (Math.Round(_range.Low * RateUnits), Math.Round(_range.High * RateUnits));

    // the property being valued, for homes: floor area, beds, baths (blank = not compared) and mobile / manufactured
    [ObservableProperty] private string _homeSqftText = "", _bedsText = "", _bathsText = "";
    [ObservableProperty] private bool _isMobile, _isMultiFamily, _isCondo;
    // one kind at a time: mobile, duplex / multi-family, or neither (a single-family house)
    partial void OnIsMobileChanged(bool value) { if (value) IsMultiFamily = IsCondo = false; }
    partial void OnIsMultiFamilyChanged(bool value) { if (value) IsMobile = IsCondo = false; }
    partial void OnIsCondoChanged(bool value) { if (value) IsMobile = IsMultiFamily = false; }
    private int? SubjectSqft => int.TryParse(HomeSqftText.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    private int? SubjectBeds => int.TryParse(BedsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
    private double? SubjectBaths => double.TryParse(BathsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;

    /// <summary>A street number and name plus a ZIP (in the address or the Zip box): a home is only valued with one.</summary>
    private bool HasValidAddress => System.Text.RegularExpressions.Regex.IsMatch(Address ?? "", @"^\s*\d+[A-Za-z]?\s+\S") && Place().Zip is not null;

    // labels that follow the House box: the stats columns and the rate box under Market value
    public string SizeHead => IsHome ? "Sqft" : "Acres";
    public string RateHead => IsHome ? "$/Sqft" : "$/Acre";
    public string RateLabel => IsHome ? "$ / sqft" : "$ / acre";
    [ObservableProperty] private string _avgSizeText = "--", _medianSizeText = "--";
    [ObservableProperty] private string _valueHow = "";
    partial void OnIsHomeChanged(bool value)
    {
        OnPropertyChanged(nameof(SizeHead)); OnPropertyChanged(nameof(RateHead)); OnPropertyChanged(nameof(RateLabel));
    }

    private void UpdateEstimate()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        decimal.TryParse(PpaText.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate);
        if (!IsHome)
        {
            Estimate = Math.Round(rate * (decimal)PinAcres);
            ValueHow = "Land: acres × the weighted median $/acre. Close, recent, similar-size lots count more; outliers are left out.";
        }
        else if (!HasValidAddress) // a home is only valued for a real address; the comps can still be read and browsed
        {
            Estimate = 0;
            ValueHow = "Home: enter the property's full address (street number, street, ZIP) to value it.";
        }
        else if (SubjectSqft is { } sqft)
        {
            Estimate = Math.Round(rate * sqft);
            ValueHow = $"Home: the weighted median $/sqft × {sqft:N0} sqft. Homes close in size, beds and baths count more; outliers are left out.";
        }
        else
        {
            Estimate = MedianPrice;
            ValueHow = "Home: the weighted median price. Enter the house's sqft (and beds / baths) from the appraiser for a size-based value.";
        }
        if (UseTopFive && Estimate > 0) ValueHow += $" Only the {TopCount} best matches count (Top 5).";
        EstimateText = Estimate > 0 ? Estimate.ToString("C0", us) : "--";
        var (lo, hi) = RangeValue;
        RangeText = Estimate > 0 && Count >= 2 && lo > 0 && hi > lo ? $"Range {lo.ToString("C0", us)} – {hi.ToString("C0", us)}" : "";
        UseCompsCommand.NotifyCanExecuteChanged();
    }

    private bool CanUse() => HasTarget && Count > 0 && Estimate > 0;
    partial void OnHasTargetChanged(bool value) => UseCompsCommand.NotifyCanExecuteChanged();

    private void LeaveToTarget()
    {
        var main = _serviceProvider.GetRequiredService<MainViewModel>();
        if (_returnTo is not null) main.CurrentViewModel = _returnTo;
        NewProperty();
    }

    /// <summary>Sends the value back to the pin or grid row that opened this tab, then returns to it.</summary>
    [RelayCommand(CanExecute = nameof(CanUse))]
    private void UseComps()
    {
        decimal.TryParse(PpaText.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var ppa);
        var (lo, hi) = RangeValue;
        _onReturn?.Invoke(new CompsResult
        {
            Kind = IsHome ? "Home" : "Land",
            Site = Site,
            Count = Count,
            PricePerAcre = IsHome ? 0 : ppa, // the rate box holds $/sqft for homes
            MedianPrice = MedianPrice,
            EstimatedValue = Estimate,
            RangeLow = RangeText.Length > 0 ? lo : null,
            RangeHigh = RangeText.Length > 0 ? hi : null,
            Confidence = ConfidenceLevel,
            ConfidenceText = ConfidenceText,
            Comps = [.. Visible.Where(c => !c.IsExcluded)]
        });
        LeaveToTarget();
    }

    /// <summary>The subject plus the listed comps as map pins; comps that could not be placed are counted in <c>skipped</c> and left out.</summary>
    private async Task<(List<MarkerLocation> Pins, int Skipped)> BuildPinsAsync(IEnumerable<Comp>? source = null)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var pins = new List<MarkerLocation>();
        // the subject is placed by Analyze's distance step; if that has not run, place it now
        _subject ??= !HasSubject ? null : Coords is { } placed ? placed : Address.Length > 0 ? await _serviceProvider.GetRequiredService<IGeocoder>().GeocodeAsync(Address) : null;
        if (_subject is { } s)
            pins.Add(_subjectPin is { } home
                // same popup as on the Home map (facts, house line, risk flags, slope, values), as a green subject pin
                ? home with { Latitude = s.Lat, Longitude = s.Lon, PinColor = "#2E9E5B", IsSubject = true }
                : new MarkerLocation { Address = Address.Length > 0 ? Address : "Subject property", Latitude = s.Lat, Longitude = s.Lon, Acres = AcresText, PinColor = "#2E9E5B", IsSubject = true });
        var skipped = 0;
        foreach (var c in source ?? Visible)
        {
            if ((c.Lat ?? c.FoundLat) is not { } lat || (c.Lon ?? c.FoundLon) is not { } lon) { skipped++; continue; }
            pins.Add(new MarkerLocation
            {
                CompFacts = new (string, string?)[]
                {
                    (c.Sold ? "Sold" : "Status", c.Sold ? (c.SoldText is { Length: > 0 } sd ? sd : "Sold") : "Active"),
                    ("Price", c.Price.ToString("C0", us)),
                    ("Lot", c.Acres > 0 ? $"{c.LotText} ({c.LotDetail})" : null),
                    ("Price per acre", c.Acres > 0 ? c.PricePerAcre.ToString("C0", us) : null),
                    ("Home size", c.HomeSqft is > 0 ? $"{c.HomeSqft:N0} sqft" + (c.PricePerSqft > 0 ? $" ({c.PricePerSqft.ToString("C0", us)}/sqft)" : "") : null),
                    ("Beds / baths", c.Beds is > 0 || c.Baths is > 0 ? $"{c.Beds?.ToString() ?? "?"} bd / {c.Baths?.ToString("0.#") ?? "?"} ba" : null),
                    ("Distance", c.Miles is { } m ? $"{m:0.0} mi from subject" : null),
                    ("Weight", $"{c.WeightDots} {c.Weight:P0}"), // the map list reads this and Distance
                    ("Outlier", c.Outlier)
                }.Where(x => x.Item2 is not null).ToDictionary(x => x.Item1, x => x.Item2!),
                CompTips = new Dictionary<string, string> { ["Weight"] = c.WeightTip }
                    .Concat(c.OutlierTip is { } ot ? [new("Outlier", ot + (c.IsExcluded ? " Left out of the value; switch Included on to count it anyway." : ""))] : Array.Empty<KeyValuePair<string, string>>())
                    .ToDictionary(),
                Address = c.Address ?? "N/A",
                Latitude = lat,
                Longitude = lon,
                Acres = c.Acres > 0 ? c.Acres.ToString("0.####", CultureInfo.InvariantCulture) : null,
                PinColor = "#CD7B2E",
                CompKey = Key(c),
                GoogleMapsUrl = c.Url // the address links to the listing
            });
        }
        return (pins, skipped);
    }

    /// <summary>Writes the comps now listed (and the property itself) as pins on a standalone HTML map.</summary>
    [RelayCommand]
    private async Task ExportHtml()
    {
        // "113 LAKE EDGE TRL, City, ST 12345" -> "113-lake-edge-trl.html" (street only)
        var street = System.Text.RegularExpressions.Regex.Replace(Address.Split(',')[0].Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "HTML File (*.html)|*.html", FileName = (street.Length > 0 ? street : "comps") + ".html" };
        if (dialog.ShowDialog() != true) return;

        var (pins, skipped) = await BuildPinsAsync();
        if (pins.Count == 0) { Message = "No comps with a location to export yet. Wait for the distances, or Analyze first."; return; }

        try
        {
            await System.IO.File.WriteAllTextAsync(dialog.FileName, await Services.PinsHtml.BuildAsync(pins, true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
            Message = $"Exported {pins.Count} pins" + (skipped > 0 ? $" ({skipped} comps had no location)." : ".");
        }
        catch (Exception ex) { Message = $"Could not export: {ex.Message}"; }
    }

    // The map replaces the browser in this view (shown/hidden by the view); picks on it update the list live.
    [ObservableProperty] private bool _isMapVisible;
    public Func<string, Task>? LoadMap { get; set; }        // set by CompsView: puts the pins page in the map WebView
    public Func<string, Task>? RunMapScript { get; set; }   // set by CompsView: calls into the map page

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRemoved))] private int _removedCount;
    public bool HasRemoved => RemovedCount > 0;

    /// <summary>Swaps the browser for a map of the comps where each one can be kept or excluded.</summary>
    [ObservableProperty] private bool _isMapLoading;
    [ObservableProperty] private string _mapStatus = "";
    private int _mapRun; // a newer open or leaving the map stops an older load

    [RelayCommand]
    private async Task ReviewOnMapAsync()
    {
        if (LoadMap is null || RunMapScript is null || IsMapVisible) return;
        var run = ++_mapRun;
        _mapReady = false;
        bool Stale() => run != _mapRun || !IsMapVisible;
        // swap at once; the rest happens with the map on screen
        MapStatus = "Loading map…";
        IsMapLoading = true;
        IsMapVisible = true;
        try
        {
            _ = RunMapScript("document.body.style.visibility='hidden'"); // hide the previous map until the new page loads
            // every comp, so loosening a filter can bring pins back; nearest first, so the map's list reads outward from the subject
            // shortcut: the list's weight is a snapshot taken when the map opens; reopen the map after distances finish to refresh it
            var (pins, _) = await BuildPinsAsync(_all.OrderBy(c => c.Miles ?? double.MaxValue));
            if (Stale()) return;
            if (pins.Count == 0) { IsMapVisible = false; Message = "No comps with a location to show yet. Wait for the distances, or Analyze first."; return; }
            await LoadMap(await Services.PinsHtml.BuildAsync([], true, picker: true));
            _mapReady = true;
            // pins go in a few at a time so they pop onto the map
            for (var i = 0; i < pins.Count && !Stale(); i += 8)
            {
                var batch = pins.Skip(i).Take(8).ToList();
                MapStatus = $"Adding pins {Math.Min(i + 8, pins.Count)} / {pins.Count}";
                await RunMapScript($"addPins({Services.PinsHtml.ToJson(batch)})");
                SyncMap(); // keeps the batch inside the current filters
                await Task.Delay(40);
            }
        }
        catch (Exception ex) { IsMapVisible = false; Message = "Could not show the map: " + ex.Message; }
        finally { if (run == _mapRun) { IsMapLoading = false; SyncMap(); } }
    }

    [RelayCommand]
    private void BackToBrowser() => IsMapVisible = false;

    /// <summary>A pin's Included switch on the map was flipped.</summary>
    public void OnMapPick(string key, bool excluded)
    {
        if (excluded) { _removed.Add(key); _kept.Remove(key); }
        else { _removed.Remove(key); _kept.Add(key); } // switching an outlier on counts it anyway
        Refresh();
    }

    /// <summary>Brings back every comp removed by hand or from the map.</summary>
    [RelayCommand]
    private void ShowRemoved()
    {
        _removed.Clear();
        foreach (var c in _all.Where(c => c.Outlier is not null)) _kept.Add(Key(c)); // outliers count again too
        Refresh();
        if (IsMapVisible) _ = RunMapScript?.Invoke("clearPicks()");
    }

    /// <summary>Returns without sending anything.</summary>
    [RelayCommand]
    private void BackWithoutSaving() => LeaveToTarget();
}
