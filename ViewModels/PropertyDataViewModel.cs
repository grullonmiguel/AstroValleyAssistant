using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class PropertyDataViewModel : ViewModelBase
    {
        private readonly IBrowserService _browserService;
        private readonly RealAuctionViewModel? _parentViewModel;

        #region Commands

        private ICommand? _openAppraiserCommand;
        public ICommand OpenAppraiserCommand => _openAppraiserCommand ??= new RelayCommand<Button>(OpenAppraiserUrl);

        private ICommand? _openRegridCommand;
        public ICommand OpenRegridCommand => _openRegridCommand ??= new RelayCommand<object>(OpenRegridUrl);

        private ICommand? _openMapsCommand;
        public ICommand OpenMapsCommand => _openMapsCommand ??= new RelayCommand<object>(OpenGoogleMapsUrl);

        private ICommand? _openFloodCommand;
        public ICommand OpenFloodCommand => _openFloodCommand ??= new RelayCommand<object>(OpenFemaFloodUrl);

        private ICommand? _selectMatchCommand;
        public ICommand SelectMatchCommand => _selectMatchCommand ??= new RelayCommand<RegridMatch>(async (match) =>
        {
            await _parentViewModel.ProcessSingleParcelAsync(this, match!.FullUrl, CancellationToken.None);
        });

        #endregion

        #region Properties

        public PropertyRecord? Record
        {
            get => _record;
            set => Set(ref _record, value); 
        }
        private PropertyRecord? _record = new();

        public ScrapeStatus Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private ScrapeStatus _status = ScrapeStatus.Pending;

        public bool HasMultipleMatches
        {
            get => _hasMultipleMatches;
            set => Set(ref _hasMultipleMatches, value);
        }
        private bool _hasMultipleMatches;

        // Records from Regrid when a search returns multipe matches
        public ObservableCollection<RegridMatch> Matches { get; set; } = [];

        // Real Auction Properties
        public string ParcelId => Record!.ParcelId;
        public string Address => Record!.Address;
        public decimal Bid => Record!.OpeningBid;
        public string AppraiserUrl => Record!.AppraiserUrl;
        public decimal? AssessedValue => Record!.AssessedValue;
        public string DateDisplay => Record!.AuctionDate.ToShortDateString();

        // Regrid Properties
        public string County => Record!.Address ?? string.Empty;
        public string City => Record?.City ?? string.Empty;
        public string Zip => Record?.Zip ?? string.Empty;
        public double? Acres => Record?.Acres;
        public string Owner => Record!.Owner;
        public string ZoningCode => Record?.ZoningCode ?? string.Empty;
        public string ZoningType => Record?.ZoningType ?? string.Empty;
        public string FloodZone => Record?.FloodZone ?? string.Empty;
        public string ElevationLow => Record?.ElevationLow ?? string.Empty;
        public string ElevationHigh => Record?.ElevationHigh ?? string.Empty;
        public string GeoCoordinates => Record?.GeoCoordinates ?? string.Empty;
        public string RegridUrl => Record?.RegridUrl ?? string.Empty;

        // 4. Computed URL Properties
        public string GoogleUrl => string.IsNullOrWhiteSpace(Record?.GeoCoordinates)
            ? string.Empty : $"https://www.google.com/maps/search/?api=1&query={Record.GeoCoordinates.ToDmsCoordinates()}";

        public string FemaUrl => string.IsNullOrWhiteSpace(Record?.GeoCoordinates)
            ? string.Empty : $"https://msc.fema.gov/portal/search?query={Uri.EscapeDataString(Record.GeoCoordinates).ToDmsCoordinates()}";

        #endregion

        #region Constructor

        public PropertyDataViewModel(PropertyRecord record, IBrowserService browserService)
        {
            Record = record;
            _browserService = browserService;
        }

        #endregion

        #region Methods

        private void OpenAppraiserUrl(object? obj)
        {
            // Launch the appraiser URL
            _browserService.Launch(Record!.AppraiserUrl);
        }

        private void OpenFemaFloodUrl(object? obj)
        {
            // Try to convert raw coordinates into DMS format (most accurate for FEMA searches)
            string? dmsCoords = GeoCoordinates.ToDmsCoordinates();

            // Choose the best available search query:
            // 1. Use coordinates if available
            // 2. Otherwise fall back to the address
            string? query =
                dmsCoords
                ?? (!string.IsNullOrWhiteSpace(Address) ? Uri.EscapeDataString(Address) : null);

            // If neither coordinates nor address exist, there's nothing to search
            if (query is null)
                return;

            // FEMA Flood Map Service Center search URL
            string url = dmsCoords != null
                ? $"https://msc.fema.gov/portal/search?AddressQuery={query}"
                : $"https://msc.fema.gov/portal/search?address={query}";

            // Launch the FEMA search in the browser
            _browserService.Launch(url);
        }

        private void OpenGoogleMapsUrl(object? obj)
        {
            // 1. Prefer DMS coordinates for precise map lookup
            // 2. Otherwise fall back to the address (escaped for URL safety)
            string? query =
                GeoCoordinates.ToDmsCoordinates()
                ?? (!string.IsNullOrWhiteSpace(Address) ? Uri.EscapeDataString(Address) : null);

            // If neither coordinates nor address are available, there's nothing to open
            if (query is null)
                return;

            // Launch Google Maps with the resolved query
            _browserService.Launch($"https://www.google.com/maps/search/?api=1&query={query}");
        }

        private void OpenRegridUrl(object? obj)
        {
            // Regrid requires the Parcel ID captured during scraping 
            if (string.IsNullOrEmpty(ParcelId)) return;

            // Build search URL: context narrows results to avoid ambiguity
            string url = $"https://app.regrid.com/search?query={Uri.EscapeDataString(ParcelId)}&context=/us";

            // Launch Regrid with the Parcel ID
            _browserService.Launch(url);
        }

        #endregion
    }
}