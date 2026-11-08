using AstroValley.Application.Interfaces.Export;
using AstroValley.Domain.Enums;
using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.IO;
using System.Text.Json;

namespace AstroValley.Presentation.ViewModels;

// Id is the county FIPS code within the state (unique even when names repeat, e.g. Richmond county/city).
public record CountyItem(string Id, string Name);

public partial class HomeViewModel : ObservableObject
{
    private const TaxSaleType Lien = TaxSaleType.TaxLien, Deed = TaxSaleType.TaxDeed,
        Redeemable = TaxSaleType.RedeemableDeed, Hybrid = TaxSaleType.Hybrid;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private StateViewModel? _selectedState;

    // Counties of the selected state; filled from the map's county GeoJSON (see HomeView).
    public ObservableCollection<CountyItem> Counties { get; } = [];

    [ObservableProperty]
    private CountyItem? _selectedCounty;

    partial void OnSelectedStateChanged(StateViewModel? value)
    {
        Counties.Clear();
        SelectedCounty = null;
    }

    // Pins sent from Real Auction / Regrid. The map page is kept alive, so these live for the whole app session.
    public ObservableCollection<MarkerLocation> Markers { get; } = [];

    private readonly IPinSheetExporter _sheetExporter;

    public HomeViewModel(IPinSheetExporter sheetExporter)
    {
        _sheetExporter = sheetExporter;
        Markers.CollectionChanged += (_, _) =>
        {
            SavePinsCommand.NotifyCanExecuteChanged();
            ExportMapCommand.NotifyCanExecuteChanged();
            ExportSheetCommand.NotifyCanExecuteChanged();
            PlanRouteCommand.NotifyCanExecuteChanged();
        };
        // The parcel list binds to Markers, i.e. this default view, so filtering it filters the list too.
        CollectionViewSource.GetDefaultView(Markers).Filter = o => Matches((MarkerLocation)o);
    }

    // Chips under the map are independent toggles; the acre slider is one more filter. "All" clears them.
    [ObservableProperty]
    private bool _onlyUnder5k;

    [ObservableProperty]
    private bool _onlyEquity;

    [ObservableProperty]
    private bool _hideSkipped;

    // Slider positions for "max acres"; the last position means no limit.
    private static readonly double[] AcreSteps = [0.25, 0.5, 1, 2, 5, 10, 25, 50, 100];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AcreLabel))]
    private int _acreStep = AcreSteps.Length;

    public string AcreLabel => AcreStep >= AcreSteps.Length ? "Acres: any" : $"Acres: ≤ {AcreSteps[AcreStep]}";

    [ObservableProperty]
    private bool _isTerrain;

    [ObservableProperty]
    private bool _isTouring;

    [ObservableProperty]
    private bool _isFlood;

    [ObservableProperty]
    private bool _isWetlands;

    [ObservableProperty]
    private bool _isEpa;

    /// <summary>Asks the map to switch an overlay ("flood" / "wetlands" / "epa") on or off.</summary>
    public event Action<string>? OverlayToggleRequested;

    /// <summary>Raised when the filter changed; the map then hides <see cref="HiddenMarkers"/>.</summary>
    public event Action? FilterChanged;
    public event Action? TerrainToggleRequested;
    public event Action? TourToggleRequested;

    public IEnumerable<MarkerLocation> HiddenMarkers => Markers.Where(m => !Matches(m));

    partial void OnOnlyUnder5kChanged(bool value) => ApplyFilter();

    partial void OnOnlyEquityChanged(bool value) => ApplyFilter();

    partial void OnHideSkippedChanged(bool value) => ApplyFilter();

    partial void OnAcreStepChanged(int value) => ApplyFilter();

    private void ApplyFilter()
    {
        CollectionViewSource.GetDefaultView(Markers).Refresh();
        FilterChanged?.Invoke();
    }

    private bool Matches(MarkerLocation m)
    {
        // pins missing the number a filter needs (no bid, no assessed value, no acreage) are hidden by that filter
        if (OnlyUnder5k && !(m.BidAmount is { } bid && bid < 5000)) return false;
        if (OnlyEquity && !(m.BidToAssessedPercent is { } percent && percent <= 25)) return false;
        if (HideSkipped && m.Status == "Skip") return false;
        return AcreStep >= AcreSteps.Length
            || (double.TryParse(m.Acres, out var acres) && acres <= AcreSteps[AcreStep]);
    }

    [RelayCommand]
    private void ToggleFilter(string name)
    {
        switch (name)
        {
            case "Under5k": OnlyUnder5k = !OnlyUnder5k; break;
            case "Equity": OnlyEquity = !OnlyEquity; break;
            case "Skipped": HideSkipped = !HideSkipped; break;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        OnlyUnder5k = OnlyEquity = HideSkipped = false;
        AcreStep = AcreSteps.Length;
    }

    /// <summary>Status, notes or access check changed in a map popup.</summary>
    public void UpdatePin(double lat, double lon, string? status, string? notes,
        string? accessStatus, double? accessFt, string? accessRoad, string? accessChecked)
    {
        var pin = Markers.FirstOrDefault(m => Math.Abs(m.Latitude - lat) < 1e-9 && Math.Abs(m.Longitude - lon) < 1e-9);
        if (pin is null) return;
        pin.Status = status;
        pin.Notes = notes;
        pin.AccessStatus = accessStatus;
        pin.AccessDistanceFt = accessFt;
        pin.AccessRoad = accessRoad;
        pin.AccessChecked = accessChecked;
        if (HideSkipped) ApplyFilter(); // a pin just marked Skip drops out
    }

    // Opens one Google Maps driving route through the pins that are showing (filters applied, skipped ones left out).
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private void PlanRoute()
    {
        const int MaxStops = 10; // Google Maps links take a destination plus 9 waypoints
        var pins = Markers.Where(m => Matches(m) && m.Status != "Skip").ToList();
        if (pins.Count == 0)
        {
            System.Windows.MessageBox.Show("No pins to route: all are hidden by a filter or marked Skip.", "Plan route");
            return;
        }

        // shortcut: greedy nearest-neighbour from the first pin, not an optimal tour; fine for a day of drive-bys
        var route = new List<MarkerLocation> { pins[0] };
        pins.RemoveAt(0);
        while (pins.Count > 0)
        {
            var last = route[^1];
            var next = pins.MinBy(p => Math.Pow((p.Longitude - last.Longitude) * Math.Cos(last.Latitude * Math.PI / 180), 2) + Math.Pow(p.Latitude - last.Latitude, 2))!;
            route.Add(next);
            pins.Remove(next);
        }

        var total = route.Count;
        route = route.Take(MaxStops).ToList();
        var points = route.Select(m => FormattableString.Invariant($"{m.Latitude},{m.Longitude}")).ToList();
        var url = "https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=" + points[^1];
        if (points.Count > 1) url += "&waypoints=" + string.Join("%7C", points.Take(points.Count - 1));

        if (total > MaxStops)
            System.Windows.MessageBox.Show($"Google Maps takes up to {MaxStops} stops, so the route covers {MaxStops} of {total} pins. " +
                "Use the filters or mark pins as Skip to choose which ones.", "Plan route");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    [RelayCommand]
    private void ToggleTerrain() => TerrainToggleRequested?.Invoke();

    [RelayCommand]
    private void ToggleOverlay(string key) => OverlayToggleRequested?.Invoke(key);

    [RelayCommand]
    private void ToggleTour() => TourToggleRequested?.Invoke();
    private readonly HashSet<string> _markerKeys = [];

    /// <summary>Raised with only the pins that were new, so the map never draws a pin twice.</summary>
    public event Action<IReadOnlyList<MarkerLocation>>? MarkersAdded;

    public event Action<MarkerLocation>? MarkerRemoved;
    public event Action? MarkersCleared;

    // Parcel IDs can repeat across counties, so the key also includes the (rounded) position.
    private static string KeyOf(MarkerLocation l) => $"{l.ParcelID}|{l.Latitude:F4}|{l.Longitude:F4}";

    [RelayCommand]
    private void RemoveMarker(MarkerLocation marker)
    {
        if (!Markers.Remove(marker)) return;
        _markerKeys.Remove(KeyOf(marker)); // so the same pin can be sent again later
        MarkerRemoved?.Invoke(marker);
    }

    // Writes the current pins to a standalone HTML map (list + fly-to + popups, no delete). Needs internet to show tiles.
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private async Task ExportMap()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "HTML File (*.html)|*.html", FileName = "AstroValley-Pins.html" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            // Default JSON encoding escapes '<' etc., so the data can't break out of the <script> tag.
            var pinsJson = JsonSerializer.Serialize(Markers, new JsonSerializerOptions { PropertyNamingPolicy = null });

            var html = (await File.ReadAllTextAsync(Path.Combine(assets, "pinsexport.html")))
                .Replace("/*POPUP_CSS*/", await File.ReadAllTextAsync(Path.Combine(assets, "pinpopup.css")))
                .Replace("/*POPUP_JS*/", await File.ReadAllTextAsync(Path.Combine(assets, "pinpopup.js")))
                .Replace("{{IMAGERY}}", IsImagery ? "true" : "false")
                .Replace("{{PINS_JSON}}", pinsJson); // last, so pin text can never be mistaken for a placeholder

            await File.WriteAllTextAsync(dialog.FileName, html);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not export the map: {ex.Message}", "Export map");
        }
    }

    // Excel research sheet: the pins fill the template's columns; the rest is left for the manual research steps.
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private async Task ExportSheet()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = "AstroValley-Research.xlsx" };
        if (dialog.ShowDialog() != true) return;
        try { await _sheetExporter.ExportAsync(Markers, dialog.FileName); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not export the sheet: {ex.Message}", "Export research sheet"); }
    }

    private static readonly JsonSerializerOptions PinJson = new() { PropertyNamingPolicy = null, WriteIndented = true };

    // Pins as a JSON file: keeps everything a pin carries (outline, links, details), so it can be loaded back later.
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private async Task SavePins()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON File (*.json)|*.json", FileName = "AstroValley-Pins.json" };
        if (dialog.ShowDialog() != true) return;
        try { await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(Markers, PinJson)); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not save the pins: {ex.Message}", "Save pins"); }
    }

    [RelayCommand]
    private async Task LoadPins()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON File (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var pins = JsonSerializer.Deserialize<List<MarkerLocation>>(await File.ReadAllTextAsync(dialog.FileName), PinJson) ?? [];
            var added = AddMarkers(pins);
            if (added == 0)
                System.Windows.MessageBox.Show("No new pins: the file had none, or they are already on the map.", "Load pins");
        }
        catch (Exception ex) // bad JSON or wrong shape
        {
            System.Windows.MessageBox.Show($"Could not load the pins: {ex.Message}", "Load pins");
        }
    }

    private bool HasMarkers() => Markers.Count > 0;

    [RelayCommand]
    private void ClearMarkers()
    {
        Markers.Clear();
        _markerKeys.Clear();
        MarkersCleared?.Invoke();
    }

    /// <summary>Adds pins that are not already on the map and returns how many were new.</summary>
    public int AddMarkers(IEnumerable<MarkerLocation> locations)
    {
        var added = locations
            .Where(l => l.IsValid && _markerKeys.Add(KeyOf(l)))
            .ToList();
        if (added.Count == 0) return 0;

        foreach (var m in added) Markers.Add(m);
        IsSidebarOpen = false; // make room for the map and the parcel list
        MarkersAdded?.Invoke(added);
        return added.Count;
    }

    // Required by the shared state-list style's scroll-offset binding.
    public double ListBoxScrollOffset { get; set; }

    // State list.
    public ObservableCollection<StateViewModel> States { get; } = new(
        new (string Name, string Abbr, int Counties, TaxSaleType Type)[]
        {
            ("Alabama", "AL", 67, Lien), ("Alaska", "AK", 29, Deed), ("Arizona", "AZ", 15, Lien),
            ("Arkansas", "AR", 75, Deed), ("California", "CA", 58, Deed), ("Colorado", "CO", 64, Lien),
            ("Connecticut", "CT", 8, Redeemable), ("District of Columbia", "DC", 1, Lien),
            ("Delaware", "DE", 3, Redeemable), ("Florida", "FL", 67, Hybrid), ("Georgia", "GA", 159, Redeemable),
            ("Hawaii", "HI", 5, Redeemable), ("Idaho", "ID", 44, Deed), ("Illinois", "IL", 102, Hybrid),
            ("Indiana", "IN", 92, Hybrid), ("Iowa", "IA", 99, Lien), ("Kansas", "KS", 105, Deed),
            ("Kentucky", "KY", 120, Lien), ("Louisiana", "LA", 64, Redeemable), ("Maine", "ME", 16, Deed),
            ("Maryland", "MD", 24, Lien), ("Massachusetts", "MA", 14, Redeemable), ("Michigan", "MI", 83, Deed),
            ("Minnesota", "MN", 87, Deed), ("Mississippi", "MS", 82, Lien), ("Missouri", "MO", 115, Lien),
            ("Montana", "MT", 56, Lien), ("Nebraska", "NE", 93, Lien), ("Nevada", "NV", 17, Hybrid),
            ("New Hampshire", "NH", 10, Deed), ("New Jersey", "NJ", 21, Lien), ("New Mexico", "NM", 33, Deed),
            ("New York", "NY", 62, Hybrid), ("North Carolina", "NC", 100, Deed), ("North Dakota", "ND", 53, Deed),
            ("Ohio", "OH", 88, Hybrid), ("Oklahoma", "OK", 77, Deed), ("Oregon", "OR", 36, Deed),
            ("Pennsylvania", "PA", 67, Deed), ("Rhode Island", "RI", 5, Redeemable), ("South Carolina", "SC", 46, Lien),
            ("South Dakota", "SD", 66, Lien), ("Tennessee", "TN", 95, Redeemable), ("Texas", "TX", 254, Redeemable),
            ("Utah", "UT", 29, Deed), ("Vermont", "VT", 14, Lien), ("Virginia", "VA", 133, Deed),
            ("Washington", "WA", 39, Deed), ("West Virginia", "WV", 55, Hybrid), ("Wisconsin", "WI", 72, Deed),
            ("Wyoming", "WY", 23, Lien)
        }.Select(s => new StateViewModel
        {
            Name = s.Name, Abbreviation = s.Abbr, CountyCount = s.Counties, TaxStatus = s.Type
        }));

    // Satellite imagery instead of the plain street map.
    [ObservableProperty]
    private bool _isImagery;

    [RelayCommand]
    private void ShowMap() => IsImagery = false;

    [RelayCommand]
    private void ShowSatellite() => IsImagery = true;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;
}
