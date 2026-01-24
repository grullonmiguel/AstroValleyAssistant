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
    public class RealAuctionCalendarDataViewModel : ViewModelBase
    {
        #region Fields

        private readonly IRealAuctionSettings _settings;
        private readonly RealAuctionDataService _dataService;

        // Hold cached values from settings
        private string? _initialStateCode;
        private string? _initialCountyName;
        private DateTime? _initialDate;

        public event Action<string, DateTime>? AuctionUrlAvailable;

        // Map state code → full display name.
        private static readonly Dictionary<string, string> StateNames = new()
        {
            ["FL"] = "Florida",
            ["TX"] = "Texas",
            ["WA"] = "Washington"
        };

        #endregion

        #region Commands

        private ICommand? _generateAuctionUrlCommand;
        public ICommand GenerateAuctionUrlCommand => _generateAuctionUrlCommand ??= new RelayCommand(_ => UpdateAuctionUrl(),_ => CanUpdateUrl());

        #endregion

        #region Properties
        
        public string? AuctionUrl
        {
            get => _auctionUrl;
            private set => Set(ref _auctionUrl, value);
        }
        private string? _auctionUrl;

        public IReadOnlyList<StateInfo> States { get; private set; } = Array.Empty<StateInfo>();

        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set => Set(ref _selectedDate, value);
        }
        private DateTime? _selectedDate;

        public StateInfo? SelectedState
        {
            get => _selectedState;
            set
            {
                Set(ref _selectedState, value);
                _= LoadCountiesForSelectedState();
            }
        }
        private StateInfo? _selectedState;
                
        public RealAuctionDataService.RealAuctionCountyInfo? SelectedCounty
        {
            get => _selectedCounty;
            set => Set(ref _selectedCounty, value);
        }
        private RealAuctionDataService.RealAuctionCountyInfo? _selectedCounty;

        public ObservableCollection<RealAuctionDataService.RealAuctionCountyInfo> Counties
        {
            get => _counties;
            private set => Set(ref _counties, value);
        }
        private ObservableCollection<RealAuctionDataService.RealAuctionCountyInfo> _counties = new();

        // Tells the UI dates prior to today are not allowed
        public DateTime MinAuctionDate => DateTime.Today;

        #endregion

        #region Constructor

        public RealAuctionCalendarDataViewModel(RealAuctionDataService dataService, IRealAuctionSettings settings)
        {
            _dataService = dataService;
            _settings = settings;
        }

        #endregion

        #region Methods

        public void Initialize() => Task.Run(InitializeAsync);

        private async Task InitializeAsync()
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

            // Restore state
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

            // baseline = what's in settings when the app opens
            _initialStateCode = SelectedState?.Code;
            _initialCountyName = SelectedCounty?.Name;
            _initialDate = SelectedDate?.Date;

            UpdateAuctionUrl();
            NotifyAuctionUrlIfValid();
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

        private void UpdateAuctionUrl()
        {
            // Must have county + date to build URL or pass minimum date.
            if (SelectedCounty is null || SelectedDate is null || SelectedDate.Value.Date < MinAuctionDate)
            {
                AuctionUrl = null;
                return;
            }

            // Format date as MM/dd/yyyy (e.g., 12/02/2025).
            var dateString = SelectedDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

            // County.Auction is the real auction URL for a given county
            AuctionUrl = string.Format(CultureInfo.InvariantCulture, SelectedCounty.Auction, dateString);

            SaveSettings();
            NotifyAuctionUrlIfValid();

            // new baseline after successful update
            _initialStateCode = SelectedState?.Code;
            _initialCountyName = SelectedCounty?.Name;
            _initialDate = SelectedDate?.Date;
        }

        private bool CanUpdateUrl()
        {
            if (SelectedState is null || SelectedCounty is null || SelectedDate is null)
                return false;

            var date = SelectedDate.Value.Date;
            if (date < DateTime.Today)
                return false;

            // must differ from what we loaded from settings
            bool changed =
                !string.Equals(_initialStateCode, SelectedState.Code, StringComparison.Ordinal) ||
                !string.Equals(_initialCountyName, SelectedCounty.Name, StringComparison.Ordinal) ||
                _initialDate != date;

            return changed;
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