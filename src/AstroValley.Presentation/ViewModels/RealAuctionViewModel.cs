using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

public partial class RealAuctionViewModel : PropertyScraperViewModelBase
{
    private readonly IRealTaxDeedClient _realScraper;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenRealAuctionCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadRealAuctionCommand))]
    public partial string? CurrentAuctionUrl { get; private set; }

    [ObservableProperty]
    public partial string? CurrentAuctionAlias { get; private set; }

    public RealAuctionCalendarDataViewModel RealAuctionCalendarData { get; }

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

    [RelayCommand(CanExecute = nameof(CanExecuteAuctionCommands))]
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
                .GetAuctionRecordsAsync(CurrentAuctionUrl, ct, progress);

            PropertyRecords.Clear();
            foreach (var record in records)
                PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));


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

    [RelayCommand(CanExecute = nameof(CanOpenRealAuction))]
    private void OpenRealAuction() => _browserService!.Launch(CurrentAuctionUrl);

    // Shared guard — used by both commands
    private bool CanExecuteAuctionCommands() => !IsScraping && CurrentAuctionUrl is not null;

    private bool CanOpenRealAuction() => CurrentAuctionUrl is not null;
    
    private void OnAuctionUrlAvailable(string url, DateTime date)
    {
        CurrentAuctionUrl = url;
        var countyName = RealAuctionCalendarData?.SelectedCounty?.Name ?? "Auction";
        CurrentAuctionAlias = $"{countyName} - {date:M/d/yy}";
    }

    // IsScraping notifications for derived commands
    protected override void OnIsScrapingChangedCore(bool value)
    {
        LoadRealAuctionCommand.NotifyCanExecuteChanged();
        OpenRealAuctionCommand.NotifyCanExecuteChanged();
    }
}