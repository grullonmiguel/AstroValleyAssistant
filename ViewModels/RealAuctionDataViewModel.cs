using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class RealAuctionDataViewModel : ViewModelBase
    {
        private readonly IRealAuctionSettings _settings;
        private readonly RealAuctionDataService _dataService;


        public event Action<string, DateTime>? AuctionUrlAvailable;

        // Map state code → full display name.
        private static readonly Dictionary<string, string> StateNames = new()
        {
            ["FL"] = "Florida",
            ["TX"] = "Texas",
            ["WA"] = "Washington"
        };

        #region Commands

        private ICommand? _generateAuctionUrlCommand;
        public ICommand GenerateAuctionUrlCommand => _generateAuctionUrlCommand ??= new RelayCommand(_ => UpdateAuctionUrl(),_ => CanUpdateUrl());

        #endregion

        #region Properties

        public IReadOnlyList<StateInfo> States { get; private set; } = Array.Empty<StateInfo>();

        public StateInfo? SelectedState
        {
            get => _selectedState;
            set
            {
                Set(ref _selectedState, value);
                LoadCountiesForSelectedState();
            }
        }
        private StateInfo? _selectedState;

        public ObservableCollection<RealAuctionDataService.RealAuctionCountyInfo> Counties
        {
            get => _counties;
            private set
            {
                Set(ref _counties, value);
            }
        }
        private ObservableCollection<RealAuctionDataService.RealAuctionCountyInfo> _counties = new();
                
        public RealAuctionDataService.RealAuctionCountyInfo? SelectedCounty
        {
            get => _selectedCounty;
            set
            {
                Set(ref _selectedCounty, value);
            }
        }
        private RealAuctionDataService.RealAuctionCountyInfo? _selectedCounty;

        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                Set(ref _selectedDate, value);
            }
        }
        private DateTime? _selectedDate;


        // Optional: convenience min date for a DatePicker binding
        public DateTime MinAuctionDate => DateTime.Today;

        
        public string? AuctionUrl
        {
            get => _auctionUrl;
            private set => Set(ref _auctionUrl, value);
        }
        private string? _auctionUrl;

        #endregion

        #region Constructor

        public RealAuctionDataViewModel(RealAuctionDataService dataService, IRealAuctionSettings settings)
        {
            _dataService = dataService;
            _settings = settings;
        }

        #endregion

        #region Methods

        public async Task InitializeAsync()
        {
            await _dataService.InitializeAsync();

            var data = _dataService.CountyData;

            States = data.Keys
                .OrderBy(code => code)
                .Select(code => new StateInfo
                {
                    Code = code,
                    Name = StateNames.TryGetValue(code, out var name) ? name : code
                })
                .ToList();

            OnPropertyChanged(nameof(States));

            // restore state
            if (!string.IsNullOrWhiteSpace(_settings.State))
            {
                SelectedState = States.FirstOrDefault(s => s.Code == _settings.State);
                if (SelectedState is not null)
                {
                    await LoadCountiesForSelectedState();

                    if (!string.IsNullOrWhiteSpace(_settings.County))
                        SelectedCounty = Counties.FirstOrDefault(c => c.Name == _settings.County);
                }
            }

            if (DateTime.TryParse(_settings.LastAuctionDate, out var lastDate))
                SelectedDate = lastDate;

            if (CanUpdateUrl())
            {
                UpdateAuctionUrl();
            }
        }

        private async Task LoadCountiesForSelectedState()
        {
            Counties.Clear();
            SelectedCounty = null;
            AuctionUrl = null;

            if (SelectedState is null)
                return;

            var list = await _dataService.GetCountiesForStateAsync(SelectedState.Code);
            foreach (var county in list)
                Counties.Add(county);
        }

        // Call this from a button command if you want explicit “Generate”,
        // or leave it private to update automatically when selections change.
        private void UpdateAuctionUrl()
        {
            // Must have county + date to build URL.
            if (SelectedCounty is null || SelectedDate is null)
            {
                AuctionUrl = null;
                return;
            }

            // Only allow today or future dates.
            if (SelectedDate.Value.Date < DateTime.Today)
            {
                AuctionUrl = null;
                return;
            }

            // Format date as MM/dd/yyyy (e.g., 12/02/2025).
            var dateString = SelectedDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

            // County.Auction is the template like
            AuctionUrl = string.Format(CultureInfo.InvariantCulture, SelectedCounty.Auction, dateString);

            SaveSettings();
            NotifyAuctionUrlIfValid();
        }

        private bool CanUpdateUrl()
        {
            return SelectedCounty is not null
                   && SelectedDate is not null
                   && SelectedDate.Value.Date >= DateTime.Today;
        }

        private void SaveSettings()
        {
            _settings.State = SelectedState?.Code ?? string.Empty;
            _settings.County = SelectedCounty?.Name ?? string.Empty;
            _settings.LastAuctionDate = SelectedDate?.ToString("MM/dd/yyyy") ?? string.Empty;
            _settings.Url = AuctionUrl ?? string.Empty;

            _settings.Save();
        }

        private void NotifyAuctionUrlIfValid()
        {
            if (string.IsNullOrWhiteSpace(AuctionUrl) ||
                SelectedDate is null ||
                SelectedDate.Value.Date < DateTime.Today)
                return;

            AuctionUrlAvailable?.Invoke(AuctionUrl, SelectedDate.Value);
        }

        #endregion
    }
}