using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Export;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Core.Utilities;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using AstroValleyAssistant.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public abstract class PropertyScraperViewModelBase : ViewModelBase
    {

        protected IDialogService? _dialogService;
        protected IServiceProvider? _serviceProvider;
        protected IExporter<IEnumerable<PropertyRecord>, string>? _clipboardExporter;

        protected CancellationTokenSource? _cts;
        protected IRegridService? _regridService;
        protected IBrowserService? _browserService;

        // -----------------------------
        // Shared Commands
        // -----------------------------

        public ICommand CancelCommand => field ??= new RelayCommand(_ => CancelOperation(), _ => IsScraping);
        public ICommand ClearCommand => field ??= new RelayCommand(_ => Clear());
        public ICommand CopyRecordsToClipboardCommand => field ??= new AsyncRelayCommand(vm => CopyToClipboardAsync());
        public ICommand SelectMatchCommand => field ??= new AsyncRelayCommand(match => ScrapeMatch((RegridMatch)match));
        public ICommand LoadRegridDataCommand => field ??= new RelayCommand(async _ => await EnrichWithRegridAsync(), _ => PropertyRecords.Count > 0 && !IsScraping);
        public ICommand MapCommand => field ??= new AsyncRelayCommand(_ => ViewInMap(), _ => PropertyRecords.Count > 0 && !IsScraping && IsRegridDataLoaded);
        public ICommand ScrapeCommand => field ??= new RelayCommand(mode => SetScrapeMode((RegridScrapeMode)mode));

        //protected abstract Task ScrapeAsync();

        // -----------------------------
        // UI State
        // -----------------------------

        public string? Status
        {
            get => field;
            set => Set(ref field, value);
        }

        public bool IsScraping
        {
            get => field;
            set => Set(ref field, value);
        }

        public bool IsRegridDataLoaded
        {
            get => field;
            set => Set(ref field, value);
        }

        public bool IsScrapeVisible
        {
            get => field;
            set => Set(ref field, value);
        }

        public bool IsResultButtonsVisible
        {
            get => field;
            set => Set(ref field, value);
        }

        public RegridScrapeMode ScrapeMode
        {
            get => field;
            set => Set(ref field, value);
        } = RegridScrapeMode.ParcelId; // Initializer for the backing field

        public PropertyDataViewModel? PropertySelected
        {
            get => field;
            set
            {
                if (field != value)
                    Set(ref field, value);
            }
        }

        // Shared collection
        public ObservableCollection<PropertyDataViewModel> PropertyRecords { get; } = [];

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
                    await ClipboardFormatter.ExportToExcelWithImagesAsync(PropertyRecords.Select(vm => vm.Record), saveDialog.FileName);
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

        /// <summary>
        /// Orchestrates the transformation and export of property records to the clipboard.
        /// </summary>
        private async Task CopyToClipboardAsync()
        {
            try
            {
                // Extract the underlying Model from your ViewModels
                var records = PropertyRecords.Select(pvm => pvm.Record);

                // Execute the export using the injected service
                await _clipboardExporter.ExportAsync(records, null);

                Status = "All records copied to clipboard.";
            }
            catch (Exception ex)
            {
                Status = $"Export failed: {ex.Message}";
            }
        }

        // -----------------------------
        // Regrid Scraping
        // -----------------------------

        protected async Task EnrichWithRegridAsync()
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

                //            string query = ScrapeMode == RegridScrapeMode.ParcelId
                //                ? vm.ParcelId
                //                : vm.Address;

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

        protected async Task ScrapeMatch(RegridMatch match)
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
                var result = await _regridService.ScrapeSingleAsync(match.FullUrl, ct);

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

        private void SetScrapeMode(RegridScrapeMode mode)
        {
            ScrapeMode = mode;
            Status = $"Scrape mode set to {mode}";
            _ = EnrichWithRegridAsync();
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        protected void BeginOperation(string message)
        {
            CancelOperation();
            _cts = new CancellationTokenSource();
            IsScraping = true;
            Status = message;
        }

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

        protected async Task ViewInMap()
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<MarkerMapViewModel>();

                // Opens the county map dialog for a given state.
                _dialogService.ShowDialog(vm);

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
            catch (Exception ex)
            {

                throw;
            }
        }

        protected void SetIdle(string message)
        {
            IsScraping = false;
            Status = message;
        }

        // -----------------------------
        // Shared Clear Logic
        // -----------------------------
        private void Clear()
        {
            PropertyRecords.Clear();
            Status = string.Empty;
            IsRegridDataLoaded = false;
            IsScrapeVisible = true;
            IsResultButtonsVisible = false;
        }
    }
}
