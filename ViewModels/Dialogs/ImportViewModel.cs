using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Extensions;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models.Domain;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    public class ImportViewModel : ViewModelDialogBase
    {
        private readonly IFileService _fileService;
        private readonly List<Dictionary<string, string>> _allRows = new();
        private static readonly Dictionary<string, string[]> ColumnAliases = new()
        {
            ["ParcelId"] = new[] { "parcel", "parcelid", "pid", "parcelnumber" },
            ["Address"] = new[] { "address", "siteaddress", "location", "street" , "parcellocation" },
            ["Owner"] = new[] { "owner", "ownername", "owner name" },
            ["Bid"] = new[] { "minimumbid", "minimunbid", "openingbid", "startingbid", "amountdue" },
            ["Assessed"] = new[] {"assessed", "assessedvalue", "appraised", "appraisedvalue" },
            ["Acres"] = new[] { "acres", "lotsize",  "acreage", "areaacres" }
        };

        public Action<List<PropertyRecord>>? OnImportCompleted { get; set; }

        // -----------------------------
        // Commands
        // -----------------------------

        #region Commands

        private ICommand? _browseCommand;
        public ICommand BrowseCommand => _browseCommand ??= new RelayCommand(_ => Browse(), _ => CanBrowse);

        private ICommand? _loadIntoGridCommand;
        public ICommand LoadIntoGridCommand => _loadIntoGridCommand ??= new RelayCommand(_ => LoadIntoGrid(), _ => CanLoadIntoGrid);

        private ICommand? _clearCommand;
        public ICommand ClearCommand => _clearCommand ??= new RelayCommand(_ => ClearDataGrid(), _ => CanClearGrid);

        // Optional: if you want to support drag-and-drop via command instead of code-behind
        private ICommand? _fileDroppedCommand;
        public ICommand FileDroppedCommand => _fileDroppedCommand ??= new RelayCommand(path => LoadFile(path as string));

        #endregion

        // -----------------------------
        // State
        // -----------------------------

        #region Properties

        private string? _selectedFilePath;
        public string? SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (Set(ref _selectedFilePath, value))
                    OnPropertyChanged(nameof(CanLoadIntoGrid));
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (Set(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set => Set(ref _isDragOver, value);
        }
        public bool IsEmpty => PreviewRows.Count == 0;

        // Columns detected from the file header
        public ObservableCollection<string> DetectedColumns { get; } = new();

        // Simple preview: each row is a dictionary of columnName -> value
        public ObservableCollection<Dictionary<string, string>> PreviewRows { get; } = new();

        public bool HasPreview => PreviewRows.Count > 0;

        public bool CanLoadIntoGrid => !IsBusy && PreviewRows.Count > 0 && !string.IsNullOrEmpty(SelectedFilePath);
        public bool CanClearGrid => !IsBusy && PreviewRows.Count > 0;
        public bool CanBrowse => !IsBusy && PreviewRows.Count <= 0;

        #endregion

        #region Constructor

        public ImportViewModel(IFileService fileService)
        {
            _fileService = fileService;
        }

        #endregion

        #region Methods

        // -----------------------------
        // Public API (called from View / code-behind)
        // -----------------------------

        public async void LoadFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            // Reset state
            ErrorMessage = null;
            SelectedFilePath = filePath;
            Status = $"Loading {Path.GetFileName(filePath)}...";
            IsBusy = true;

            DetectedColumns.Clear();
            PreviewRows.Clear();
            _allRows.Clear();

            try
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();

                // Offload heavy parsing to background thread
                await Task.Run(() =>
                {
                    if (ext == ".csv")
                        LoadCsv(filePath);
                    else if (ext == ".xlsx")
                        LoadExcel(filePath);
                    else
                        throw new NotSupportedException("Unsupported file type.");
                });

                Status = $"Imported {PreviewRows.Count} records. Press SAVE to continue.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading file: {ex.Message}";
                Status = "Load failed.";
            }
            finally
            {
                IsBusy = false;
                // Refresh all dependent UI properties
                OnPropertyChanged(nameof(HasPreview));
                OnPropertyChanged(nameof(CanLoadIntoGrid));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(CanBrowse));
            }
        }

        // -----------------------------
        // Command Handlers
        // -----------------------------

        private void Browse()
        {
            var path = _fileService.OpenFile("Select Excel or CSV File", "Data Files (*.xlsx;*.csv)|*.xlsx;*.csv");
            if (path != null)
            {
                LoadFile(path);
            }
        }

        private void ClearDataGrid()
        {
            DetectedColumns?.Clear ();
            PreviewRows?.Clear ();

            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(CanLoadIntoGrid));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private async void LoadIntoGrid()
        {
            if (!CanLoadIntoGrid) return;

            IsBusy = true;
            Status = "Processing records...";
            ErrorMessage = null;

            try
            {
                // Offload the mapping/transformation logic to a background thread
                var records = await Task.Run(() => BuildRecordsFromAllRows());

                // Invoke the callback (usually updates a main collection or DB)
                OnImportCompleted?.Invoke(records);

                Status = $"Successfully imported {records.Count} records.";

                // Optional: Prevent double-execution if the UI doesn't close immediately
                OnImportCompleted = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Import failed: {ex.Message}";
                Status = "Error during import.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // -----------------------------
        // Internal helpers
        // -----------------------------

        private List<PropertyRecord> BuildRecordsFromAllRows()
        {
            var list = new List<PropertyRecord>();
            var map = BuildColumnMap();

            foreach (var row in _allRows)
            {
                var record = new PropertyRecord
                {
                    ParcelId        = row.GetMappedValue(map, "ParcelId"),
                    Address         = row.GetMappedValue(map, "Address"),
                    Owner           = row.GetMappedValue(map, "Owner"),
                    OpeningBid      = row.GetMappedValue(map, "Bid").TryParseDecimal(),
                    AssessedValue   = row.GetMappedValue(map, "Assessed").TryParseDecimal(),
                    Acres           = row.GetMappedValue(map, "Acres").TryParseDouble()
                };

                list.Add(record);
            }

            return list;
        }

        // PARSING
        private void LoadCsv(string filePath)
        {
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(filePath);
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            if (!parser.EndOfData)
            {
                var headers = parser.ReadFields()?.Select(h => h.NormalizeHeaders()).ToList();
                if (headers != null)
                {
                    // Marshal UI updates back to the main thread
                    App.Current.Dispatcher.Invoke(() => {
                        foreach (var h in headers) DetectedColumns.Add(h);
                    });
                }
            }

            int count = 0;
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null) continue;

                var row = new Dictionary<string, string>();
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i < DetectedColumns.Count)
                        row[DetectedColumns[i]] = fields[i];
                }

                _allRows.Add(row);
                if (count < 50)
                {
                    App.Current.Dispatcher.Invoke(() => PreviewRows.Add(row));
                }
                count++;
            }
        }

        private void LoadExcel(string filePath)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
            var ws = workbook.Worksheets.First();

            // Read header row
            var headerRow = ws.Row(1);
            var headerCells = headerRow.CellsUsed().ToList();

            // Marshal collection clearing to UI thread
            App.Current.Dispatcher.Invoke(() => DetectedColumns.Clear());

            foreach (var cell in headerCells)
            {
                var hdr = cell.GetString().NormalizeHeaders();
                App.Current.Dispatcher.Invoke(() => DetectedColumns.Add(hdr));
            }

            int rowIndex = 2;
            int previewLimit = 50;
            var lastRow = ws.LastRowUsed().RowNumber();

            while (rowIndex <= lastRow)
            {
                var row = new Dictionary<string, string>();
                var excelRow = ws.Row(rowIndex);

                foreach (var headerCell in headerCells)
                {
                    var normalized = headerCell.GetString().NormalizeHeaders();
                    var colIndex = headerCell.Address.ColumnNumber;
                    var value = excelRow.Cell(colIndex).GetString();
                    row[normalized] = value;
                }

                _allRows.Add(row);

                if (previewLimit > 0)
                {
                    var captureRow = row; // Capture for closure
                    App.Current.Dispatcher.Invoke(() => PreviewRows.Add(captureRow));
                    previewLimit--;
                }

                rowIndex++;
            }
        }

        private Dictionary<string, string> BuildColumnMap()
        {
            var map = new Dictionary<string, string>();

            foreach (var detected in DetectedColumns)
            {
                foreach (var (canonical, aliases) in ColumnAliases)
                {
                    // detected is already normalized
                    if (detected.MatchesAlias(aliases))
                    {
                        map[canonical] = detected;
                        break;
                    }
                }
            }

            return map;
        }

        #endregion
    }
}