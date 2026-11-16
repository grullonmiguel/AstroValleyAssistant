using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Domain.Enums;
using AstroValley.Domain.Models;
using AstroValley.Presentation.Models;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels.Dialogs;
using AstroValley.Presentation.ViewModels.Sources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;

namespace AstroValley.Presentation.ViewModels.Pages;

/// <summary>
/// Takes a list of parcels (IDs or addresses) and looks each one up in a parcel data provider to fill in as much as possible.
/// Replaces the RealAuction and Regrid pages once everything has been moved over.
/// </summary>
public sealed partial class ParcelLookupViewModel : PageViewModelBase
{
    // Pause between lookups so the provider does not rate-limit us.
    private static readonly TimeSpan DelayBetweenLookups = TimeSpan.FromMilliseconds(500);

    private readonly IBrowserService _browser;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IFileService _files;
    private readonly IClipboardExporter _clipboardExporter;
    private readonly IExcelExporter _excelExporter;
    private CancellationTokenSource? _cts;

    /// <summary>The auction the rows came from; stored on each map pin.</summary>
    private string? _loadedAuctionUrl;

    /// <summary>"Bay - 11/17/26" for the auction the rows came from (shown over the grid); null when the list did not come from an auction.</summary>
    [ObservableProperty]
    private string? loadedAuctionAlias;

    /// <summary>The grid header's auction name: open that auction on RealAuction.</summary>
    [RelayCommand]
    private void OpenLoadedAuction() => _browser.Launch(_loadedAuctionUrl);

    public ParcelLookupViewModel(RealAuctionSourceViewModel auction, IEnumerable<IParcelDataProvider> providers,
        IBrowserService browser, ISnackbarService snackbar, IDialogService dialogs, INavigationService navigation,
        IFileService files, IClipboardExporter clipboardExporter, IExcelExporter excelExporter)
    {
        _browser = browser;
        _snackbar = snackbar;
        _dialogs = dialogs;
        _navigation = navigation;
        _files = files;
        _clipboardExporter = clipboardExporter;
        _excelExporter = excelExporter;
        Auction = auction;
        Providers = providers.ToList();
        selectedProvider = Providers.FirstOrDefault();

        Auction.PropertyChanged += (_, _) => LoadAuctionCommand.NotifyCanExecuteChanged();
        Rows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRows));
            LookupCommand.NotifyCanExecuteChanged();
            NotifyListCommands();
            ToggleGetDetailsCommand.NotifyCanExecuteChanged();
            if (!HasLookupTargets && IsGetDetailsOpen) CloseFlyout();
        };
    }

    private void NotifyListCommands()
    {
        LookupWithCommand.NotifyCanExecuteChanged();
        BrowseRealAuctionCommand.NotifyCanExecuteChanged();
        LoadRecentAuctionCommand.NotifyCanExecuteChanged();
        ShowOnMapCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        ExportToExcelCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyListCommands();

    public override string Title => "Parcel Lookup";

    /// <summary>The list source: a RealAuction sale. Other sources sit next to it later.</summary>
    public RealAuctionSourceViewModel Auction { get; }

    /// <summary>Every registered parcel data provider (Regrid today).</summary>
    public IReadOnlyList<IParcelDataProvider> Providers { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LookupCommand))]
    private IParcelDataProvider? selectedProvider;

    public ObservableCollection<PropertyDataViewModel> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

    /// <summary>The row whose details and links are shown beside the grid.</summary>
    [ObservableProperty]
    private PropertyDataViewModel? selectedRow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadAuctionCommand))]
    [NotifyCanExecuteChangedFor(nameof(LookupCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResolveMatchesCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    /// <summary>"Looking up 3 of 27" while a lookup runs; null otherwise (the progress bar is then indeterminate).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    private string? progressText;

    /// <summary>0-100 while a lookup runs.</summary>
    [ObservableProperty]
    private double progressValue;

    public bool IsProgressIndeterminate => ProgressText is null;

    [RelayCommand(CanExecute = nameof(CanLoadAuction))]
    private Task LoadAuction() => RunAsync("Loading", async ct =>
    {
        var records = await Auction.LoadAsync(ct);

        Rows.Clear();
        _loadedAuctionUrl = Auction.Url;
        LoadedAuctionAlias = Auction.Alias;
        var county = RealAuctionSourceViewModel.CountyFrom(Auction.Url);
        foreach (var record in records)
            Rows.Add(new PropertyDataViewModel(string.IsNullOrWhiteSpace(record.County) && county is not null ? record with { County = county } : record, _browser));

        _snackbar.Show($"Loaded {Rows.Count} properties from {Auction.Alias}.", SnackbarSeverity.Success);
    });

    private bool CanLoadAuction() => !IsBusy && Auction.CanLoad;

    // ----- Header flyouts (Lists, Add Parcels, Get Details): one open at a time -----

    /// <summary>Which header flyout is showing, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListsOpen), nameof(IsAddParcelsOpen), nameof(IsGetDetailsOpen))]
    private ParcelLookupFlyout openFlyout;

    public bool IsListsOpen => OpenFlyout == ParcelLookupFlyout.Lists;
    public bool IsAddParcelsOpen => OpenFlyout == ParcelLookupFlyout.AddParcels;
    public bool IsGetDetailsOpen => OpenFlyout == ParcelLookupFlyout.GetDetails;

    private void Toggle(ParcelLookupFlyout flyout) => OpenFlyout = OpenFlyout == flyout ? ParcelLookupFlyout.None : flyout;

    [RelayCommand]
    private void ToggleLists() => Toggle(ParcelLookupFlyout.Lists);

    [RelayCommand]
    private void ToggleAddParcels() => Toggle(ParcelLookupFlyout.AddParcels);

    [RelayCommand(CanExecute = nameof(HasLookupTargets))]
    private void ToggleGetDetails() => Toggle(ParcelLookupFlyout.GetDetails);

    [RelayCommand]
    private void CloseFlyout() => OpenFlyout = ParcelLookupFlyout.None;

    // ----- Get Details flyout: pick a data provider and the lookup runs straight away -----

    /// <summary>At least one row has something a provider can search by (a parcel ID or an address).</summary>
    public bool HasLookupTargets => Rows.Any(HasLookupTarget);

    private static bool HasLookupTarget(PropertyDataViewModel row) =>
        !string.IsNullOrWhiteSpace(row.ParcelId) || !string.IsNullOrWhiteSpace(row.Address);

    /// <summary>A provider in the flyout: use it, and look up the list now.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task LookupWith(IParcelDataProvider provider)
    {
        CloseFlyout();
        SelectedProvider = provider;
        return CanLookup() ? Lookup() : Task.CompletedTask;
    }

    /// <summary>The flyout's Real Auction button: browse to an auction, then load it.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BrowseRealAuction()
    {
        CloseFlyout();
        if (await Auction.BrowseAsync())
            await LoadCurrentAuction();
    }

    /// <summary>A recent auction in the flyout: make it current, then load it.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task LoadRecentAuction(AuctionUrlEntry entry)
    {
        CloseFlyout();
        Auction.Select(entry);
        return LoadCurrentAuction();
    }

    private Task LoadCurrentAuction()
    {
        if (Auction.CanLoad) return LoadAuction();
        _snackbar.Show($"{Auction.Alias} has already taken place, so there is nothing to load.", SnackbarSeverity.Warning);
        return Task.CompletedTask;
    }

    private bool IsIdle() => !IsBusy;

    /// <summary>Looks up every row that has not been found yet (so a second run retries not-found, failed and rate-limited rows).</summary>
    [RelayCommand(CanExecute = nameof(CanLookup))]
    private Task Lookup() => RunAsync("Lookup", async ct =>
    {
        var provider = SelectedProvider!;
        var todo = Rows.Where(r => r.Status != ScrapeStatus.Success && HasLookupTarget(r)).ToList();
        if (todo.Count == 0)
        {
            _snackbar.Show("Every parcel has already been found.", SnackbarSeverity.Info);
            return;
        }

        for (var i = 0; i < todo.Count; i++)
        {
            ProgressText = $"Looking up {i + 1} of {todo.Count} in {provider.Name}";
            ProgressValue = i * 100.0 / todo.Count;

            var row = todo[i];
            row.Status = ScrapeStatus.Loading;
            // Parcel ID when there is one, otherwise the address (imported lists can have either).
            var query = string.IsNullOrWhiteSpace(row.ParcelId) ? row.Address : row.ParcelId;
            var result = await provider.LookupAsync(query, ct);
            ct.ThrowIfCancellationRequested(); // providers report a canceled request as an error; this row was not really tried
            row.ApplyLookupResult(result);

            if (result.IsRateLimited)
            {
                // Every further request would be refused too; stop and let the user run it again later.
                var wait = result.RetryAfterSeconds is { } s ? $" Try again in about {Math.Ceiling(s / 60.0)} min." : " Try again in a few minutes.";
                _snackbar.Show($"{provider.Name} is limiting requests, so the lookup stopped at {i + 1} of {todo.Count}.{wait}", SnackbarSeverity.Warning);
                return;
            }

            await Task.Delay(DelayBetweenLookups, ct);
        }

        var found = todo.Count(r => r.Status == ScrapeStatus.Success);
        var problems = todo.Count - found;
        _snackbar.Show(problems == 0
                ? $"Found all {found} parcels in {provider.Name}."
                : $"Found {found} of {todo.Count} parcels in {provider.Name}; {problems} need a look (not found, several matches or failed).",
            problems == 0 ? SnackbarSeverity.Success : SnackbarSeverity.Warning);
    });

    private bool CanLookup() => !IsBusy && HasRows && SelectedProvider is not null;

    /// <summary>A row with several matches: the user picks one in a dialog and that parcel is looked up.</summary>
    [RelayCommand(CanExecute = nameof(CanResolveMatches))]
    private async Task ResolveMatches(PropertyDataViewModel? row)
    {
        var match = await _dialogs.ShowAsync(new ResolveMatchesDialogViewModel(row!.ParcelId, row.Address, row.Owner, row.Matches));
        if (match is null) return;

        await RunAsync("Lookup", async ct =>
        {
            // shortcut: a match's URL only means something to the provider that found it (Regrid); keep the matches' provider on the row once there are two.
            var provider = SelectedProvider!;
            row.Status = ScrapeStatus.Loading;
            var result = await provider.LookupAsync(match.FullUrl, ct);
            ct.ThrowIfCancellationRequested();
            row.ApplyLookupResult(result);

            if (row.Status == ScrapeStatus.Success)
                _snackbar.Show($"Found {row.ParcelId} in {provider.Name}.", SnackbarSeverity.Success);
            else
                _snackbar.Show($"{provider.Name} could not read that parcel; try again or pick another match.", SnackbarSeverity.Warning);
        });
    }

    private bool CanResolveMatches(PropertyDataViewModel? row) => !IsBusy && row is { HasMultipleMatches: true };

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cts?.Cancel();

    // ----- What to do with the list -----

    /// <summary>Puts every parcel that has a location on the Home map (found ones; auction rows have none until looked up) and shows the map.</summary>
    [RelayCommand(CanExecute = nameof(CanUseList))]
    private void ShowOnMap()
    {
        var pins = Rows.Select(r => MarkerLocation.FromRecord(r.Record, _loadedAuctionUrl)).OfType<MarkerLocation>().ToList();
        if (pins.Count == 0)
        {
            _snackbar.Show("None of these parcels has a location yet. Look them up first.", SnackbarSeverity.Warning);
            return;
        }

        var added = _navigation.NavigateTo<HomeViewModel>().AddMarkers(pins);
        var missing = Rows.Count - pins.Count;
        static string Pins(int n) => n == 1 ? "1 pin" : $"{n} pins";
        static string Parcels(int n) => n == 1 ? "1 parcel has" : $"{n} parcels have";
        _snackbar.Show((added == pins.Count ? $"Added {Pins(added)} to the map." : $"Added {Pins(added)} to the map ({pins.Count - added} already there).")
            + (missing > 0 ? $" {Parcels(missing)} no location yet." : ""), SnackbarSeverity.Success);
    }

    [RelayCommand(CanExecute = nameof(CanUseList))]
    private Task Copy() => RunAsync("Copy", async _ =>
    {
        await _clipboardExporter.ExportAsync(Rows.Select(r => r.Record), null);
        _snackbar.Show($"Copied {Rows.Count} parcels to the clipboard.", SnackbarSeverity.Success);
    });

    [RelayCommand(CanExecute = nameof(CanUseList))]
    private Task ExportToExcel()
    {
        var path = _files.SaveFile("Export parcels to Excel", "Excel Files (*.xlsx)|*.xlsx", $"{Auction.Alias ?? "Parcels"}.xlsx".Replace('/', '-'));
        if (path is null) return Task.CompletedTask;

        return RunAsync("Export", async _ =>
        {
            await _excelExporter.ExportAsync(Rows.Select(r => r.Record), path);
            _snackbar.Show($"Exported {Rows.Count} parcels to {Path.GetFileName(path)}.", "OPEN", () => _browser.Launch(path), SnackbarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanUseList))]
    private void Clear()
    {
        Rows.Clear();
        SelectedRow = null;
        _loadedAuctionUrl = null;
        LoadedAuctionAlias = null;
    }

    private bool CanUseList() => !IsBusy && HasRows;

    /// <summary>Runs one long operation: busy state, cancellation, and a snackbar for cancel or failure.</summary>
    private async Task RunAsync(string name, Func<CancellationToken, Task> work)
    {
        using var cts = _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await work(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _snackbar.Show($"{name} canceled.", SnackbarSeverity.Warning);
        }
        catch (Exception ex)
        {
            _snackbar.Show($"{name} failed: {ex.Message}", SnackbarSeverity.Error);
        }
        finally
        {
            // a row cut off mid-lookup goes back to waiting
            foreach (var row in Rows.Where(r => r.Status == ScrapeStatus.Loading))
                row.Status = ScrapeStatus.Pending;

            _cts = null;
            ProgressText = null;
            IsBusy = false;
        }
    }
}

/// <summary>The header flyouts on Parcel Lookup; at most one is open.</summary>
public enum ParcelLookupFlyout
{
    None,
    Lists,
    AddParcels,
    GetDetails
}
