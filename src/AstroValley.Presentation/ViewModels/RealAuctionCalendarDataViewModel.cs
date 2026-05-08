using AstroValley.Application.Interfaces.Data;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AstroValley.Presentation.ViewModels;

public partial class RealAuctionCalendarDataViewModel : ObservableObject
{
    private readonly IRealAuctionSettings _settings;
    private readonly IRealAuctionDataService _dataService;

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
            
    public string? AuctionUrl
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public IReadOnlyList<StateInfo> States { get; private set; } = Array.Empty<StateInfo>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateAuctionUrlCommand))]
    private DateTime? _selectedDate;

    public StateInfo? SelectedState
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _ = LoadCountiesForSelectedState();
                UpdateAuctionUrlCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateAuctionUrlCommand))]
    private RealAuctionCountyInfo? _selectedCounty;

    public ObservableCollection<RealAuctionCountyInfo> Counties
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    // Tells the UI dates prior to today are not allowed
    public DateTime MinAuctionDate => DateTime.Today;

    public RealAuctionCalendarDataViewModel(IRealAuctionDataService dataService, IRealAuctionSettings settings)
    {
        _dataService = dataService;
        _settings = settings;
    }

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

    [RelayCommand(CanExecute = nameof(CanUpdateUrl))]
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
}