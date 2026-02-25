using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Export;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    /// <summary>
    /// Singleton ViewModel responsible for managing marker data, parsing inputs, 
    /// and orchestrating map exports.
    /// </summary>
    public class MarkerMapViewModel : ViewModelDialogBase
    {
        private readonly IMarkerMapParserService _parserService;
        private readonly IExporter<IEnumerable<MarkerLocation>, string> _htmlExporter;

        private string? _status;

        public MarkerMapViewModel(IMarkerMapParserService parserService, IExporter<IEnumerable<MarkerLocation>, string> htmlExporter)
        {
            _parserService = parserService;
            _htmlExporter = htmlExporter;

            Markers = new ObservableCollection<MarkerLocation>();
        }

        /// <summary>
        /// The source of truth for all markers currently on the map.
        /// </summary>
        public ObservableCollection<MarkerLocation> Markers { get; }

        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        #region Commands

        private AsyncRelayCommand? _importFileCommand;
        public ICommand ImportFileCommand => _importFileCommand ??= new AsyncRelayCommand(vm => ExecuteImportFileAsync());

        private AsyncRelayCommand? _exportHtmlCommand;
        public ICommand ExportHtmlCommand => _exportHtmlCommand ??= new AsyncRelayCommand(vm => ExecuteExportHtmlAsync());

        private ICommand? _resetCommand;
        public ICommand ResetCommand => _resetCommand ??= new RelayCommand(vm => ExecuteReset());

        #endregion

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
            ExecuteReset();
            AddLocations(locations);
        }

        private async Task ExecuteImportFileAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Map Data (*.csv;*.xlsx)|*.csv;*.xlsx|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var imported = await _parserService.ParseFileAsync(openFileDialog.FileName);
                    AddLocations(imported);
                }
                catch (Exception ex)
                {
                    Status = $"Import error: {ex.Message}";
                }
            }
        }

        private async Task ExecuteExportHtmlAsync()
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
                    await _htmlExporter.ExportAsync(Markers, saveFileDialog.FileName);
                    Status = "Map exported successfully.";
                }
                catch (Exception ex)
                {
                    Status = $"Export error: {ex.Message}";
                }
            }
        }

        private void ExecuteReset()
        {
            Markers.Clear();
            Status = "Map cleared.";
        }
    }
}
