using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

namespace AstroValley.Presentation.ViewModels;

public partial class RealAuctionViewModel : PropertyScraperViewModelBase
{
    private readonly IRealTaxDeedClient _realScraper;
    private readonly IRealAuctionSettings _realAuctionSettings;

    // Matches realtaxdeed.com or realforeclose.com auction preview URLs with an AUCTIONDATE param
    private static readonly Regex RealAuctionUrlPattern = new(
        @"^https?://[^.]+\.real(taxdeed|foreclose)\.com/.*[?&]zaction=AUCTION.*AUCTIONDATE=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenRealAuctionCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadRealAuctionCommand))]
    public partial string? CurrentAuctionUrl { get; private set; }

    [ObservableProperty]
    public partial string? CurrentAuctionAlias { get; private set; }

    public RealAuctionViewModel(
        IRegridService regridService,
        IRealTaxDeedClient realScraper,
        IBrowserService browserService,
        IClipboardExporter clipboardExporter,
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IRealAuctionSettings realAuctionSettings)
    {
        _realScraper = realScraper;
        _regridService = regridService;
        _browserService = browserService;
        _clipboardExporter = clipboardExporter;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _realAuctionSettings = realAuctionSettings;

        // Restore last saved URL on startup
        if (!string.IsNullOrWhiteSpace(_realAuctionSettings.Url))
        {
            CurrentAuctionUrl = _realAuctionSettings.Url;
            CurrentAuctionAlias = BuildAlias(_realAuctionSettings.Url);
        }
    }

    /// <summary>
    /// Extracts a friendly "County - M/d/yy" label from a realtaxdeed.com auction URL.
    /// Falls back to the raw URL if parsing fails.
    /// </summary>
    private static string BuildAlias(string url)
    {
        try
        {
            var uri = new Uri(url);

            // Subdomain is the county name: "alachua.realtaxdeed.com" → "Alachua"
            var host = uri.Host; // e.g. "alachua.realtaxdeed.com"
            var county = host.Split('.')[0];
            county = char.ToUpper(county[0]) + county[1..];

            // AUCTIONDATE query param: "12/02/2025" → DateTime → "12/2/25"
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var dateStr = query["AUCTIONDATE"];

            if (DateTime.TryParse(dateStr, out var date))
                return $"{county} - {date:M/d/yy}";

            return county;
        }
        catch
        {
            return url;
        }
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

    [RelayCommand(CanExecute = nameof(CanOpenWebNavigation))]
    private async Task OpenWebNavigation()
    {
        var dialogVm = new WebNavigationDialogViewModel("https://www.realauction.com/clients");
        _dialogService!.ShowDialog(dialogVm);

        var selectedUrl = await dialogVm.Result;

        if (selectedUrl is null)
            return;

        if (!RealAuctionUrlPattern.IsMatch(selectedUrl))
        {
            Status = "Invalid URL: please navigate to a specific auction date page on realtaxdeed.com.";
            return;
        }

        CurrentAuctionUrl = selectedUrl;
        CurrentAuctionAlias = BuildAlias(selectedUrl);

        // Persist so it survives app restarts
        _realAuctionSettings.Url = selectedUrl;
        _realAuctionSettings.Save();

        Status = "Auction URL updated. Ready to load.";
    }

    private bool CanOpenWebNavigation() => !IsScraping;

    [RelayCommand(CanExecute = nameof(CanOpenRealAuction))]
    private void OpenRealAuction() => _browserService!.Launch(CurrentAuctionUrl);

    // Shared guard — used by both commands
    private bool CanExecuteAuctionCommands() => !IsScraping && CurrentAuctionUrl is not null;

    private bool CanOpenRealAuction() => CurrentAuctionUrl is not null;

    // IsScraping notifications for derived commands
    protected override void OnIsScrapingChangedCore(bool value)
    {
        LoadRealAuctionCommand.NotifyCanExecuteChanged();
        OpenRealAuctionCommand.NotifyCanExecuteChanged();
        OpenWebNavigationCommand.NotifyCanExecuteChanged();
    }
}