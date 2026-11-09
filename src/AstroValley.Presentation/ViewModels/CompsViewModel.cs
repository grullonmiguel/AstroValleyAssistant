using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using AstroValley.Application.Interfaces.Data;
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
public record CompsSubject(string? Address, double? Acres, string? PropertyType, double? Latitude, double? Longitude, string? Zip = null);

/// <summary>
/// The Comps tab: a browser (homes.com, Zillow, ...) plus a sidebar that reads the listings on screen, filters them and
/// gives a market value. Opened by hand it just shows the outcome; opened from a pin or a grid row it can send the value back.
/// </summary>
public partial class CompsViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Comp> _all = [];
    private readonly HashSet<string> _removed = [];
    private Action<CompsResult>? _onReturn;
    private ObservableObject? _returnTo;
    private double? _lat, _lon;
    private string? _type;
    private (double Lat, double Lon)? _subject; // where the property was placed; the export's first pin

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
    public Action? GoBackRequested { get; set; }
    public string? PendingUrl { get; private set; }
    public string StartUrl => PendingUrl ?? Sites[0].Url;

    public ObservableCollection<Comp> Visible { get; } = [];

    /// <summary>The site buttons; the one being visited is highlighted.</summary>
    public ObservableCollection<SiteOption> SiteOptions { get; } = [.. Sites.Select(s => new SiteOption(s.Name) { IsSelected = s.Name == "Homes.com" })];
    partial void OnSiteChanged(string value) { foreach (var o in SiteOptions) o.IsSelected = o.Name == value; }

    /// <summary>A comp's address opens the listing in the default browser, not in the embedded one.</summary>
    public void OpenListing(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = u.AbsoluteUri, UseShellExecute = true });
        else Message = "This listing has no link to open."; // shows when the site gave no URL for the card
    }

    // the property being valued; editable so any random property can be typed in
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _acresText = "";
    [ObservableProperty] private string _zipText = "";
    [ObservableProperty] private bool _isHome;

    public string[] SortOptions { get; } = ["Featured", "Distance: Nearest first", "Price: Low to High", "Price: High to Low", "Acres: Small to Large", "Acres: Large to Small", "Newest Sold (Date)", "Address: A-Z"];
    [ObservableProperty] private string _sort = "Featured";

    // set when opened from a pin or grid row: the value can be sent back to it
    [ObservableProperty] private bool _hasTarget;
    [ObservableProperty] private string _targetLabel = "";

    [ObservableProperty] private bool _useMedian = true;
    [ObservableProperty] private string _minPrice = "", _maxPrice = "", _minAcres = "", _maxAcres = "", _minPpa = "", _maxPpa = "";
    [ObservableProperty] private string _site = "Homes.com";
    [ObservableProperty] private string _message = "Search and filter on the site, then press Analyze.";
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty] private int _count;
    [ObservableProperty] private decimal _avgPpa, _medianPpa, _avgPrice, _medianPrice, _estimate;
    [ObservableProperty] private double _avgAcres, _medianAcres;
    [ObservableProperty] private string _ppaText = ""; // the $ / acre used; editable
    [ObservableProperty] private string _estimateText = "--";

    public CompsViewModel(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    private double PinAcres => double.TryParse(AcresText, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;

    /// <summary>Opens the Comps tab for a property; <paramref name="onReturn"/> receives the value when the user sends it back.</summary>
    public void Open(CompsSubject subject, string label, Action<CompsResult> onReturn)
    {
        var main = _serviceProvider.GetRequiredService<MainViewModel>();
        _returnTo = main.CurrentViewModel is CompsViewModel ? _returnTo : main.CurrentViewModel;
        _distanceCts?.Cancel();
        _onReturn = onReturn;
        TargetLabel = label;
        HasTarget = true;
        (_lat, _lon, _type) = (subject.Latitude, subject.Longitude, subject.PropertyType);
        Address = subject.Address ?? "";
        ZipText = subject.Zip ?? "";
        if (ZipText.Length == 0) ZipText = Place().Zip ?? "";                // the address may end in one
        if (ZipText.Length == 0 && _lat is not null) _ = FillZipAsync();     // else look it up from the coordinates
        AcresText = subject.Acres is > 0 ? subject.Acres.Value.ToString("0.####", CultureInfo.InvariantCulture) : "";
        IsHome = subject.PropertyType == "Structures";
        _all.Clear();
        _removed.Clear();
        _subject = null;
        Message = "Search and filter on the site, then press Analyze.";
        Refresh();
        main.CurrentViewModel = this;
        if (_lat is not null) GoSite("Zillow"); // Zillow can be centred on the property; other sites start where the browser was
    }

    // Zillow recently-sold search: around the coordinates when known, else by the address; same property type, 90 days. No lot-size range: the user sets that on the page.
    // shortcut: Zillow's filter keys are undocumented (written from memory), so a filter may be ignored; adjust it on the page.
    private string ZillowUrl()
    {
        object Yes = new { value = true }, No = new { value = false };
        var f = new Dictionary<string, object> { ["rs"] = Yes, ["fsba"] = No, ["fsbo"] = No, ["nc"] = No, ["cmsn"] = No, ["auc"] = No, ["fore"] = No, ["pmf"] = No, ["pf"] = No, ["doz"] = new { value = "90" } };
        if (IsHome) { f["land"] = No; f["sf"] = Yes; f["tow"] = Yes; f["manu"] = Yes; f["mf"] = No; f["con"] = No; f["apa"] = No; f["apco"] = No; }
        else { f["land"] = Yes; foreach (var k in new[] { "sf", "tow", "mf", "con", "apa", "manu", "apco" }) f[k] = No; }
        object state;
        if (_lat is { } lat && _lon is { } lon)
        {
            double d = 0.03, dx = d / Math.Cos(lat * Math.PI / 180);
            state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = "", mapBounds = new { west = lon - dx, east = lon + dx, south = lat - d, north = lat + d }, filterState = f, isListVisible = true };
        }
        else state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = Address.Length > 0 ? Address : Place().Zip ?? "", filterState = f, isListVisible = true };
        return "https://www.zillow.com/homes/recently_sold/?searchQueryState=" + Uri.EscapeDataString(JsonSerializer.Serialize(state));
    }

    // City, state and ZIP from "street, City, ST 12345" (or the ZIP box); the sold-listing URLs below are built from them, like the extension does.
    private (string? City, string? State, string? Zip) Place()
    {
        var m = System.Text.RegularExpressions.Regex.Match(Address ?? "", @",\s*([^,]+?),\s*([A-Za-z]{2})\s+(\d{5})");
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
            case "Zillow": return _lat is not null || Address.Length > 0 || zip is not null ? ZillowUrl() : home;
            case "Homes.com" when city is not null && state is not null:
                return $"https://www.homes.com/{city.ToLowerInvariant().Replace(' ', '-')}-{state.ToLowerInvariant()}/sold/?{(land ? "property_type=32&" : "")}sd=4";
            case "Realtor" when zip is not null: return $"https://www.realtor.com/realestateandhomes-search/{zip}{(land ? "/type-land" : "")}/soldwithin-6";
            case "Redfin" when zip is not null: return $"https://www.redfin.com/zipcode/{zip}/filter/{(land ? "property-type=land," : "")}include=sold-6mo";
            case "Trulia" when zip is not null: return $"https://www.trulia.com/sold/{zip}_zip/{(land ? "LOT%7CLAND_type/" : "")}6_srl/";
            default: return home;
        }
    }

    [RelayCommand]
    private void GoSite(string name)
    {
        Site = name;
        PendingUrl = SiteUrl(name);
        NavigateTo?.Invoke(PendingUrl); // before the browser is ready, the view loads PendingUrl when it starts
    }

    [RelayCommand]
    private void GoBack() => GoBackRequested?.Invoke();

    [RelayCommand]
    private async Task Analyze()
    {
        if (ReadPage is null) return;
        try
        {
            var json = await ReadPage();
            // ExecuteScriptAsync returns JSON
            var rows = JsonSerializer.Deserialize<List<RawComp>>(json) ?? [];
            var added = 0;
            foreach (var r in rows)
            {
                if (r.price <= 0) continue; // no lot size is fine: houses are valued by price, land rows without one are left out of the stats
                var comp = new Comp(r.address, (decimal)r.price, r.acres, r.sold, r.url, r.soldText, r.homeSqft, r.lat, r.lon);
                if (_all.Any(c => Key(c) == Key(comp))) continue;
                _all.Add(comp);
                added++;
            }
            Message = rows.Count == 0 ? "No listings found on this page. Open a results page and try again."
                : $"Read {rows.Count} listings: {added} added, {rows.Count(r => r.price <= 0)} skipped for no price; {rows.Count(r => r.price > 0 && r.acres <= 0)} have no lot size (used for houses only). Total {_all.Count}.";
        }
        catch (Exception ex) { Message = "Could not read the page: " + ex.Message; }
        Refresh();
        _ = MeasureDistancesAsync();
    }

    private record RawComp(string? address, double price, double acres, bool sold, string? url, string? soldText, int? homeSqft, double? lat, double? lon);
    private static string Key(Comp c) => (c.Address ?? c.Url ?? "") + "|" + (c.Sold ? "S" : "A");

    [RelayCommand]
    private void Clear()
    {
        _distanceCts?.Cancel();
        _all.Clear();
        _removed.Clear();
        Message = "Cleared.";
        Refresh();
    }

    [RelayCommand]
    private void Remove(Comp comp)
    {
        _removed.Add(Key(comp));
        Refresh();
    }

    /// <summary>Forgets the property and any pin / row it was opened from, for a fresh manual search.</summary>
    [RelayCommand]
    private void NewProperty()
    {
        _onReturn = null;
        _returnTo = null;
        (_lat, _lon, _type) = (null, null, null);
        HasTarget = false;
        TargetLabel = "";
        Address = AcresText = ZipText = "";
        IsHome = false;
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
            (double Lat, double Lon)? home = _lat is { } la && _lon is { } lo ? (la, lo)
                : Address.Length > 0 ? await geo.GeocodeAsync(Address, cts.Token) : null;
            _subject = home;
            if (home is not { } h) { Message += " Distance needs the property's coordinates or a full address."; return; }

            var todo = _all.Where(c => c.Miles is null).ToList();
            Message = $"Measuring distance for {todo.Count} comps...";
            var done = 0;
            foreach (var c in todo)
            {
                (double Lat, double Lon)? p = c.Lat is { } y && c.Lon is { } x ? (y, x)
                    : c.Address is { Length: > 0 } a ? await geo.GeocodeAsync(a, cts.Token) : null;
                if (p is { } q)
                {
                    c.Miles = Math.Round(Miles(h.Lat, h.Lon, q.Lat, q.Lon), 1);
                    c.FoundLat = q.Lat; c.FoundLon = q.Lon;
                }
                if (++done % 5 == 0) Refresh();
            }
            Refresh();
            Message = $"Distance found for {_all.Count(c => c.Miles is not null)} of {_all.Count} comps.";
        }
        catch (OperationCanceledException) { } // a newer Analyze, Clear or property took over
    }

    private static readonly HashSet<string> Triggers = [nameof(IsHome), nameof(Sort), nameof(UseMedian),
        nameof(MinPrice), nameof(MaxPrice), nameof(MinAcres), nameof(MaxAcres), nameof(MinPpa), nameof(MaxPpa)];

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } n && Triggers.Contains(n)) Refresh();
        if (e.PropertyName is nameof(PpaText) or nameof(AcresText)) UpdateEstimate();
    }

    private static bool In(double v, string min, string max) =>
        (!double.TryParse(min.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) || v >= lo) &&
        (!double.TryParse(max.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var hi) || v <= hi);

    private void Refresh()
    {
        var shown = _all.Where(c => !_removed.Contains(Key(c)) && (IsHome || c.Acres > 0)
            && In((double)c.Price, MinPrice, MaxPrice) && (c.Acres <= 0 ? MinAcres + MaxAcres + MinPpa + MaxPpa == "" : In(c.Acres, MinAcres, MaxAcres) && In((double)c.PricePerAcre, MinPpa, MaxPpa))).ToList();
        Visible.Clear();
        IEnumerable<Comp> ordered = Sort switch
        {
            "Distance: Nearest first" => shown.OrderBy(c => c.Miles ?? double.MaxValue), // comps that could not be placed go last
            "Price: Low to High" => shown.OrderBy(c => c.Price),
            "Price: High to Low" => shown.OrderByDescending(c => c.Price),
            "Acres: Small to Large" => shown.OrderBy(c => c.Acres),
            "Acres: Large to Small" => shown.OrderByDescending(c => c.Acres),
            "Newest Sold (Date)" => shown.OrderByDescending(c => c.SoldDate),
            "Address: A-Z" => shown.OrderBy(c => c.Address),
            _ => shown // Featured = the order the site listed them
        };
        foreach (var c in ordered) Visible.Add(c);

        Count = shown.Count;
        var withLot = shown.Where(c => c.Acres > 0).ToList(); // $/acre and acres stats only make sense for rows that have a lot
        var ppas = withLot.Select(c => c.PricePerAcre).ToList();
        AvgPpa = ppas.Count > 0 ? Math.Round(ppas.Average()) : 0;
        MedianPpa = CompsResult.Median(ppas);
        AvgAcres = withLot.Count > 0 ? withLot.Average(c => c.Acres) : 0;
        MedianAcres = withLot.Count > 0 ? (double)CompsResult.Median(withLot.Select(c => (decimal)c.Acres)) : 0;
        AvgPrice = shown.Count > 0 ? Math.Round(shown.Average(c => c.Price)) : 0;
        MedianPrice = CompsResult.Median(shown.Select(c => c.Price));
        PpaText = (UseMedian ? MedianPpa : AvgPpa).ToString("0", CultureInfo.InvariantCulture); // also refreshes the estimate
        UpdateEstimate();
    }

    private void UpdateEstimate()
    {
        if (IsHome) Estimate = MedianPrice;
        else Estimate = decimal.TryParse(PpaText.Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var ppa) ? Math.Round(ppa * (decimal)PinAcres) : 0;
        EstimateText = Estimate > 0 ? Estimate.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "--";
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
        _onReturn?.Invoke(new CompsResult
        {
            Kind = IsHome ? "Home" : "Land",
            Site = Site,
            Count = Count,
            PricePerAcre = ppa,
            MedianPrice = MedianPrice,
            EstimatedValue = Estimate,
            Comps = [.. Visible]
        });
        LeaveToTarget();
    }

    /// <summary>Writes the comps now listed (and the property itself) as pins on a standalone HTML map. Comps that could not be placed are left out.</summary>
    [RelayCommand]
    private async Task ExportHtml()
    {
        // "113 LAKE EDGE TRL, City, ST 12345" -> "113-lake-edge-trl.html" (street only)
        var street = System.Text.RegularExpressions.Regex.Replace(Address.Split(',')[0].Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "HTML File (*.html)|*.html", FileName = (street.Length > 0 ? street : "comps") + ".html" };
        if (dialog.ShowDialog() != true) return;

        var us = CultureInfo.GetCultureInfo("en-US");
        var pins = new List<MarkerLocation>();
        // the subject is placed by Analyze's distance step; if that has not run, place it now
        _subject ??= _lat is { } la && _lon is { } lo ? (la, lo) : Address.Length > 0 ? await _serviceProvider.GetRequiredService<IGeocoder>().GeocodeAsync(Address) : null;
        if (_subject is { } s)
            pins.Add(new MarkerLocation { Address = Address.Length > 0 ? Address : "Subject property", Latitude = s.Lat, Longitude = s.Lon, Acres = AcresText, PinColor = "#2E9E5B", IsSubject = true });
        var skipped = 0;
        foreach (var c in Visible)
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
                    ("Home size", c.HomeSqft is > 0 ? $"{c.HomeSqft:N0} sqft" : null),
                    ("Distance", c.Miles is { } m ? $"{m:0.0} mi from subject" : null)
                }.Where(x => x.Item2 is not null).ToDictionary(x => x.Item1, x => x.Item2!),
                Address = c.Address ?? "N/A",
                Latitude = lat,
                Longitude = lon,
                Acres = c.Acres > 0 ? c.Acres.ToString("0.####", CultureInfo.InvariantCulture) : null,
                PinColor = "#CD7B2E",
                GoogleMapsUrl = c.Url // the address links to the listing
            });
        }
        if (pins.Count == 0) { Message = "No comps with a location to export yet. Wait for the distances, or Analyze first."; return; }

        try
        {
            await System.IO.File.WriteAllTextAsync(dialog.FileName, await Services.PinsHtml.BuildAsync(pins, true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
            Message = $"Exported {pins.Count} pins" + (skipped > 0 ? $" ({skipped} comps had no location)." : ".");
        }
        catch (Exception ex) { Message = $"Could not export: {ex.Message}"; }
    }

    /// <summary>Returns without sending anything.</summary>
    [RelayCommand]
    private void BackWithoutSaving() => LeaveToTarget();
}
