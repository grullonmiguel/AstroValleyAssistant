using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
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
public record CompsSubject(string? Address, double? Acres, string? PropertyType, double? Latitude, double? Longitude);

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

    public static readonly (string Name, string Url)[] Sites =
    [
        ("Homes.com", "https://www.homes.com/"),
        ("Zillow", "https://www.zillow.com/homes/recently_sold/"),
        ("Realtor", "https://www.realtor.com/"),
        ("Redfin", "https://www.redfin.com/")
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
    [RelayCommand]
    private void OpenListing(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https")
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = u.AbsoluteUri, UseShellExecute = true });
    }

    // the property being valued; editable so any random property can be typed in
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _acresText = "";
    [ObservableProperty] private bool _isHome;

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
        _onReturn = onReturn;
        TargetLabel = label;
        HasTarget = true;
        (_lat, _lon, _type) = (subject.Latitude, subject.Longitude, subject.PropertyType);
        Address = subject.Address ?? "";
        AcresText = subject.Acres is > 0 ? subject.Acres.Value.ToString("0.####", CultureInfo.InvariantCulture) : "";
        IsHome = subject.PropertyType == "Structures";
        _all.Clear();
        _removed.Clear();
        Message = "Search and filter on the site, then press Analyze.";
        Refresh();
        main.CurrentViewModel = this;
        if (_lat is not null) GoSite("Zillow"); // Zillow can be centred on the property; other sites start where the browser was
    }

    // Zillow recently-sold search: around the coordinates when known, else by the address; same property type, 90 days, lot 50-150%.
    // shortcut: Zillow's filter keys are undocumented (written from memory), so a filter may be ignored; adjust it on the page.
    private string ZillowUrl()
    {
        object Yes = new { value = true }, No = new { value = false };
        var f = new Dictionary<string, object> { ["rs"] = Yes, ["fsba"] = No, ["fsbo"] = No, ["nc"] = No, ["cmsn"] = No, ["auc"] = No, ["fore"] = No, ["pmf"] = No, ["pf"] = No, ["doz"] = new { value = "90" } };
        if (IsHome) { f["land"] = No; f["sf"] = Yes; f["tow"] = Yes; f["manu"] = Yes; f["mf"] = No; f["con"] = No; f["apa"] = No; f["apco"] = No; }
        else if (_type == "Vacant land") { f["land"] = Yes; foreach (var k in new[] { "sf", "tow", "mf", "con", "apa", "manu", "apco" }) f[k] = No; }
        if (!IsHome && PinAcres > 0) f["lot"] = new { min = (int)Math.Round(PinAcres * 0.5 * 43560), max = (int)Math.Round(PinAcres * 1.5 * 43560) };
        object state;
        if (_lat is { } lat && _lon is { } lon)
        {
            double d = 0.03, dx = d / Math.Cos(lat * Math.PI / 180);
            state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = "", mapBounds = new { west = lon - dx, east = lon + dx, south = lat - d, north = lat + d }, filterState = f, isListVisible = true };
        }
        else state = new { pagination = new { }, isMapVisible = true, usersSearchTerm = Address, filterState = f, isListVisible = true };
        return "https://www.zillow.com/homes/recently_sold/?searchQueryState=" + Uri.EscapeDataString(JsonSerializer.Serialize(state));
    }

    [RelayCommand]
    private void GoSite(string name)
    {
        Site = name;
        PendingUrl = name == "Zillow" && (_lat is not null || Address.Length > 0) ? ZillowUrl() : Sites.First(s => s.Name == name).Url;
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
                if (r.price <= 0 || r.acres <= 0) continue; // needs both to be a usable comp
                var comp = new Comp(r.address, (decimal)r.price, r.acres, r.sold, r.url, r.soldText);
                if (_all.Any(c => Key(c) == Key(comp))) continue;
                _all.Add(comp);
                added++;
            }
            Message = rows.Count == 0 ? "No listings found on this page. Open a results page and try again."
                : $"Read {rows.Count} listings, {added} new with price and lot size. Total {_all.Count}.";
        }
        catch (Exception ex) { Message = "Could not read the page: " + ex.Message; }
        Refresh();
    }

    private record RawComp(string? address, double price, double acres, bool sold, string? url, string? soldText);
    private static string Key(Comp c) => (c.Address ?? c.Url ?? "") + "|" + (c.Sold ? "S" : "A");

    [RelayCommand]
    private void Clear()
    {
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
        Address = AcresText = "";
        IsHome = false;
        Clear();
    }

    private static readonly HashSet<string> Triggers = [nameof(IsHome), nameof(UseMedian),
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
        var shown = _all.Where(c => !_removed.Contains(Key(c))
            && In((double)c.Price, MinPrice, MaxPrice) && In(c.Acres, MinAcres, MaxAcres) && In((double)c.PricePerAcre, MinPpa, MaxPpa)).ToList();
        Visible.Clear();
        foreach (var c in shown) Visible.Add(c); // page order, like the extension's Featured sort

        Count = shown.Count;
        var ppas = shown.Select(c => c.PricePerAcre).ToList();
        AvgPpa = ppas.Count > 0 ? Math.Round(ppas.Average()) : 0;
        MedianPpa = CompsResult.Median(ppas);
        AvgAcres = shown.Count > 0 ? shown.Average(c => c.Acres) : 0;
        MedianAcres = shown.Count > 0 ? (double)CompsResult.Median(shown.Select(c => (decimal)c.Acres)) : 0;
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

    /// <summary>Returns without sending anything.</summary>
    [RelayCommand]
    private void BackWithoutSaving() => LeaveToTarget();
}
