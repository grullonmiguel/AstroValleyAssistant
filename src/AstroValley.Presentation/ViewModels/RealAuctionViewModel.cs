using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

public partial class RealAuctionViewModel : PropertyScraperViewModelBase
{
    private readonly IRealTaxDeedClient _realScraper;

    // -----------------------------
    // UI State
    // -----------------------------

    public string? CurrentAuctionUrl
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? CurrentAuctionAlias
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public RealAuctionCalendarDataViewModel RealAuctionCalendarData { get; }

    // -----------------------------
    // Constructor
    // -----------------------------
    public RealAuctionViewModel(
        IRegridService regridService,
        IRealTaxDeedClient realScraper,
        IBrowserService browserService,
        IClipboardExporter clipboardExporter,
        RealAuctionCalendarDataViewModel realAuctionData,
        IServiceProvider serviceProvider, IDialogService dialogService)
    {
        _realScraper = realScraper;
        _regridService = regridService;
        _browserService = browserService;
        _clipboardExporter = clipboardExporter;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;

        RealAuctionCalendarData = realAuctionData;
        RealAuctionCalendarData.AuctionUrlAvailable += OnAuctionUrlAvailable;
        RealAuctionCalendarData.Initialize();
    }

    // -----------------------------
    // RealAuction Loading
    // -----------------------------

    [RelayCommand(CanExecute = nameof(CanLoadRealAuction))]
    public async Task LoadRealAuctionAsync()
    {
        BeginOperation("Loading RealAuction data...");

        try
        {
            var ct = _cts!.Token;

            var progress = new Progress<int>(count =>
            {
                Status = $"Items Found: {count}";
            });

            var records = await _realScraper
                .GetAuctionRecordsAsync(CurrentAuctionUrl, ct, progress)
                .ConfigureAwait(false);

            App.Current.Dispatcher.Invoke(() =>
            {
                PropertyRecords.Clear();
                foreach (var record in records)
                    PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
            });

            SetIdle($"Loaded {PropertyRecords.Count} properties.");
            IsScrapeVisible = false;
            IsResultButtonsVisible = true;
        }
        catch (OperationCanceledException)
        {
            SetIdle("Scrape canceled.");
        }
        catch (Exception ex)
        {
            SetIdle($"Error: {ex.Message}");
        }
    }
    private bool CanLoadRealAuction => !IsScraping;

    [RelayCommand]
    private void OpenRealAuction() => _browserService!.Launch(CurrentAuctionUrl);

    // -----------------------------
    // Helpers
    // -----------------------------

    private void OnAuctionUrlAvailable(string url, DateTime date)
    {
        CurrentAuctionUrl = url;
        var countyName = RealAuctionCalendarData?.SelectedCounty?.Name ?? "Auction";
        CurrentAuctionAlias = $"{countyName} - {date:M/d/yy}";
    }
}