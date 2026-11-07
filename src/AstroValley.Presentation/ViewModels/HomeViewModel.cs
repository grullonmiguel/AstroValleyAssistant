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

    public HomeViewModel()
    {
        Markers.CollectionChanged += (_, _) =>
        {
            SavePinsCommand.NotifyCanExecuteChanged();
            ExportMapCommand.NotifyCanExecuteChanged();
        };
        // The parcel list binds to Markers, i.e. this default view, so filtering it filters the list too.
        CollectionViewSource.GetDefaultView(Markers).Filter = o => Matches((MarkerLocation)o);
    }

    // Quick filters shown as chips under the map: All | Week | Under5k | Acre.
    [ObservableProperty]
    private string _filter = "All";

    [ObservableProperty]
    private bool _isTerrain;

    [ObservableProperty]
    private bool _isTouring;

    /// <summary>Raised when the filter changed; the map then hides <see cref="HiddenMarkers"/>.</summary>
    public event Action? FilterChanged;
    public event Action? TerrainToggleRequested;
    public event Action? TourToggleRequested;

    public IEnumerable<MarkerLocation> HiddenMarkers => Markers.Where(m => !Matches(m));

    partial void OnFilterChanged(string value)
    {
        CollectionViewSource.GetDefaultView(Markers).Refresh();
        FilterChanged?.Invoke();
    }

    private bool Matches(MarkerLocation m)
    {
        var today = DateTime.Today;
        return Filter switch
        {
            "Week" => m.AuctionDate is { } d && d.Date >= today && d.Date <= today.AddDays(7),
            "Under5k" => m.ExtraDetails?.GetValueOrDefault("Minimum Bid") is { } bid
                         && decimal.TryParse(bid, System.Globalization.NumberStyles.Currency, null, out var amount) && amount < 5000,
            "Acre" => double.TryParse(m.Acres, out var acres) && acres > 1,
            _ => true
        };
    }

    [RelayCommand]
    private void SetFilter(string filter) => Filter = filter;

    [RelayCommand]
    private void ToggleTerrain() => TerrainToggleRequested?.Invoke();

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
