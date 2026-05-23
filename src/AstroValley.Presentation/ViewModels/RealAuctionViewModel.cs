using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels;

public partial class RealAuctionViewModel : PropertyScraperViewModelBase
{
    private readonly IRealTaxDeedClient _realScraper;
    private readonly IRealAuctionSettings _realAuctionSettings;

    private const int MaxRecentUrls = 10;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenRealAuctionCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadRealAuctionCommand))]
    public partial string? CurrentAuctionUrl { get; private set; }

    [ObservableProperty]
    public partial string? CurrentAuctionAlias { get; private set; }

    /// <summary>True when the AUCTIONDATE in the saved URL is before today.</summary>
    [ObservableProperty]
    public partial bool IsAuctionDateStale { get; private set; }

    public ObservableCollection<AuctionUrlEntry> RecentAuctionUrls { get; } = [];

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

        // Restore recent URLs list
        foreach (var url in _realAuctionSettings.RecentUrls)
            RecentAuctionUrls.Add(new AuctionUrlEntry(url, BuildAlias(url)));

        // Restore last saved URL on startup
        if (!string.IsNullOrWhiteSpace(_realAuctionSettings.Url))
            ApplyUrl(_realAuctionSettings.Url, persist: false);
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
            var county = uri.Host.Split('.')[0];
            county = char.ToUpper(county[0]) + county[1..];

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

    /// <summary>
    /// Parses the AUCTIONDATE from the URL. Returns null if not found or unparseable.
    /// </summary>
    private static DateTime? ParseAuctionDate(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var dateStr = query["AUCTIONDATE"];
            return DateTime.TryParse(dateStr, out var date) ? date : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Sets the current URL, updates alias and stale flag, and optionally persists.
    /// Also pushes the URL to the top of the recent list.
    /// </summary>
    private void ApplyUrl(string url, bool persist)
    {
        CurrentAuctionUrl = url;
        CurrentAuctionAlias = BuildAlias(url);

        var auctionDate = ParseAuctionDate(url);
        IsAuctionDateStale = auctionDate.HasValue && auctionDate.Value.Date < DateTime.Today;

        if (persist)
        {
            // Push to top of recent list, remove duplicate if already present, cap at max
            _realAuctionSettings.RecentUrls.Remove(url);
            _realAuctionSettings.RecentUrls.Insert(0, url);
            if (_realAuctionSettings.RecentUrls.Count > MaxRecentUrls)
                _realAuctionSettings.RecentUrls.RemoveAt(_realAuctionSettings.RecentUrls.Count - 1);

            _realAuctionSettings.Url = url;
            _realAuctionSettings.Save();

            // Sync observable collection
            RecentAuctionUrls.Clear();
            foreach (var u in _realAuctionSettings.RecentUrls)
                RecentAuctionUrls.Add(new AuctionUrlEntry(u, BuildAlias(u)));
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

        ApplyUrl(selectedUrl, persist: true);
        Status = "Auction URL updated. Ready to load.";
    }

    [RelayCommand]
    private void SelectRecentUrl(AuctionUrlEntry entry)
    {
        ApplyUrl(entry.Url, persist: true);
        Status = $"Switched to {entry.Alias}.";
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

/// <summary>A recent auction URL paired with its display alias.</summary>
public record AuctionUrlEntry(string Url, string Alias);