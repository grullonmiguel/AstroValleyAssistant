using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class PropertyDataViewModel : ViewModelBase
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
        private PropertyRecord _record;
        public PropertyRecord Record
        {
            get => _record;
            set
            {
                // Call your Set method
                Set(ref _record, value);

                // Now notify all dependent properties
                RaiseRecordDependentProperties();
            }
        }

        // -----------------------------
        // UI State
        // -----------------------------
        private ScrapeStatus _status = ScrapeStatus.Pending;
        public ScrapeStatus Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private bool _hasMultipleMatches;
        public bool HasMultipleMatches
        {
            get => _hasMultipleMatches;
            set => Set(ref _hasMultipleMatches, value);
        }

        public ObservableCollection<RegridMatch> Matches { get; } = new();

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
        public string GeoCoordinates => Record.GeoCoordinates;
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
        // Commands
        // -----------------------------
        public ICommand OpenAppraiserCommand =>
            new RelayCommand<object>(_ =>
            {
                var url = UrlBuilder.BuildAppraiserUrl(Record);
               _browserService.Launch(url);
            });

        public ICommand OpenRegridCommand =>
            new RelayCommand<object>(_ =>
            {
                if (!string.IsNullOrWhiteSpace(ParcelId))
                {
                    var url = UrlBuilder.BuildRegridSearchUrl(Record);
                    _browserService.Launch(url);
                }
            });

        public ICommand OpenMapsCommand =>
            new RelayCommand<object>(_ =>
            {
                var url = UrlBuilder.BuildGoogleMapsUrl(Record);
                if (url != null) _browserService.Launch(url);
            });

        public ICommand OpenFloodCommand =>
            new RelayCommand<object>(_ =>
            {
                var url = UrlBuilder.BuildFemaFloodUrl(Record);
                if (url != null) _browserService.Launch(url);
            });
    }
}