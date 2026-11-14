using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Import;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Enums;
using AstroValley.Infrastructure.Import;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace AstroValley.Presentation.ViewModels;

public partial class RegridViewModel : PropertyScraperViewModelBase
{
    private readonly IFileService _fileService;
    private readonly List<IPropertyImporter> _importers;

    public RegridViewModel(
        IRegridService regridService,
        IBrowserService browserService,
        IDialogService dialogService, 
        IServiceProvider serviceProvider,
        IClipboardExporter clipboardExporter,
        IExcelExporter excelExporter,
        IFileService fileService,
        IEnumerable<IPropertyImporter> importers)
    {
        _fileService = fileService;
        _importers = importers.ToList();
        _regridService = regridService;
        _browserService = browserService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _clipboardExporter = clipboardExporter;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// Generic clipboard processing logic to avoid code duplication.
    /// </summary>
    private void PasteFromClipboard(RegridScrapeMode mode)
    {
        if (!Clipboard.ContainsText()) return;

        var lines = Clipboard.GetText()
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        PropertyRecords.Clear();

        foreach (var line in lines)
        {
            // Initialize the record based on the selected mode
            var record = mode switch
            {
                RegridScrapeMode.ParcelId => new PropertyRecord { ParcelId = line },
                RegridScrapeMode.Address => new PropertyRecord { Address = line },
                _ => new PropertyRecord()
            };

            PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));
        }

        // Update state once
        ScrapeMode = mode;
        IsScrapeVisible = PropertyRecords.Count == 0;
        IsResultButtonsVisible = PropertyRecords.Count > 0;
    }

    /// <summary>Reads a .csv/.xlsx (dropped on the view or picked with Import) and adds its rows to the list; parcel IDs already listed are skipped.</summary>
    public async Task ImportFile(string path)
    {
        if (IsScraping) return; // BeginOperation would cancel the scrape in progress

        BeginOperation($"Reading {Path.GetFileName(path)}...");
        try
        {
            var (headers, rows) = await Task.Run(() => SpreadsheetReader.Read(path));
            var importer = _importers.First(i => i.CanRead(headers)); // the generic importer, registered last, accepts anything

            var listed = PropertyRecords.Select(p => p.ParcelId).Where(id => id != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var records = rows.Select(importer.Map).Where(r => r.ParcelId == "" || listed.Add(r.ParcelId)).ToList();

            foreach (var record in records)
                PropertyRecords.Add(new PropertyDataViewModel(record, _browserService));

            // Auto-detect scrape mode based on what the list contains
            var all = PropertyRecords.Select(p => p.Record).ToList();
            if (all.All(r => !string.IsNullOrWhiteSpace(r.ParcelId)))
                ScrapeMode = RegridScrapeMode.ParcelId;
            else if (all.All(r => !string.IsNullOrWhiteSpace(r.Address)))
                ScrapeMode = RegridScrapeMode.Address;

            IsScrapeVisible = PropertyRecords.Count == 0;
            IsResultButtonsVisible = PropertyRecords.Count > 0;
            var skipped = rows.Count - records.Count;
            SetIdle($"Added {records.Count} records from {importer.Name}" + (skipped > 0 ? $" ({skipped} already in the list)." : "."));
        }
        catch (Exception ex)
        {
            SetIdle($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void PasteParcels() => PasteFromClipboard(RegridScrapeMode.ParcelId);

    [RelayCommand]
    private void PasteAddresses() => PasteFromClipboard(RegridScrapeMode.Address);

    [RelayCommand]
    private async Task OpenImport()
    {
        var path = _fileService.OpenFile("Select Excel or CSV File", "Data Files (*.xlsx;*.csv)|*.xlsx;*.csv");
        if (path != null) await ImportFile(path);
    }
}