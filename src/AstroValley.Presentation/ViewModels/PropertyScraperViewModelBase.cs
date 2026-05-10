using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Enums;
using AstroValley.Domain.Models;
using AstroValley.Domain.Utilities;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels;

public abstract partial class PropertyScraperViewModelBase : ObservableObject
{
    private bool _isDialogOpen = false;

    protected IDialogService? _dialogService;
    protected IServiceProvider? _serviceProvider;
    protected IExporter<IEnumerable<PropertyRecord>, string>? _clipboardExporter;
    protected IExporter<IEnumerable<PropertyRecord>, string?>? _excelExporter;
    protected CancellationTokenSource? _cts;
    protected IRegridService? _regridService;
    protected IBrowserService? _browserService;

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnrichWithRegridCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewInMapCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
    public partial bool IsScraping { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ViewInMapCommand))]
    public partial bool IsRegridDataLoaded { get; set; }

    [ObservableProperty]
    public partial bool IsScrapeVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsResultButtonsVisible { get; set; }

    [ObservableProperty]
    public partial RegridScrapeMode ScrapeMode { get; set; } = RegridScrapeMode.ParcelId;

    [ObservableProperty]
    public partial PropertyDataViewModel? PropertySelected { get; set; }

    public ObservableCollection<PropertyDataViewModel> PropertyRecords { get; } = [];

    // ✅ Called when PropertyRecords collection items are added/removed
    protected void OnPropertyRecordsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        EnrichWithRegridCommand.NotifyCanExecuteChanged();
        ViewInMapCommand.NotifyCanExecuteChanged();
    }

    private async void ExportData()
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "PropertyExport.xlsx"
        };

        if (saveDialog.ShowDialog() == true)
        {
            IsScraping = true;
            Status = "Generating Excel file with images...";
            try
            {
                await _excelExporter!.ExportAsync(PropertyRecords.Select(vm => vm.Record), saveDialog.FileName);
                Status = "Export successful!";
            }
            catch (Exception ex)
            {
                Status = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsScraping = false;
            }
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        try
        {
            // Extract the underlying Model from your ViewModels
            var records = PropertyRecords.Select(pvm => pvm.Record);

            // Execute the export using the injected service
            await _clipboardExporter!.ExportAsync(records, null);

            Status = "All records copied to clipboard.";
        }
        catch (Exception ex)
        {
            Status = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadRegridData))]
    protected async Task EnrichWithRegrid()
    {
        if (PropertyRecords.Count == 0)
        {
            Status = "No properties to scrape.";
            return;
        }

        BeginOperation("Begin Regrid Scraping...");

        try
        {
            var ct = _cts!.Token;

            // 1. Build list of parcel queries
            var queries = PropertyRecords
                .Select(vm => ScrapeMode == RegridScrapeMode.ParcelId ? vm.ParcelId : vm.Address)
                .ToList();

            var progress = new Progress<int>(count =>
            {
                Status = $"Processed {count} of {PropertyRecords.Count}";
            });

            int index = 0;

            foreach (var query in queries)
            {
                ct.ThrowIfCancellationRequested();

                var vm = PropertyRecords[index];

                // 2. Show "Loading" BEFORE scraping begins
                vm.Status = ScrapeStatus.Loading;
                vm.Matches.Clear();
                vm.HasMultipleMatches = false;

                // 3. Scrape a single parcel
                var result = await _regridService!.ScrapeSingleAsync(query, ct);

                // 4. Apply result to the row
                ApplyRegridResult(vm, result);

                // 5. Update progress text
                ((IProgress<int>)progress).Report(index + 1);

                // 6. External throttling between parcels
                await Task.Delay(500, ct);

                index++;
            }

            IsRegridDataLoaded = true;
            SetIdle($"Regrid complete. Processed {PropertyRecords.Count} properties.");
        }
        catch (OperationCanceledException)
        {
            SetIdle("Operation canceled.");
        }
        catch (Exception ex)
        {
            SetIdle($"Error: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanScrapeMatch))]
    public async Task ScrapeMatch(RegridMatch match)
    {
        if (PropertySelected == null)
            return;

        BeginOperation("Scraping selected Regrid match...");

        try
        {
            var ct = _cts!.Token;

            // 1. Show loading state on the selected row
            PropertySelected.Status = ScrapeStatus.Loading;
            PropertySelected.Matches.Clear();
            PropertySelected.HasMultipleMatches = false;

            // 2. Scrape using the final parcel URL
            var result = await _regridService!.ScrapeSingleAsync(match.FullUrl, ct);

            // 3. Apply the result to the selected row
            ApplyRegridResult(PropertySelected, result);

            // 4. Mark sidebar as resolved
            PropertySelected.Matches.Clear();
            PropertySelected.HasMultipleMatches = false;

            SetIdle("Match scraping complete.");
        }
        catch (OperationCanceledException)
        {
            SetIdle("Operation canceled.");
        }
        catch (Exception ex)
        {
            SetIdle($"Error: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanViewMatches))]
    private async Task ViewMatches(PropertyDataViewModel property)
    {
        if (property == null || !property.HasMultipleMatches || _isDialogOpen)
            return;

        _isDialogOpen = true;  // Mark dialog as open

        try
        {
            // Create the dialog ViewModel with original property info
            var dialogVm = new ResolveMatchesViewModel(
                property.ParcelId,
                property.Address,
                property.Owner,
                property.Matches);

            // Show the dialog
            _dialogService!.ShowDialog(dialogVm);

            // Await the result
            try
            {
                var selectedMatch = await dialogVm.Result;

                // If user selected a match (not cancelled), scrape it
                if (selectedMatch != null)
                {
                    await ScrapeMatch(selectedMatch);
                }
                // If cancelled (selectedMatch == null), do nothing - banner stays visible
            }
            catch (TaskCanceledException)
            {
                // Dialog was closed without selection - do nothing, banner stays visible
            }
        }
        finally
        {
            _isDialogOpen = false;  // Always reset state
        }
    }


    [RelayCommand]
    private void SetScrapeMode(RegridScrapeMode mode)
    {
        ScrapeMode = mode;
        Status = $"Scrape mode set to {mode}";
        _ = EnrichWithRegrid();
    }

    [RelayCommand(CanExecute = nameof(CanCancelOperation))]
    protected void CancelOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            try
            {
                // Trigger cancellation
                _cts?.Cancel();

                // Update UI immediately
                Status = "Canceling...";
            }
            catch { } // No-op: cancellation is best-effort
        }
    }

    [RelayCommand(CanExecute = nameof(CanMap))]
    protected async Task ViewInMap()
    {
        try
        {
            var vm = _serviceProvider!.GetRequiredService<MarkerMapViewModel>();

            // Opens the county map dialog for a given state.
            _dialogService!.ShowDialog(vm);

            // Add short delay then load data
            await Task.Delay(300);

            var mapLocations = new List<MarkerLocation>();
            foreach (var record in PropertyRecords)
            {
                var details = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(record.FloodZone)) details.Add("Flood Zone", record.FloodZone);
                if (!string.IsNullOrEmpty(record.Owner)) details.Add("Owner", record.Owner);
                if (!string.IsNullOrEmpty(record.AssessedValue?.ToString() ?? "")) details.Add("Assessed", record.AssessedValue?.ToString() ?? "");
                if (!string.IsNullOrEmpty(record.Bid?.ToString() ?? "")) details.Add("Minimum Bid", record.Bid?.ToString() ?? "");

                if (!string.IsNullOrWhiteSpace(record.Latitude) && !string.IsNullOrEmpty(record.Longitude))
                    mapLocations.Add(new MarkerLocation
                    {
                        ParcelID = record.ParcelId,
                        Address = record.Address ?? record.Owner,
                        Acres = record.Acres?.ToString() ?? null,
                        Latitude = (double)record.Latitude.TryParseDouble(),
                        Longitude = (double)record.Longitude.TryParseDouble(),
                        ParcelLines = record.ParcelLines,
                        ExtraDetails = details
                    });
            }

            vm.AddLocations(mapLocations);
        }
        catch (Exception)
        {
            throw;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _isDialogOpen = false;
        PropertyRecords.Clear();
        Status = string.Empty;
        IsRegridDataLoaded = false;
        IsScrapeVisible = true;
        IsResultButtonsVisible = false;
    }

    private bool CanCancelOperation => IsScraping;
    private bool CanMap => PropertyRecords.Count > 0 && !IsScraping && IsRegridDataLoaded;
    private bool CanLoadRegridData => PropertyRecords.Count > 0 && !IsScraping;
    private bool CanScrapeMatch(RegridMatch? match) => match != null && PropertySelected != null;
    private bool CanViewMatches(PropertyDataViewModel? property) => property != null && property.HasMultipleMatches && !_isDialogOpen;

    protected void ApplyRegridResult(PropertyDataViewModel vm, RegridParcelResult result)
    {
        vm.Matches.Clear();
        vm.HasMultipleMatches = false;

        // Error case
        if (result.Error != null)
        {
            vm.Status = ScrapeStatus.Error;
            return;
        }

        // Not Found case
        if (result.NotFound)
        {
            // If scraper provided a record with a RegridUrl (search URL), merge just that
            if (!string.IsNullOrWhiteSpace(result.Record?.RegridUrl))
            {
                var existing = vm.Record ?? new PropertyRecord();
                vm.Record = existing with { RegridUrl = result.Record.RegridUrl };
            }

            vm.Status = ScrapeStatus.NotFound;
            return;
        }

        // Multiple Matches case
        if (result.IsMultiple)
        {
            foreach (var match in result.Matches)
                vm.Matches.Add(match);

            vm.HasMultipleMatches = true;

            // If scraper provided a record with a RegridUrl (search URL), merge just that
            if (!string.IsNullOrWhiteSpace(result.Record?.RegridUrl))
            {
                var existing = vm.Record ?? new PropertyRecord();
                vm.Record = existing with { RegridUrl = result.Record.RegridUrl };
            }

            vm.Status = ScrapeStatus.MultipleMatches;
            return;
        }

        // Success case: full merge
        vm.Record = PropertyRecordMerger.Merge(vm.Record, result.Record!);
        vm.Status = ScrapeStatus.Success;
    }

    protected void BeginOperation(string message)
    {
        CancelOperation();
        _cts = new CancellationTokenSource();
        IsScraping = true;
        Status = message;
    }

    protected void SetIdle(string message)
    {
        IsScraping = false;
        Status = message;
    }

    // partial void consumed by the generator — forwards to virtual
    partial void OnIsScrapingChanged(bool value) => OnIsScrapingChangedCore(value);

    // virtual — safe to override in derived classes
    protected virtual void OnIsScrapingChangedCore(bool value) { }
}
