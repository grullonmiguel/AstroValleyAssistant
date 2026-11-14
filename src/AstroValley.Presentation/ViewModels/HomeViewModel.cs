using AstroValley.Application.Interfaces.Data;
using AstroValley.Application.Interfaces.Export;
using AstroValley.Domain.Enums;
using AstroValley.Domain.Models;
using AstroValley.Presentation.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;

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
    private readonly IBrowserService _browser;
    private readonly IPinStore _pinStore;
    private readonly IServiceProvider _serviceProvider; // IDialogService is resolved when needed: it depends on MainViewModel, which holds this view model

    public HomeViewModel(IPinSheetExporter sheetExporter, IBrowserService browser, IPinStore pinStore, IServiceProvider serviceProvider)
    {
        _sheetExporter = sheetExporter;
        _browser = browser;
        _pinStore = pinStore;
        _serviceProvider = serviceProvider;
        Markers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MatchCount));
            SavePinsCommand.NotifyCanExecuteChanged();
            ExportMapCommand.NotifyCanExecuteChanged();
            ExportSheetCommand.NotifyCanExecuteChanged();
            ClearMarkersCommand.NotifyCanExecuteChanged();
            PlanRouteCommand.NotifyCanExecuteChanged();
        };
        // The parcel list binds to Markers, i.e. this default view, so filtering it filters the list too.
        CollectionViewSource.GetDefaultView(Markers).Filter = o => Matches((MarkerLocation)o);
    }

    // Tools dialog: Draft is what the dialog edits (the match count follows it); Show copies it into _applied, which filters the map and list.
    private PinFilter _applied = new();

    [ObservableProperty]
    private PinFilter _draft = new();

    [ObservableProperty]
    private bool _isToolsOpen;

    /// <summary>"Matching Properties" at the top of the dialog: pins the draft filter would show.</summary>
    public int MatchCount => Markers.Count(Draft.Matches);

    partial void OnDraftChanging(PinFilter value) => Draft.PropertyChanged -= OnDraftEdited;

    partial void OnDraftChanged(PinFilter value)
    {
        value.PropertyChanged += OnDraftEdited;
        OnPropertyChanged(nameof(MatchCount));
    }

    private void OnDraftEdited(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => OnPropertyChanged(nameof(MatchCount));

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

    private void ApplyFilter()
    {
        CollectionViewSource.GetDefaultView(Markers).Refresh();
        OnPropertyChanged(nameof(MatchCount));
        FilterChanged?.Invoke();
    }

    private bool Matches(MarkerLocation m) => _applied.Matches(m);

    [RelayCommand]
    private void OpenTools()
    {
        Draft = _applied.Clone(); // drop edits that were never applied
        IsToolsOpen = true;
    }

    [RelayCommand]
    private void ClearFilters() => Draft = new(); // applied when the user presses Show

    [RelayCommand]
    private void ApplyTools()
    {
        _applied = Draft.Clone();
        IsToolsOpen = false;
        ApplyFilter();
    }

    [RelayCommand]
    private void CloseTools() => IsToolsOpen = false;

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
        AutoSave([pin]);
    }

    /// <summary>Raised after the user accepted comps for a pin, so the map refreshes its popup.</summary>
    public event Action<MarkerLocation>? CompsChanged;

    /// <summary>Raised after the due-diligence dialog closes, so the map refreshes the popup's "Due diligence n/m".</summary>
    public event Action<MarkerLocation>? ChecklistChanged;

    /// <summary>
    /// The popup's "Due diligence": the checklist from the pin's jurisdiction profile (made on first open; items a newer profile adds
    /// are appended to an older saved checklist), the names to search and the risk flags. Answers are kept on the pin and saved with it.
    /// </summary>
    public async void OpenDueDiligence(double lat, double lon)
    {
        var pin = Markers.FirstOrDefault(m => Math.Abs(m.Latitude - lat) < 1e-9 && Math.Abs(m.Longitude - lon) < 1e-9);
        if (pin is null) return;
        var template = AstroValley.Domain.DueDiligence.Jurisdictions.For(pin.State, pin.County).NewChecklist();
        pin.Checklist = pin.Checklist is null ? [.. template] : [.. pin.Checklist, .. template.Where(t => pin.Checklist.All(i => i.Key != t.Key))];
        pin.SearchNames ??= AstroValley.Domain.DueDiligence.Jurisdictions.SearchNames(
            pin.ExtraDetails?.GetValueOrDefault("Owner"), pin.ExtraDetails?.GetValueOrDefault("Other Owners")); // pins saved before names existed
        var dialog = new ViewModels.Dialogs.DueDiligenceViewModel(pin, _browser, SavePinsCommand);
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDialogService>(_serviceProvider).ShowDialog(dialog);
        try { await dialog.Result; }
        catch (TaskCanceledException) { } // closed with X: the answers were already written to the pin
        AutoSave([pin]);
        ChecklistChanged?.Invoke(pin);
    }

    /// <summary>The popup's "Find comps": full-screen browser + sidebar; the accepted result is saved on the pin.</summary>
    public void FindComps(double lat, double lon)
    {
        var pin = Markers.FirstOrDefault(m => Math.Abs(m.Latitude - lat) < 1e-9 && Math.Abs(m.Longitude - lon) < 1e-9);
        if (pin is null) return;
        double.TryParse(pin.Acres, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var acres);
        var d = pin.ExtraDetails;
        int? Int(string key) => int.TryParse(d?.GetValueOrDefault(key), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        double? Num(string key) => double.TryParse(d?.GetValueOrDefault(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var subject = new CompsSubject(pin.Address, acres, pin.PropertyType, pin.Latitude, pin.Longitude, d?.GetValueOrDefault("Zip"),
            Int("Living Sqft"), Int("Beds"), Num("Baths"), d?.GetValueOrDefault("Home Type"), pin);
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CompsViewModel>(_serviceProvider)
            .Open(subject, result => { pin.Comps = result; AutoSave([pin]); CompsChanged?.Invoke(pin); });
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

    /// <summary>
    /// The fail-safe: every pin that reaches the map, and every edit to one, goes straight to its saved set on disk (see IPinStore).
    /// Removing pins from the map never deletes them there; a set is deleted from the Saved Pins dialog.
    /// </summary>
    private void AutoSave(IReadOnlyCollection<MarkerLocation> pins, bool restoreWork = false)
    {
        // an owned property's pin is saved with the property (My Properties), never in an auction's pin set
        foreach (var owned in pins.Where(p => p.OwnedId is not null)) OwnedPinEdited?.Invoke(owned);
        pins = [.. pins.Where(p => p.OwnedId is null)];
        if (pins.Count == 0) return;
        try { _pinStore.Save(pins, restoreWork); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // never interrupts the research; Export Pins still works as the manual way out
            System.Diagnostics.Trace.TraceWarning($"Could not auto-save {pins.Count} pin(s): {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSavedPins() =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Services.Interfaces.IDialogService>(_serviceProvider)
            .ShowDialog(new ViewModels.Dialogs.SavedPinsViewModel(_pinStore, _browser, AddMarkers));

    // --- Owned properties: Home draws their pins; MyPropertiesViewModel keeps them and listens to these ---

    /// <summary>Notes or comps changed in an owned pin's popup.</summary>
    public event Action<MarkerLocation>? OwnedPinEdited;

    /// <summary>The popup's "My Properties" link, with the property's Id.</summary>
    public event Action<string>? OwnedPropertyRequested;

    /// <summary>The popup's "I bought this" on an auction pin.</summary>
    public event Action<MarkerLocation>? BoughtRequested;

    /// <summary>Asks the map to move to a pin.</summary>
    public event Action<MarkerLocation>? FlyToRequested;

    public void OpenOwned(string id) => OwnedPropertyRequested?.Invoke(id);

    public void Bought(double lat, double lon)
    {
        var pin = Markers.FirstOrDefault(m => Math.Abs(m.Latitude - lat) < 1e-9 && Math.Abs(m.Longitude - lon) < 1e-9);
        if (pin is { OwnedId: null }) BoughtRequested?.Invoke(pin);
    }

    public void FlyTo(MarkerLocation pin) => FlyToRequested?.Invoke(pin);

    /// <summary>Raised with only the pins that were new, so the map never draws a pin twice.</summary>
    public event Action<IReadOnlyList<MarkerLocation>>? MarkersAdded;

    public event Action<MarkerLocation>? MarkerRemoved;
    public event Action? MarkersCleared;

    [RelayCommand]
    private void RemoveMarker(MarkerLocation marker)
    {
        if (!Markers.Remove(marker)) return;
        _markerKeys.Remove(marker.Key); // so the same pin can be sent again later
        MarkerRemoved?.Invoke(marker);
    }

    // Writes the current pins to a standalone HTML map (list + fly-to + popups, no delete). Needs internet to show tiles.
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private async Task ExportMap()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "HTML File (*.html)|*.html", FileName = "AstroValley-Pins.html" };
        if (dialog.ShowDialog() != true) return;

        BeginExport("Exporting map...");
        try
        {
            await File.WriteAllTextAsync(dialog.FileName, await Services.PinsHtml.BuildAsync(Markers, IsImagery));
            OpenFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not export the map: {ex.Message}", "Export map");
        }
        finally { IsScraping = false; }
    }

    // Busy overlay state (ScrapeOverlay binds these); the export has no known total, so no percent bar.
    [ObservableProperty]
    private bool _isScraping;

    [ObservableProperty]
    private string? _progressText;

    public bool IsProgressIndeterminate => true;

    private void BeginExport(string message)
    {
        ProgressText = message;
        IsScraping = true;
    }

    // Excel research sheet: the pins fill the template's columns; the rest is left for the manual research steps.
    [RelayCommand(CanExecute = nameof(HasMarkers))]
    private async Task ExportSheet()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = "AstroValley-Research.xlsx" };
        if (dialog.ShowDialog() != true) return;
        BeginExport("Exporting research sheet...");
        try { await _sheetExporter.ExportAsync(Markers, dialog.FileName); OpenFile(dialog.FileName); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not export the sheet: {ex.Message}", "Export research sheet"); }
        finally { IsScraping = false; }
    }

    // Opens a file we just wrote in its default app (browser for .html, Excel for .xlsx)
    private static void OpenFile(string path) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

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

    [RelayCommand(CanExecute = nameof(HasMarkers))]
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
            .Where(l => l.IsValid && _markerKeys.Add(l.Key))
            .ToList();
        if (added.Count == 0) return 0;

        var fromAuctions = added.Where(l => l.OwnedId is null).ToList(); // owned pins arrive from My Properties, already saved there
        if (fromAuctions.Count > 0) AutoSave(fromAuctions, restoreWork: true); // before they are drawn, so a parcel scraped again shows the research already done on it

        foreach (var m in added) Markers.Add(m);
        if (fromAuctions.Count > 0) IsSidebarOpen = false; // make room for the map and the parcel list
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
