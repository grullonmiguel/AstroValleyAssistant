using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using AstroValley.Application.Interfaces.Data;
using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Domain.Entities;
using AstroValley.Domain.Models;
using AstroValley.Domain.Utilities;
using AstroValley.Presentation.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

/// <summary>
/// The My Properties tab: the properties already bought, as a list with totals and an editor for the selected one.
/// Every edit is saved at once, one file per property (see <see cref="IOwnedPropertyStore"/>).
/// </summary>
public partial class MyPropertiesViewModel : ObservableObject
{
    private readonly IOwnedPropertyStore _store;
    private readonly IBrowserService _browser;
    private readonly HomeViewModel _home;
    private readonly IRegridService _regrid;
    private readonly SettingsService _settings;
    private readonly IPortfolioExporter _excel;
    private readonly IServiceProvider _services; // MainViewModel, the dialogs and Comps are resolved when needed: they are built after this view model

    public MyPropertiesViewModel(IOwnedPropertyStore store, IBrowserService browser, HomeViewModel home, IRegridService regrid, SettingsService settings, IPortfolioExporter excel, IServiceProvider services)
    {
        _settings = settings;
        _excel = excel;
        CollectionViewSource.GetDefaultView(Properties).Filter = o => o is OwnedPropertyViewModel p && p.Matches(SearchText) && StatusFilter switch
        {
            "All" => true,
            "Not sold" => !p.Model.IsSold,
            var status => p.Model.Status == status,
        };
        _store = store;
        _browser = browser;
        _home = home;
        _regrid = regrid;
        _services = services;
        home.OwnedPinEdited += OnPinEdited;
        home.OwnedPropertyRequested += id => { if (Properties.FirstOrDefault(p => p.Model.Id == id) is { } item) Show(item); };
        home.BoughtRequested += pin =>
        {
            home.RemoveMarkerCommand.Execute(pin); // the gold pin takes its place
            AddBought(OwnedProperty.FromPin(pin, DateTime.Today));
        };
        try
        {
            foreach (var p in store.List()) Properties.Add(new OwnedPropertyViewModel(p, Save));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not read the saved properties: {ex.Message}";
        }
        foreach (var item in Properties) SyncPin(item);
        Selected = Properties.FirstOrDefault();
    }

    public ObservableCollection<OwnedPropertyViewModel> Properties { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(ExportCommand), nameof(ScrapeCommand), nameof(FindCompsCommand), nameof(ShowOnMapCommand),
        nameof(SaveTiersAsDefaultCommand), nameof(ExportTermSheetCommand), nameof(NeighborsToLeadsCommand), nameof(CopyBriefCommand), nameof(PasteResearchCommand),
        nameof(AddFilesCommand), nameof(OpenDocumentsCommand))]
    private OwnedPropertyViewModel? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScrapeCommand))]
    private bool _isScraping;

    [ObservableProperty]
    private string _status = "Every change is saved as you make it.";

    // --- reminders: what needs attention across every property, shown above the list ---
    public IReadOnlyList<string> Reminders => [.. Properties.SelectMany(p => p.Model.Reminders(DateTime.Today).Select(r => $"{p.Title}: {r}"))];
    public bool HasReminders => Reminders.Count > 0;

    // --- documents and photos of the selected property: plain files in its own folder ---
    public record DocFile(string Name, string Path, System.Windows.Media.ImageSource? Thumbnail);

    [ObservableProperty]
    private IReadOnlyList<DocFile> _files = [];

    partial void OnSelectedChanged(OwnedPropertyViewModel? value) => RefreshFiles();

    private static readonly string[] ImageTypes = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    private void RefreshFiles()
    {
        try
        {
            var folder = Selected is null ? null : _store.FolderOf(Selected.Model.Id);
            Files = folder is null || !Directory.Exists(folder) ? []
                : [.. Directory.EnumerateFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Select(f => new DocFile(Path.GetFileName(f), f, Thumbnail(f)))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Files = []; }
    }

    private static System.Windows.Media.ImageSource? Thumbnail(string path)
    {
        if (!ImageTypes.Contains(Path.GetExtension(path).ToLowerInvariant())) return null;
        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 240;                                                  // a thumbnail, not the full photo, in memory
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;     // read now, so the file is not left locked
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return null; // a damaged picture still shows by name
        }
    }

    /// <summary>Copies files (the deed, tax bills, photos) into the property's folder; a name already there gets " (2)".</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddFiles()
    {
        if (Selected is not { } item) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = $"Add documents and photos to {item.Title}" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var folder = _store.FolderOf(item.Model.Id);
            Directory.CreateDirectory(folder);
            foreach (var source in dialog.FileNames)
            {
                var target = Path.Combine(folder, Path.GetFileName(source));
                for (var n = 2; File.Exists(target); n++)
                    target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)} ({n}){Path.GetExtension(source)}");
                File.Copy(source, target);
            }
            Status = $"Copied {dialog.FileNames.Length} file(s) into {item.Title}'s folder. The originals are untouched.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = $"Could not copy the files: {ex.Message}"; }
        RefreshFiles();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenDocuments()
    {
        if (Selected is not { } item) return;
        Directory.CreateDirectory(_store.FolderOf(item.Model.Id));
        _browser.Launch(_store.FolderOf(item.Model.Id));
    }

    [RelayCommand]
    private void OpenFile(string? path)
    {
        if (path is not null && File.Exists(path)) _browser.Launch(path);
        else RefreshFiles(); // it was moved or deleted outside the app
    }

    /// <summary>Every property, every expense line and the spending by year, as one workbook.</summary>
    [RelayCommand]
    private async Task ExportExcel()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"My Properties {DateTime.Today:yyyy-MM-dd}.xlsx" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await _excel.ExportAsync(Properties.Select(p => p.Model).ToList(), dialog.FileName);
            _browser.Launch(dialog.FileName);
            Status = $"Exported {Properties.Count} properties to {dialog.FileName}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = $"Could not export (is the file open in Excel?): {ex.Message}"; }
    }

    // --- status pipeline: a column in the list and this filter above it ---
    public static string[] StatusOptions => OwnedProperty.Statuses;
    public static string[] StatusFilters { get; } = ["All", "Not sold", .. OwnedProperty.Statuses];

    [ObservableProperty]
    private string _statusFilter = "All";

    /// <summary>The sidebar's search box: address, city, parcel ID or lot number.</summary>
    [ObservableProperty]
    private string _searchText = "";

    partial void OnSearchTextChanged(string value) => OnStatusFilterChanged(StatusFilter);

    /// <summary>A card's Edit button: the small dialog with that section's fields ("Property", "Purchase", "Lot", "Value", "Utilities", "Links").</summary>
    [RelayCommand]
    private void EditSection(string? section)
    {
        if (Selected is { } item && section is not null && SectionEditViewModel.Sections.ContainsKey(section))
            _services.GetRequiredService<IDialogService>().ShowDialog(new SectionEditViewModel(item, section, OpenLinkCommand));
    }

    partial void OnStatusFilterChanged(string value)
    {
        CollectionViewSource.GetDefaultView(Properties).Refresh();
        if (Selected is null || !CollectionViewSource.GetDefaultView(Properties).Contains(Selected))
            Selected = CollectionViewSource.GetDefaultView(Properties).Cast<OwnedPropertyViewModel>().FirstOrDefault();
    }

    public static BuyerType[] BuyerTypes { get; } = Enum.GetValues<BuyerType>();

    /// <summary>The line under the logo on every term sheet (city, e-mail, phone); kept in the app's settings, not per property.</summary>
    public string ContactLine
    {
        get => _settings.Seller.ContactLine;
        set
        {
            if (value == _settings.Seller.ContactLine) return;
            _settings.Seller.ContactLine = value ?? "";
            SaveSettings();
            OnPropertyChanged();
        }
    }

    private void SaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = $"Could not save the settings: {ex.Message}"; }
    }

    private List<FinancingTier> StartingTiers() => [.. (_settings.Seller.FinancingTiers is { Count: > 0 } mine ? mine : FinancingTier.Defaults()).Select(t => t with { })];

    /// <summary>Makes the selected property's financing offers the ones every new property starts with.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SaveTiersAsDefault()
    {
        if (Selected is not { } item) return;
        _settings.Seller.FinancingTiers = [.. item.Model.FinancingTiers.Select(t => t with { })];
        SaveSettings();
        Status = "New properties will start with these financing offers.";
    }

    /// <summary>Writes the term sheet as a web page and opens it; print it to PDF from the browser.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ExportTermSheet()
    {
        if (Selected is not { } item) return;
        if (item.Model.FinancingQuotes.Count == 0) { Status = "Set a market value, an asking price or a financing price first; the term sheet needs a price."; return; }
        var name = string.Concat(item.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Web page (*.html)|*.html", FileName = $"Term sheet - {name}.html" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, TermSheetHtml.Build(item.Model, ContactLine, TermSheetHtml.LogoSvg()));
            _browser.Launch(dialog.FileName);
            Status = $"Term sheet saved to {dialog.FileName}. Print it to PDF from the browser.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save the term sheet: {ex.Message}";
        }
    }

    /// <summary>Puts the property's facts on the clipboard, worded for Claude's property-intel skill.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyBrief()
    {
        if (Selected is not { } item) return;
        try
        {
            Clipboard.SetText(item.Model.BriefForClaude());
            Status = $"Copied the brief for {item.Title}. Paste it into Claude; copy its JSON block when it is done, then press Paste results.";
        }
        catch (System.Runtime.InteropServices.COMException) { Status = "The clipboard is busy. Try again."; } // another program is holding it
    }

    /// <summary>Takes the JSON block Claude wrote from the clipboard and adds it to the property (see IOwnedPropertyStore.ApplyResearch).</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PasteResearch()
    {
        if (Selected is not { } item) return;
        try
        {
            var (merged, changes) = _store.ApplyResearch(item.Model, Clipboard.ContainsText() ? Clipboard.GetText() : "");
            if (changes > 0) item.Replace(merged);
            Status = changes == 0 ? "Nothing new in the copied text: the texts match and every other field already had a value."
                : $"Added {changes} thing(s) from Claude to {item.Title}. Values you had typed were kept; only the three marketing texts are replaced.";
        }
        catch (JsonException ex) { Status = $"The copied text is not usable: {ex.Message}"; }
        catch (System.Runtime.InteropServices.COMException) { Status = "The clipboard is busy. Try again."; }
    }

    /// <summary>Every neighbor not already in the buyers log becomes a lead there.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void NeighborsToLeads()
    {
        if (Selected is not { } item) return;
        var added = item.AddNeighborsAsLeads();
        Status = added == 0 ? "Every neighbor is already in the buyers log (or there are none yet)." : $"Added {added} neighbor(s) to the buyers log.";
    }

    public static UtilityStatus[] UtilityOptions { get; } = Enum.GetValues<UtilityStatus>();
    public static ExpenseCategory[] ExpenseCategories { get; } = Enum.GetValues<ExpenseCategory>();

    // --- totals above the list ---
    public bool IsEmpty => Properties.Count == 0;
    /// <summary>The totals one per line, for the sidebar.</summary>
    public string TotalsLines => Totals.Replace("   ·   ", Environment.NewLine);

    public string Totals
    {
        get
        {
            if (Properties.Count == 0) return "";
            var held = Properties.Where(p => !p.Model.IsSold).Select(p => p.Model).ToList();
            var sold = Properties.Where(p => p.Model.IsSold).Select(p => p.Model).ToList();
            return string.Join("   ·   ", new[]
            {
                $"{held.Count} held",
                $"Spent {held.Sum(p => p.TotalSpent):C0}",
                $"Market value {held.Sum(p => p.MarketValue ?? 0):C0}",
                $"Minimum sale {held.Sum(p => p.MinimumSale ?? 0):C0}",
                sold.Count > 0 ? $"{sold.Count} sold, net profit {sold.Sum(p => p.NetProfit ?? 0):C0}" : null,
                Properties.Where(p => p.Model.NoteBalance > 0).Sum(p => p.Model.NoteMonthly ?? 0) is > 0 and var income ? $"Payments coming in {income:C0} a month" : null,
            }.Where(s => s is not null));
        }
    }

    private void Save(OwnedPropertyViewModel item)
    {
        try
        {
            _store.Save(item.Model);
            Status = $"Saved {item.Title}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"NOT saved, {item.Title}: {ex.Message}";
        }
        OnPropertyChanged(nameof(Totals));
        OnPropertyChanged(nameof(TotalsLines));
        OnPropertyChanged(nameof(Reminders));
        OnPropertyChanged(nameof(HasReminders));
        SyncPin(item);
    }

    // --- Home map: each property with coordinates has a gold pin there, redrawn when the property changes ---
    private readonly Dictionary<string, MarkerLocation> _pins = [];
    private bool _editFromPin;

    private void SyncPin(OwnedPropertyViewModel item)
    {
        if (_editFromPin) return; // the pin already shows the edit; redrawing it would close its popup
        if (_pins.Remove(item.Model.Id, out var old)) _home.RemoveMarkerCommand.Execute(old);
        if (item.Model.ToPin() is not { } pin) return;
        _home.AddMarkers([pin]);
        _pins[item.Model.Id] = pin;
    }

    // notes typed or comps accepted in the pin's popup on the Home map
    private void OnPinEdited(MarkerLocation pin)
    {
        if (Properties.FirstOrDefault(p => p.Model.Id == pin.OwnedId) is not { } item) return;
        _editFromPin = true;
        try { item.ApplyPin(pin.Notes, pin.Comps); }
        finally { _editFromPin = false; }
    }

    private void Show(OwnedPropertyViewModel item)
    {
        Selected = item;
        _services.GetRequiredService<MainViewModel>().CurrentViewModel = this;
    }

    /// <summary>"Bought" on an auction row or "I bought this" on a Home pin: the property arrives with its research; only the purchase is left to type.</summary>
    public void AddBought(OwnedProperty property)
    {
        var parcel = property.Record.ParcelId;
        var existing = parcel.Length == 0 ? null : Properties.FirstOrDefault(p =>
            string.Equals(p.Model.Record.ParcelId, parcel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Model.Record.State, property.Record.State, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SyncPin(existing);
            Show(existing);
            Status = $"{existing.Title} is already in My Properties.";
            return;
        }
        var item = new OwnedPropertyViewModel(property with { FinancingTiers = StartingTiers() }, Save);
        Properties.Insert(0, item);
        Save(item);
        ListChanged();
        Show(item);
        Status = $"Added {item.Title}. Type the purchase price and the fees under Purchase Info.";
    }

    private bool CanScrape => Selected is not null && !IsScraping;

    /// <summary>Looks the property up on Regrid and fills the fields that are still empty; anything typed is kept.</summary>
    [RelayCommand(CanExecute = nameof(CanScrape))]
    private async Task Scrape()
    {
        if (Selected is not { } item) return;
        var r = item.Model.Record;
        // the full address finds one parcel far more often than a parcel number, which repeats across counties
        var query = r.Address.Length == 0 ? r.ParcelId
            : r.Address.Contains(',') ? r.Address
            : string.Join(", ", new[] { r.Address, r.City, $"{r.State} {r.Zip}".Trim() }.Where(s => s.Length > 0));
        if (query.Length == 0) { Status = "Type an address or a parcel ID first."; return; }

        IsScraping = true;
        Status = $"Looking up {query} on Regrid...";
        try
        {
            var result = await _regrid.ScrapeSingleAsync(query, CancellationToken.None);
            if (result.IsMultiple)
            {
                var dialog = new ResolveMatchesViewModel(r.ParcelId, r.Address, r.Owner, result.Matches);
                _services.GetRequiredService<IDialogService>().ShowDialog(dialog);
                RegridMatch? match = null;
                try { match = await dialog.Result; }
                catch (TaskCanceledException) { } // closed without choosing
                if (match is null) { Status = $"Regrid found {result.Matches.Count} parcels for {query}; none was chosen."; return; }
                Status = $"Reading {match.Address} from Regrid...";
                result = await _regrid.ScrapeSingleAsync(match.FullUrl, CancellationToken.None);
            }

            if (result.IsRateLimited) Status = "Regrid is limiting requests right now. Try again in a few minutes.";
            else if (result.Error is not null) Status = $"Regrid lookup failed: {result.Error.Message} (the Regrid sign-in is under the menu, Regrid)";
            else if (result.NotFound || result.Record is null) Status = $"Regrid found nothing for {query}. Try the parcel ID, or the address with city and state.";
            else
            {
                item.MergeScraped(result.Record);
                Status = $"Regrid data added to {item.Title}. Fields you had typed were kept."
                    + (item.Model.ToPin() is null ? " No coordinates came back, so there is no map pin yet." : "");
            }
        }
        finally { IsScraping = false; }
    }

    /// <summary>Opens the Comps tab for the property; the accepted result is saved on it and becomes the market value when none was typed.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FindComps()
    {
        if (Selected is not { } item) return;
        var r = item.Model.Record;
        var subject = new CompsSubject(r.Address.Length > 0 ? r.Address : r.ParcelId, item.Model.Acres, r.PropertyType,
            r.Latitude.TryParseDouble(), r.Longitude.TryParseDouble(), r.Zip, r.LivingSqft, r.Beds, r.Baths, r.HomeType);
        _services.GetRequiredService<CompsViewModel>().Open(subject, result =>
        {
            var hadValue = item.Model.MarketValue is not null;
            item.ApplyPin(item.Model.Notes, result);
            Status = result.EstimatedValue is { } v
                ? $"Comps saved on {item.Title}: {v:C0}." + (hadValue ? $" Market value left at {item.Model.MarketValue:C0}; change it by hand if you agree with the comps." : " Used as the market value.")
                : $"Comps saved on {item.Title}.";
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ShowOnMap()
    {
        if (Selected is not { } item) return;
        if (!_pins.TryGetValue(item.Model.Id, out var pin))
        {
            Status = $"{item.Title} has no coordinates yet. Press Scrape, or type the latitude and longitude.";
            return;
        }
        _services.GetRequiredService<MainViewModel>().CurrentViewModel = _home;
        _home.FlyTo(pin);
    }

    private void ListChanged()
    {
        OnPropertyChanged(nameof(Totals));
        OnPropertyChanged(nameof(TotalsLines));
        OnPropertyChanged(nameof(Reminders));
        OnPropertyChanged(nameof(HasReminders));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Add()
    {
        var item = new OwnedPropertyViewModel(new OwnedProperty { PurchaseDate = DateTime.Today, FinancingTiers = StartingTiers() }, Save);
        Properties.Insert(0, item);
        Selected = item;
        Save(item);
        ListChanged();
        Status = "New property added.";
        EditSection("Property"); // straight to the address and parcel
    }

    private bool HasSelection => Selected is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is not { } item) return;
        if (MessageBox.Show($"Delete {item.Title}?\n\nIts file goes to the Recycle Bin, with its {item.Model.Expenses.Count} expense lines.",
                "Delete property", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        try
        {
            _store.Delete(item.Model.Id);
            if (_pins.Remove(item.Model.Id, out var pin)) _home.RemoveMarkerCommand.Execute(pin);
            Properties.Remove(item);
            Selected = Properties.FirstOrDefault();
            ListChanged();
            Status = $"Moved {item.Title} to the Recycle Bin.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not delete {item.Title}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Property file (*.json)|*.json", Multiselect = true, Title = "Import properties" };
        if (dialog.ShowDialog() != true) return;

        var failed = new List<string>();
        OwnedPropertyViewModel? last = null;
        foreach (var path in dialog.FileNames)
        {
            try
            {
                last = new OwnedPropertyViewModel(_store.Import(path), Save);
                Properties.Insert(0, last);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                failed.Add(Path.GetFileName(path));
            }
        }
        if (last is not null) Selected = last;
        ListChanged();
        var added = dialog.FileNames.Length - failed.Count;
        Status = (added == 1 ? "Imported 1 property." : $"Imported {added} properties.")
            + (failed.Count > 0 ? $" Not a property file: {string.Join(", ", failed)}." : "");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Export()
    {
        if (Selected is not { } item) return;
        var name = string.Concat(item.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Property file (*.json)|*.json", FileName = name + ".json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _store.Export(item.Model, dialog.FileName);
            Status = $"Exported {item.Title} to {dialog.FileName}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not export {item.Title}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(_store.Folder); // nothing saved yet: still show where it will go
        _browser.Launch(_store.Folder);
    }

    /// <summary>Opens one of the property's links (auction page, GIS viewer, county detail page).</summary>
    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            _browser.Launch(uri.AbsoluteUri);
        else
            Status = string.IsNullOrWhiteSpace(url) ? "There is no link for this yet. Scrape the property, or type the address." : "That is not a web address (it should start with https://).";
    }
}

/// <summary>
/// One owned property, editable: each property replaces the immutable <see cref="Model"/> with a changed copy and reports it for saving,
/// so the list, the totals and the computed figures all follow an edit.
/// </summary>
public class OwnedPropertyViewModel(OwnedProperty model, Action<OwnedPropertyViewModel> changed) : ObservableObject
{
    public OwnedProperty Model { get; private set; } = WithCurrentChecklist(model);

    // steps added to the checklist after this property was saved appear at the end of its list; nothing is saved until an edit
    private static OwnedProperty WithCurrentChecklist(OwnedProperty p)
    {
        p.SaleChecklist.AddRange(OwnedProperty.NewSaleChecklist().Where(step => p.SaleChecklist.All(s => s.Key != step.Key)));
        return p;
    }

    // --- selling ---
    public decimal? ConservativeValue { get => Model.ConservativeValue; set => Set(Model with { ConservativeValue = value }); }
    public decimal? OptimisticValue { get => Model.OptimisticValue; set => Set(Model with { OptimisticValue = value }); }
    public decimal? NegotiationTarget { get => Model.NegotiationTarget; set => Set(Model with { NegotiationTarget = value }); }
    public decimal? FinancingPrice { get => Model.FinancingPrice; set => Set(Model with { FinancingPrice = value }); }
    public DateTime? SaleDate { get => Model.SaleDate; set => Set(Model with { SaleDate = value }); }
    public decimal? SalePrice { get => Model.SalePrice; set => Set(Model with { SalePrice = value }); }
    public decimal SaleClosingCosts { get => Model.SaleClosingCosts; set => Set(Model with { SaleClosingCosts = value }); }

    // --- owner-financed sale and the tax reminder ---
    public decimal? NoteDownPayment { get => Model.NoteDownPayment; set => Set(Model with { NoteDownPayment = value }); }
    public decimal? NoteApr { get => Model.NoteApr; set => Set(Model with { NoteApr = value }); }
    public int? NoteMonths { get => Model.NoteMonths; set => Set(Model with { NoteMonths = value }); }
    public DateTime? NoteFirstPayment { get => Model.NoteFirstPayment; set => Set(Model with { NoteFirstPayment = value }); }
    public DateTime? NextTaxDue { get => Model.NextTaxDue; set => Set(Model with { NextTaxDue = value }); }

    public string NoteSummary
    {
        get
        {
            if (!Model.HasNote) return "Type the sale price in The sale card, then the down payment and the term here.";
            var today = DateTime.Today;
            return string.Join("   ·   ", new[]
            {
                $"Financed {Model.NotePrincipal:C}",
                $"{Model.NoteMonthly:C} a month",
                $"Received {Model.NotePaid:C}",
                $"Balance {Model.NoteBalance:C}",
                Model.NoteBalance == 0 ? "Paid off" : Model.NoteNextDue(today) is { } next ? $"Next due {next:d}" : null,
                Model.NoteBehind(today) is > 0 and var behind ? $"{behind:C} BEHIND" : null,
            }.Where(s => s is not null));
        }
    }

    // --- read-only text for the cards, the header and the sidebar ---
    private static string Us(FormattableString text) => text.ToString(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
    private static string Words(UtilityStatus s) => s switch { UtilityStatus.AtStreet => "At the street", UtilityStatus.Connected => "Connected", UtilityStatus.No => "Not available", _ => "Unknown" };

    public bool Matches(string search) => string.IsNullOrWhiteSpace(search)
        || new[] { Model.Record.Address, Model.Record.City, Model.Record.ParcelId, Model.LotNumber, Model.Record.Zip }.Any(s => s.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

    public string CityLine => string.Join(", ", new[] { Model.Record.City, $"{Model.Record.State} {Model.Record.Zip}".Trim() }.Where(s => s.Length > 0));
    public string FullAddress => string.Join(", ", new[] { Title, CityLine }.Where(s => s.Length > 0));
    public string StateZip => $"{Model.Record.State} {Model.Record.Zip}".Trim();
    public string ZoningText => $"{Model.Record.ZoningCode} {Model.Record.ZoningType}".Trim();
    public string HeaderFacts => string.Join("   ·   ", new[]
    {
        Model.Acres is { } acres ? Us($"{acres:0.##} acres") : null,
        Size.Length > 0 ? $"{Size} ft" : null,
        Model.Record.Beds is { } beds ? $"{beds} bed" : null,
        Model.Record.Baths is { } baths ? $"{baths} bath" : null,
        Model.Record.LivingSqft is { } sqft ? Us($"{sqft:N0} sq ft") : null,
        Model.Record.PropertyType.Length > 0 ? Model.Record.PropertyType : null,
        ZoningText.Length > 0 ? $"Zoning {Model.Record.ZoningCode}" : null,
        Model.LotNumber.Length > 0 ? $"Lot {Model.LotNumber}" : null,
    }.Where(s => s is not null));

    // sold: what was made; held: what the market value would leave over everything spent
    public string ProfitLabel => Model.NetProfit is not null ? "Net Profit" : "Est. Profit";
    public decimal? ProfitValue => Model.NetProfit ?? Model.MarketValue - Model.TotalSpent;
    public decimal? ProfitPercent => Model.NetProfit is not null ? Model.NetRoiPercent : Model.MarketGainPercent;

    public string PremiumText => Us($"{Model.BuyerPremium:C} ({Model.BuyerPremiumPercent:0.##}%)");
    public string MinimumSaleText => Model.MinimumSale is { } min ? Us($"{min:C} ({Model.MinimumSalePercent:0.##}% of market value)") : "–";
    public string CornerLotText => Model.CornerLot ? "Yes" : "No";
    public string RoadFrontageText => Model.RoadFrontage switch { true => "Yes", false => "No", _ => "Not checked" };
    public string ElectricityText => Words(Model.Electricity);
    public string WaterText => Words(Model.Water);
    public string SewerText => Words(Model.Sewer);
    public string GasText => Words(Model.Gas);
    public string PhoneText => Words(Model.Phone);

    private static string Count(int n, string one, string many) => n == 0 ? $"No {many} yet" : n == 1 ? $"1 {one}" : $"{n} {many}";
    public string ExpensesSubtitle => Model.Expenses.Count == 0 ? "Track what you spend after the purchase: tax bills, grass cuts, signs" : Us($"{Count(Model.Expenses.Count, "line", "lines")} · {Model.ExpensesTotal:C}");
    public string NeighborsSubtitle => Model.Neighbors.Count == 0 ? "The owners next door: often the first buyers to call" : Count(Model.Neighbors.Count, "neighbor", "neighbors");
    public string ListingsSubtitle => Model.Listings.Count == 0 ? "Where it is advertised" : Count(Model.Listings.Count, "listing", "listings");
    public string BuyersSubtitle => Model.Buyers.Count == 0 ? "Everyone who asked or made an offer" : Count(Model.Buyers.Count, "lead", "leads");
    public string SaleSubtitle => Model.SalePrice is { } price ? Us($"Sold for {price:C}") + (Model.HasNote ? Us($" · balance {Model.NoteBalance:C}") : "") : "Record the sale, and the payments if you financed it";

    public string ChecklistProgress => $"{Model.SaleChecklist.Count(s => s.Done)} of {Model.SaleChecklist.Count} done";
    public string FinancingBasisText => Model.FinancingBasis is { } price ? $"Worked out on {price:C}" : "Set a market value or an asking price to see the payments";
    public string HeldText => Model.DaysHeld(DateTime.Today) is { } days ? $"{days:N0} days" + (Model.CostPerMonthHeld(DateTime.Today) is { } m ? $" · {m:C} a month in upkeep" : "") : "";

    /// <summary>The county's record offices, where the app knows their websites (see the jurisdiction profiles).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> OfficeLinks =>
        [.. AstroValley.Domain.DueDiligence.Jurisdictions.For(Model.Record.State, Model.Record.County).Links
            .Select(l => KeyValuePair.Create(l.Title, l.Url(Model.Record.ParcelId) ?? "")).Where(l => l.Value.Length > 0)];

    public int AddNeighborsAsLeads()
    {
        var fresh = Model.Neighbors.Where(n => n.Name.Trim().Length > 0
            && Model.Buyers.All(b => !string.Equals(b.Name.Trim(), n.Name.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        if (fresh.Count == 0) return 0;
        // a new list, not an addition to the old one: the buyers grid only notices its own edits
        Set(Model with { Buyers = [.. Model.Buyers, .. fresh.Select(n => new BuyerLead
        {
            Name = n.Name.Trim(), Type = BuyerType.Neighbor, Status = "To contact",
            Contact = n.MailingAddress.Length > 0 ? n.MailingAddress : n.PropertyAddress,
        })] });
        return fresh.Count;
    }

    private void Set(OwnedProperty next)
    {
        if (next == Model) return;
        Model = next;
        Touch();
    }

    private void Rec(Func<PropertyRecord, PropertyRecord> change) => Set(Model with { Record = change(Model.Record) });

    /// <summary>Saves and refreshes every figure; also called by the view after the expense and neighbor grids change their lists in place.</summary>
    public void Touch()
    {
        OnPropertyChanged(string.Empty); // every binding on this row: the computed figures depend on most fields
        changed(this);
    }

    /// <summary>The property after research was pasted in: new lists, so the grids redraw.</summary>
    public void Replace(OwnedProperty next) => Set(next);

    /// <summary>Regrid's record fills what is still empty; nothing typed is overwritten (see PropertyRecordMerger).</summary>
    public void MergeScraped(PropertyRecord scraped) => Set(Model with { Record = PropertyRecordMerger.Merge(Model.Record, scraped) });

    /// <summary>Notes and comps as edited on the Home map pin or accepted in the Comps tab. The comps' value becomes the market value only when none was typed.</summary>
    public void ApplyPin(string? notes, CompsResult? comps) => Set(Model with
    {
        Notes = notes ?? "",
        Record = Model.Record with { Comps = comps },
        MarketValue = Model.MarketValue ?? comps?.EstimatedValue,
    });

    public string CompsSummary => Model.Record.Comps is { EstimatedValue: { } v } c ? $"{v:C0} from {c.Count} comps" : "none yet";

    // --- aerial photo and map links; the photo is kept until the place changes, so other edits do not reload it ---
    private string? _aerialFor;
    private AerialImage.Result? _aerial;
    private System.Windows.Media.ImageSource? _aerialPhoto;

    public AerialImage.Result? Aerial
    {
        get
        {
            var r = Model.Record;
            var key = $"{r.Latitude}|{r.Longitude}|{r.ParcelLines?.Length}";
            if (key == _aerialFor) return _aerial;
            _aerialFor = key;
            // built at half the card's size and stretched: the imagery service refuses a lot-sized picture any sharper than about 0.3 m per pixel
            _aerial = AerialImage.Build(r.Latitude, r.Longitude, r.ParcelLines, 179, 95);
            _aerialPhoto = _aerial is null ? null : new System.Windows.Media.Imaging.BitmapImage(new Uri(_aerial.Url));
            return _aerial;
        }
    }
    public System.Windows.Media.ImageSource? AerialPhoto => Aerial is null ? null : _aerialPhoto;

    public string? StreetViewUrl => Aerial is null ? null : $"https://www.google.com/maps/@?api=1&map_action=pano&viewpoint={Model.Record.Latitude},{Model.Record.Longitude}";
    public string? GoogleMapsUrl => UrlBuilder.BuildGoogleMapsUrl(Model.Record);
    public string RegridUrl => Model.Record.RegridUrl;

    public string Title => Model.Record.Address is { Length: > 0 } a ? a : Model.Record.ParcelId is { Length: > 0 } p ? p : "New property";

    // --- scraped fields (the same ones an auction property has) ---
    public string Address { get => Model.Record.Address; set => Rec(r => r with { Address = value?.Trim() ?? "" }); }
    public string ParcelId { get => Model.Record.ParcelId; set => Rec(r => r with { ParcelId = value?.Trim() ?? "" }); }
    public string City { get => Model.Record.City; set => Rec(r => r with { City = value ?? "" }); }
    public string County { get => Model.Record.County; set => Rec(r => r with { County = value ?? "" }); }
    public string State { get => Model.Record.State; set => Rec(r => r with { State = (value ?? "").Trim().ToUpperInvariant() }); }
    public string Zip { get => Model.Record.Zip; set => Rec(r => r with { Zip = value ?? "" }); }
    public string Owner { get => Model.Record.Owner; set => Rec(r => r with { Owner = value ?? "" }); }
    public string ZoningCode { get => Model.Record.ZoningCode; set => Rec(r => r with { ZoningCode = value ?? "" }); }
    public string ZoningType { get => Model.Record.ZoningType; set => Rec(r => r with { ZoningType = value ?? "" }); }
    public string PropertyType { get => Model.Record.PropertyType; set => Rec(r => r with { PropertyType = value ?? "" }); }
    public string FloodZone { get => Model.Record.FloodZone; set => Rec(r => r with { FloodZone = value ?? "" }); }
    public string Latitude { get => Model.Record.Latitude; set => Rec(r => r with { Latitude = value?.Trim() ?? "" }); }
    public string Longitude { get => Model.Record.Longitude; set => Rec(r => r with { Longitude = value?.Trim() ?? "" }); }
    public string LegalDescription { get => Model.Record.LegalDescription; set => Rec(r => r with { LegalDescription = value ?? "" }); }
    public double? ScrapedAcres { get => Model.Record.Acres; set => Rec(r => r with { Acres = value }); }
    public decimal? AssessedValue { get => Model.Record.AssessedValue; set => Rec(r => r with { AssessedValue = value }); }

    // --- identity and links ---
    public string LotNumber { get => Model.LotNumber; set => Set(Model with { LotNumber = value ?? "" }); }
    public string PurchasedBy { get => Model.PurchasedBy; set => Set(Model with { PurchasedBy = value ?? "" }); }
    public string AuctionUrl { get => Model.AuctionUrl; set => Set(Model with { AuctionUrl = value?.Trim() ?? "" }); }
    public string GisUrl { get => Model.GisUrl; set => Set(Model with { GisUrl = value?.Trim() ?? "" }); }
    public string PropertyDetailUrl { get => Model.PropertyDetailUrl; set => Set(Model with { PropertyDetailUrl = value?.Trim() ?? "" }); }

    // --- lot ---
    public double? LotWidthFt { get => Model.LotWidthFt; set => Set(Model with { LotWidthFt = value }); }
    public double? LotDepthFt { get => Model.LotDepthFt; set => Set(Model with { LotDepthFt = value }); }
    public bool CornerLot { get => Model.CornerLot; set => Set(Model with { CornerLot = value }); }
    public bool? RoadFrontage { get => Model.RoadFrontage; set => Set(Model with { RoadFrontage = value }); }
    public string Easements { get => Model.Easements; set => Set(Model with { Easements = value ?? "" }); }
    public string Size => Model is { LotWidthFt: { } w, LotDepthFt: { } d } ? $"{w:0.##}x{d:0.##}" : "";
    public string LotSummary => Model.LotSqft is { } sqft ? $"{sqft:N0} sq ft · {Model.Acres:0.####} acres" : "Type the width and depth to get the area";

    // --- purchase ---
    public DateTime? PurchaseDate { get => Model.PurchaseDate; set => Set(Model with { PurchaseDate = value }); }
    public decimal? StartingBid { get => Model.StartingBid; set => Set(Model with { StartingBid = value }); }
    public decimal PurchasePrice { get => Model.PurchasePrice; set => Set(Model with { PurchasePrice = value }); }
    public decimal BuyerPremiumPercent { get => Model.BuyerPremiumPercent; set => Set(Model with { BuyerPremiumPercent = value }); }
    public decimal TaxesAtPurchase { get => Model.TaxesAtPurchase; set => Set(Model with { TaxesAtPurchase = value }); }
    public decimal DeedFee { get => Model.DeedFee; set => Set(Model with { DeedFee = value }); }
    public string DeedType { get => Model.DeedType; set => Set(Model with { DeedType = value ?? "" }); }
    public DateTime? DeedRecordedDate { get => Model.DeedRecordedDate; set => Set(Model with { DeedRecordedDate = value }); }
    public string DeedInstrument { get => Model.DeedInstrument; set => Set(Model with { DeedInstrument = value ?? "" }); }

    // --- utilities ---
    public UtilityStatus Phone { get => Model.Phone; set => Set(Model with { Phone = value }); }
    public UtilityStatus Electricity { get => Model.Electricity; set => Set(Model with { Electricity = value }); }
    public UtilityStatus Water { get => Model.Water; set => Set(Model with { Water = value }); }
    public UtilityStatus Sewer { get => Model.Sewer; set => Set(Model with { Sewer = value }); }
    public UtilityStatus Gas { get => Model.Gas; set => Set(Model with { Gas = value }); }

    // --- pricing ---
    public decimal? MarketValue { get => Model.MarketValue; set => Set(Model with { MarketValue = value }); }
    public decimal MinimumSalePercent { get => Model.MinimumSalePercent; set => Set(Model with { MinimumSalePercent = value }); }
    public decimal? AskingPrice { get => Model.AskingPrice; set => Set(Model with { AskingPrice = value }); }
    public decimal? QuickSalePrice { get => Model.QuickSalePrice; set => Set(Model with { QuickSalePrice = value }); }

    // --- text ---
    public string Status { get => Model.Status; set => Set(Model with { Status = value ?? "" }); }
    public string Notes { get => Model.Notes; set => Set(Model with { Notes = value ?? "" }); }
    public string Overview { get => Model.Overview; set => Set(Model with { Overview = value ?? "" }); }
    public string LocationOverview { get => Model.LocationOverview; set => Set(Model with { LocationOverview = value ?? "" }); }
    public string ListingAd { get => Model.ListingAd; set => Set(Model with { ListingAd = value ?? "" }); }

    // --- expenses: the grid edits Model.Expenses in place, then the view calls Touch() ---
    public string ExpensesByCategory => Summary(Model.Expenses.GroupBy(e => e.Category.ToString()));
    public string ExpensesByYear => Summary(Model.Expenses.GroupBy(e => e.Date.Year.ToString()).OrderBy(g => g.Key));

    private static string Summary(IEnumerable<IGrouping<string, Expense>> groups) =>
        string.Join("   ·   ", groups.Select(g => $"{g.Key} {g.Sum(e => e.Amount):C}"));
}
