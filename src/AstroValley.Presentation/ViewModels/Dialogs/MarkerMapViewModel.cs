using AstroValley.Application.Interfaces.Export;
using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// Singleton ViewModel responsible for managing marker data, parsing inputs, 
/// and orchestrating map exports.
/// </summary>
public partial class MarkerMapViewModel : DialogViewModelBase<bool>
{
    public override string Title => "Marker Map";

    private readonly IMarkerMapParserService? _parserService;
    private readonly IExporter<IEnumerable<MarkerLocation>, string>? _htmlExporter;

    public ObservableCollection<MarkerLocation> Markers { get; }

    public string? Status
    {
        get;
        set => SetProperty(ref field, value);
    }

    public MarkerMapViewModel(IMarkerMapParserService parserService, IHtmlMapExporter htmlExporter)
    {
        _parserService = parserService;
        _htmlExporter = htmlExporter;
        Markers = [];
    }

    /// <summary>
    /// Adds a list of locations to the existing map without clearing current data.
    /// </summary>
    public void AddLocations(IEnumerable<MarkerLocation> locations)
    {
        foreach (var loc in locations.Where(l => l.IsValid))
        {
            Markers.Add(loc);
        }
        Status = $"Added {locations.Count()} locations to the map.";
    }

    /// <summary>
    /// Clears the current map and starts fresh with new data.
    /// </summary>
    public void ResetWithLocations(IEnumerable<MarkerLocation> locations)
    {
        Reset();
        AddLocations(locations);
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Map Data (*.csv;*.xlsx)|*.csv;*.xlsx|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var imported = await _parserService!.ParseFileAsync(openFileDialog.FileName);
                AddLocations(imported);
            }
            catch (Exception ex)
            {
                Status = $"Import error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        if (!Markers.Any())
        {
            Status = "No markers to export.";
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "HTML File (*.html)|*.html",
            FileName = "MyMarkerMap.html"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                await _htmlExporter!.ExportAsync(Markers, saveFileDialog.FileName);
                Status = "Map exported successfully.";
            }
            catch (Exception ex)
            {
                Status = $"Export error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Markers.Clear();
        Status = "Map cleared.";
    }
}