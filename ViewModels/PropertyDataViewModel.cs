using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class PropertyDataViewModel : ViewModelBase
    {
        #region Commands

        // Command to open the AppraiserUrl
        private ICommand? _openAppraiserCommand;
        public ICommand OpenAppraiserCommand => _openAppraiserCommand ??= new RelayCommand<Button>(ExecuteOpenAppraiser);

        private ICommand? _openRegridCommand;
        public ICommand OpenRegridCommand => _openRegridCommand ??= new RelayCommand<object>(ExecuteOpenRegrid);

        private ICommand? _openMapsCommand;
        public ICommand OpenMapsCommand => _openMapsCommand ??= new RelayCommand<object>(ExecuteOpenMaps);

        private ICommand? _openFloodCommand;
        public ICommand OpenFloodCommand => _openFloodCommand ??= new RelayCommand<object>(ExecuteOpenFlood);

        #endregion

        #region Properties
        // 1. The underlying data models
        public AuctionRecord Record { get; }
       
        // Holds Regrid data
        private RegridRecord? _regrid;
        public RegridRecord? Regrid
        {
            get => _regrid;
            set
            {
                if (_regrid != value)
                {
                    _regrid = value;
                    // Notifying with string.Empty tells WPF that ALL properties on this 
                    // ViewModel have changed.
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        // 2. Real Auction Properties
        public string ParcelId => Record.ParcelId;
        public string Address => Record.PropertyAddress;
        public decimal Bid => Record.OpeningBid;
        public decimal? AssessedValue => Record.AssessedValue;
        public string DateDisplay => Record.AuctionDate.ToShortDateString();
        public string AppraiserUrl => Record.AppraiserUrl;

        // 3. New Enriched Properties (Mapping from the Regrid model)
        // These will now automatically update when the 'Regrid' property above is set
        public string County => Regrid?.Address ?? string.Empty;
        public string City => Regrid?.City ?? string.Empty;
        public string Zip => Regrid?.Zip ?? string.Empty;
        public double? Acres => Regrid?.Acres;
        public string Owner => Regrid?.Owner ?? "Loading...";
        public string ZoningCode => Regrid?.ZoningCode ?? string.Empty;
        public string ZoningType => Regrid?.ZoningType ?? string.Empty;
        public string FloodZone => Regrid?.FloodZone ?? string.Empty;
        public string ElevationLow => Regrid?.ElevationLow ?? string.Empty;
        public string ElevationHigh => Regrid?.ElevationHigh ?? string.Empty;
        public string GeoCoordinates => Regrid?.GeoCoordinates ?? string.Empty;
        public string RegridUrl => Regrid?.RegridUrl ?? string.Empty;

        // 4. Computed Link Properties (UI Logic)
        public string GoogleUrl => Regrid != null
            ? $"https://www.google.com/maps/search/?api=1&query={Regrid.GeoCoordinates}"
            : string.Empty;

        public string FemaUrl => Regrid != null
            ? $"https://msc.fema.gov/portal/search?query={Uri.EscapeDataString(Address)}"
            : string.Empty;

        public ScrapeStatus Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private ScrapeStatus _status = ScrapeStatus.Pending;

        public ObservableCollection<RegridMatch> Matches { get; set; } = [];

        public bool HasMultipleMatches
        {
            get => _hasMultipleMatches;
            set => Set(ref _hasMultipleMatches, value);
        }
        private bool _hasMultipleMatches;

        #endregion

        public PropertyDataViewModel(AuctionRecord record)
        {
            Record = record;
        }

        private void ExecuteOpenRegrid(object? obj)
        {
            // Regrid requires the Parcel ID captured during scraping 
            if (string.IsNullOrEmpty(ParcelId)) return;

            // Build search URL: context narrows results to avoid ambiguity
            // Note: Defaulting to /us context as per Regrid support guidelines
            string url = $"https://app.regrid.com/search?query={Uri.EscapeDataString(ParcelId)}&context=/us";
            LaunchBrowser(url);
        }

        private void ExecuteOpenMaps(object? obj)
        {
            // Record.RawCoordinates might be "29.691032, 20-82.353909"
            string dmsCoords = GeoCoordinates.ToDmsCoordinates();

            // Prioritize coordinate-based lookup for pinpoint accuracy
            if (!string.IsNullOrEmpty(dmsCoords))
            {
                // Format: google.com/maps?q=lat,lon
                string url = $"https://www.google.com/maps/search/?api=1&query={dmsCoords}";
                LaunchBrowser(url);
            }
            else if (!string.IsNullOrEmpty(Address))
            {
                string url = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(Address)}";
                LaunchBrowser(url);
            }
        }

        private void ExecuteOpenFlood(object? obj)
        {
            string dmsCoords = GeoCoordinates.ToDmsCoordinates();

            // FEMA Flood Map Service Center search pattern
            if (!string.IsNullOrEmpty(dmsCoords))
            {
                // Format: google.com/maps?q=lat,lon
                string url = $"https://msc.fema.gov/portal/search?AddressQuery={dmsCoords}";
                LaunchBrowser(url);
            }
            else if (!string.IsNullOrEmpty(Address))
            {
                string url = $"https://msc.fema.gov/portal/search?address={Uri.EscapeDataString(Address)}";
                LaunchBrowser(url);
            }
        }

        private void ExecuteOpenAppraiser(object? obj)
        {
            if (string.IsNullOrEmpty(Record.AppraiserUrl)) return;

            LaunchBrowser(Record.AppraiserUrl);
        }

        public void LaunchBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching {url}: {ex.Message}");
            }
        }
    }
}
