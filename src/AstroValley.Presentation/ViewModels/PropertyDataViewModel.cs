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
    public string FloodZone => Record.FloodZone;
    public string ElevationLow => Record.ElevationLow;
    public string ElevationHigh => Record.ElevationHigh;
    public string Latitude => Record.Latitude;
    public string Longitude => Record.Longitude;
    public string GeoCoordinates => Record.GeoCoordinates;
    public string ParcelLines => Record.ParcelLines;
    public string RegridUrl => Record.RegridUrl;

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
        OnPropertyChanged(nameof(FloodZone));
        OnPropertyChanged(nameof(ElevationLow));
        OnPropertyChanged(nameof(ElevationHigh));
        OnPropertyChanged(nameof(GeoCoordinates));
        OnPropertyChanged(nameof(RegridUrl));
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