using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Domain.Entities;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Web;

namespace AstroValley.Presentation.ViewModels.Sources;

/// <summary>
/// Where Parcel Lookup gets its list from a RealAuction tax deed sale: the current auction URL, the recent ones, and loading the list.
/// The page owns busy state and cancellation; this class only knows about auctions.
/// </summary>
public sealed partial class RealAuctionSourceViewModel : ObservableObject
{
    private const int MaxRecentUrls = 10;

    private readonly IRealTaxDeedClient _client;
    private readonly IRealAuctionSettings _settings;
    private readonly IBrowserService _browser;
    private readonly IDialogService _dialogs;

    public RealAuctionSourceViewModel(IRealTaxDeedClient client, IRealAuctionSettings settings, IBrowserService browser, IDialogService dialogs)
    {
        _client = client;
        _settings = settings;
        _browser = browser;
        _dialogs = dialogs;
        RecentUrls.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentUrls));

        foreach (var url in _settings.RecentUrls)
            RecentUrls.Add(NewEntry(url));

        if (!string.IsNullOrWhiteSpace(_settings.Url))
            ApplyUrl(_settings.Url, persist: false);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad))]
    private string? url;

    [ObservableProperty]
    private string? alias;

    /// <summary>True when the AUCTIONDATE in the URL is before today: that sale is over, so loading it is pointless.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad))]
    private bool isStale;

    public bool CanLoad => Url is not null && !IsStale;

    public ObservableCollection<AuctionUrlEntry> RecentUrls { get; } = [];

    public bool HasRecentUrls => RecentUrls.Count > 0;

    /// <summary>Downloads the auction's property list and remembers how many it had.</summary>
    public async Task<List<PropertyRecord>> LoadAsync(CancellationToken ct)
    {
        var records = await _client.GetAuctionRecordsAsync(Url!, ct);
        SaveRecentCount(Url!, records.Count);
        return records;
    }

    /// <summary>Makes a recent auction the current one.</summary>
    public void Select(AuctionUrlEntry entry) => ApplyUrl(entry.Url, persist: true);

    [RelayCommand]
    private void Remove(AuctionUrlEntry entry)
    {
        _settings.RecentUrls.Remove(entry.Url);
        _settings.RecentCounts.Remove(entry.Url);
        _settings.Save();
        RecentUrls.Remove(entry);
    }

    /// <summary>Lets the user browse RealAuction to an auction date and makes that auction the current one; false when cancelled.</summary>
    public async Task<bool> BrowseAsync()
    {
        var picked = await _dialogs.ShowAsync(new BrowseForUrlDialogViewModel(
            "Find an auction on RealAuction", ClientsPageUrl, IsAuctionUrl,
            "Open a county's auction calendar and click an auction date; Select turns on once its property list is showing."));

        if (picked is null) return false;
        ApplyUrl(picked, persist: true);
        return true;
    }

    /// <summary>"marion.realtaxdeed.com/..." -> "Marion County"; null when it is not a URL. Auction rows have no county of their own.</summary>
    // shortcut: a multi-word county shows as one word ("Miami-dade County"), as in the auction alias
    public static string? CountyFrom(string? auctionUrl) =>
        Uri.TryCreate(auctionUrl, UriKind.Absolute, out var u) && u.Host.Split('.')[0] is { Length: > 0 } h ? char.ToUpper(h[0]) + h[1..] + " County" : null;

    /// <summary>A realtaxdeed.com / realforeclose.com auction page for one date (the only kind LoadAsync can read).</summary>
    public static bool IsAuctionUrl(string candidate) => AuctionUrlPattern.IsMatch(candidate);

    private const string ClientsPageUrl = "https://www.realauction.com/clients";

    private static readonly Regex AuctionUrlPattern = new(
        @"^https?://[^.]+\.real(taxdeed|foreclose)\.com/.*[?&]zaction=AUCTION.*AUCTIONDATE=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Opens a recent auction's page on RealAuction in the user's browser.</summary>
    [RelayCommand]
    private void OpenSite(AuctionUrlEntry entry) => _browser.Launch(entry.Url);

    private void ApplyUrl(string newUrl, bool persist)
    {
        Url = newUrl;
        var date = ParseAuctionDate(newUrl);
        Alias = BuildAlias(newUrl, date);
        IsStale = date?.Date < DateTime.Today;

        if (!persist) return;

        // most recent first, no duplicates, capped
        _settings.RecentUrls.Remove(newUrl);
        _settings.RecentUrls.Insert(0, newUrl);
        if (_settings.RecentUrls.Count > MaxRecentUrls)
        {
            _settings.RecentCounts.Remove(_settings.RecentUrls[^1]);
            _settings.RecentUrls.RemoveAt(_settings.RecentUrls.Count - 1);
        }
        _settings.Url = newUrl;
        _settings.Save();

        RecentUrls.Clear();
        foreach (var u in _settings.RecentUrls)
            RecentUrls.Add(NewEntry(u));
    }

    private void SaveRecentCount(string auctionUrl, int count)
    {
        _settings.RecentCounts[auctionUrl] = count;
        _settings.Save();

        var i = RecentUrls.ToList().FindIndex(e => e.Url == auctionUrl);
        if (i >= 0) RecentUrls[i] = NewEntry(auctionUrl);
    }

    private AuctionUrlEntry NewEntry(string entryUrl) =>
        new(entryUrl, BuildAlias(entryUrl, ParseAuctionDate(entryUrl)), _settings.RecentCounts.TryGetValue(entryUrl, out var count) ? count : null);

    /// <summary>"County - M/d/yy" from a realtaxdeed.com URL; the raw URL when it cannot be parsed.</summary>
    private static string BuildAlias(string auctionUrl, DateTime? date)
    {
        if (!Uri.TryCreate(auctionUrl, UriKind.Absolute, out var uri)) return auctionUrl;

        var county = uri.Host.Split('.')[0];
        county = county.Length > 0 ? char.ToUpper(county[0]) + county[1..] : auctionUrl;
        return date is { } d ? $"{county} - {d:M/d/yy}" : county;
    }

    private static DateTime? ParseAuctionDate(string auctionUrl) =>
        Uri.TryCreate(auctionUrl, UriKind.Absolute, out var uri)
        && DateTime.TryParse(HttpUtility.ParseQueryString(uri.Query)["AUCTIONDATE"], out var date) ? date : null;
}
