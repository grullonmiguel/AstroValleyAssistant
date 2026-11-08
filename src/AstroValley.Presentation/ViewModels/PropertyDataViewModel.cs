using AstroValley.Domain.Entities;
using AstroValley.Domain.Enums;
using AstroValley.Domain.Utilities;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels;

public partial class PropertyDataViewModel : ObservableObject
{
    private readonly IBrowserService _browserService;

    public PropertyDataViewModel(PropertyRecord record, IBrowserService browserService)
    {
        Record = record;
        _browserService = browserService;
    }

    // -----------------------------
    // Domain Model
    // -----------------------------
    public PropertyRecord Record
    {
        get;
        set
        {
            // Call your Set method
            SetProperty(ref field, value);

            // Now notify all dependent properties
            RaiseRecordDependentProperties();
        }
    }

    // -----------------------------
    // UI State
    // -----------------------------
    public ScrapeStatus Status
    {
        get;
        set => SetProperty(ref field, value);
    } = ScrapeStatus.Pending;

    public bool HasMultipleMatches
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<RegridMatch> Matches { get; } = [];

    // -----------------------------
    // RealAuction Convenience Properties
    // -----------------------------
    public string ParcelId => Record.ParcelId;
    public string Address => Record.Address;
    public decimal? Bid => Record.OpeningBid;
    public string AppraiserUrl => Record.AppraiserUrl;
    public decimal? AssessedValue => Record.AssessedValue;
    public string DateDisplay => Record.AuctionDate.ToShortDateString();

    // -----------------------------
    // Regrid Convenience Properties
    // -----------------------------
    public string City => Record.City;
    public string Zip => Record.Zip;
    public double? Acres => Record.Acres;
    public string Owner => Record.Owner;
    public string ZoningCode => Record.ZoningCode;
    public string ZoningType => Record.ZoningType;
    public string PropertyType => Record.PropertyType;
    public string FloodZone => Record.FloodZone;
    public string ElevationLow => Record.ElevationLow;
    public string ElevationHigh => Record.ElevationHigh;
    public string Latitude => Record.Latitude;
    public string Longitude => Record.Longitude;
    public string GeoCoordinates => Record.GeoCoordinates;
    public string ParcelLines => Record.ParcelLines;
    public string RegridUrl => Record.RegridUrl;

    // Aerial photo for the details panel (null until the Regrid scrape gives coordinates)
    private AerialImage.Result? _aerial;
    public AerialImage.Result? Aerial => _aerial ??= AerialImage.Build(Latitude, Longitude, ParcelLines, 168, 110);

    private string? _aerialCaption;
    private bool _aerialLoading;
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // Reading it the first time starts a one-off lookup of the capture date (Esri metadata layers: 60 cm, then 1.2 m)
    public string AerialCaption
    {
        get
        {
            if (_aerialCaption is null && !_aerialLoading && Aerial is not null) { _aerialLoading = true; _ = LoadAerialCaptionAsync(); }
            return _aerialCaption ?? "Aerial: Esri World Imagery";
        }
    }

    private async Task LoadAerialCaptionAsync()
    {
        try
        {
            foreach (var layer in new[] { 10, 11 })
            {
                var json = await Http.GetStringAsync(FormattableString.Invariant(
                    $"{AerialImage.ServiceUrl}/{layer}/query?geometry={Longitude},{Latitude}&geometryType=esriGeometryPoint&inSR=4326&outFields=SRC_DATE2,SRC_RES&returnGeometry=false&f=json"));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0) continue;
                var a = features[0].GetProperty("attributes");
                if (a.GetProperty("SRC_DATE2").ValueKind != System.Text.Json.JsonValueKind.Number) continue;
                var date = DateTimeOffset.FromUnixTimeMilliseconds(a.GetProperty("SRC_DATE2").GetInt64()).UtcDateTime;
                var res = a.GetProperty("SRC_RES").ValueKind == System.Text.Json.JsonValueKind.Number ? $" · {a.GetProperty("SRC_RES").GetDouble():0.##} m" : "";
                _aerialCaption = $"Aerial: Esri {date:MMM yyyy}{res}";
                OnPropertyChanged(nameof(AerialCaption));
                return;
            }
        }
        catch { /* service down or slow: keep the plain caption */ }
        finally { _aerialLoading = false; }
    }

    public bool HasGoogleUrl => UrlBuilder.BuildGoogleMapsUrl(Record) != null;
    public bool HasFemaUrl => UrlBuilder.BuildFemaFloodUrl(Record) != null;

    // -----------------------------
    // Update Properties
    // -----------------------------
    private void RaiseRecordDependentProperties()
    {
        OnPropertyChanged(nameof(ParcelId));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(AssessedValue));
        OnPropertyChanged(nameof(Acres));
        OnPropertyChanged(nameof(Owner));
        OnPropertyChanged(nameof(City));
        OnPropertyChanged(nameof(Zip));
        OnPropertyChanged(nameof(ZoningCode));
        OnPropertyChanged(nameof(ZoningType));
        OnPropertyChanged(nameof(PropertyType));
        OnPropertyChanged(nameof(FloodZone));
        OnPropertyChanged(nameof(ElevationLow));
        OnPropertyChanged(nameof(ElevationHigh));
        OnPropertyChanged(nameof(GeoCoordinates));
        OnPropertyChanged(nameof(RegridUrl));
        _aerial = null; _aerialCaption = null; // a new scrape result can move the pin
        OnPropertyChanged(nameof(Aerial));
        OnPropertyChanged(nameof(AerialCaption));
        OnPropertyChanged(nameof(HasFemaUrl));
        OnPropertyChanged(nameof(HasGoogleUrl));
    }

    // -----------------------------
    // ✅ Commands
    // -----------------------------
    [RelayCommand]
    private void OpenAppraiser()
    {
        var url = UrlBuilder.BuildAppraiserUrl(Record);
        _browserService.Launch(url);
    }

    [RelayCommand(CanExecute = nameof(CanOpenRegrid))]
    private void OpenRegrid()
    {
        var url = UrlBuilder.BuildRegridSearchUrl(Record);
        _browserService.Launch(url);
    }
    private bool CanOpenRegrid() => !string.IsNullOrWhiteSpace(ParcelId);

    [RelayCommand]
    private void OpenMaps()
    {
        var url = UrlBuilder.BuildGoogleMapsUrl(Record);
        if (url != null) _browserService.Launch(url);
    }

    [RelayCommand]
    private void OpenFlood()
    {
        var url = UrlBuilder.BuildFemaFloodUrl(Record);
        if (url != null) _browserService.Launch(url);
    }
}