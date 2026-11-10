using System.Collections.ObjectModel;
using AstroValley.Domain.DueDiligence;
using AstroValley.Domain.Models;
using AstroValley.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>A record office's website, with this pin's parcel number already in the address.</summary>
public record OfficeLink(string Title, string Url, string? Hint);

/// <summary>One record office: its websites and its checklist lines.</summary>
public record ChecklistGroup(string Office, List<OfficeLink> Links, List<DueDiligenceItem> Items);

/// <summary>
/// The due-diligence checklist for one pin: risk flags, names to search and the encumbrance checklist from its jurisdiction profile.
/// Edits go straight onto the pin's items, so they are kept even when the dialog is closed with X; Save Pins writes them to disk.
/// </summary>
public partial class DueDiligenceViewModel : DialogViewModelBase<bool>
{
    private readonly MarkerLocation _pin;
    private readonly IBrowserService _browser;
    private readonly IJurisdictionProfile _profile;

    /// <summary>Writes every pin (and so these answers) to a file; the map page owns the pins, so it supplies the command.</summary>
    public System.Windows.Input.ICommand SavePinsCommand { get; }

    public DueDiligenceViewModel(MarkerLocation pin, IBrowserService browser, System.Windows.Input.ICommand savePinsCommand)
    {
        _pin = pin;
        _browser = browser;
        SavePinsCommand = savePinsCommand;
        _profile = Jurisdictions.For(pin.State, pin.County);
        Risks = pin.Risks ?? [];
        // links are read from the profile each time, not saved on the pin, so a corrected address reaches every pin
        var links = _profile.Links.ToLookup(l => l.Office, l => l.Url(pin.ParcelID) is { } url ? new OfficeLink(l.Title, url, l.Hint) : null);
        var items = (pin.Checklist ?? []).ToLookup(i => i.Office);
        // offices with a website first (the appraiser has no checklist lines), then the rest in checklist order
        Groups = [.. links.Select(l => l.Key).Concat(items.Select(i => i.Key)).Distinct()
            .Select(office => new ChecklistGroup(office, [.. links[office].OfType<OfficeLink>()], [.. items[office]]))];
        Names = [.. pin.SearchNames ?? []];
        UpdateProgress();
    }

    public override string Title => "Due diligence";
    public string Address => _pin.Address;
    public string Subtitle => string.Join(" · ", new[] { _pin.ParcelID, _pin.County, _profile.Name + " checklist" }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public List<RiskFlag> Risks { get; }
    public bool HasRisks => Risks.Count > 0;
    public List<ChecklistGroup> Groups { get; }
    public ObservableCollection<string> Names { get; }
    public string? LegalDescription => _pin.ExtraDetails?.GetValueOrDefault("Legal Description");
    public bool HasLegalDescription => !string.IsNullOrWhiteSpace(LegalDescription);
    public DueDiligenceStatus[] Statuses { get; } = Enum.GetValues<DueDiligenceStatus>();

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _progress = "";

    /// <summary>Called by the view when a status changes.</summary>
    public void UpdateProgress()
    {
        var items = _pin.Checklist ?? [];
        Progress = $"{items.Count(i => i.Status != DueDiligenceStatus.Pending)} of {items.Count} checked"
            + (items.Count(i => i.Status == DueDiligenceStatus.Found) is > 0 and var found ? $" · {found} found" : "");
    }

    // a trust, trustee, lender or other case party the user found; kept with the owners from the county record
    [RelayCommand]
    private void AddName()
    {
        var name = NewName.Trim();
        if (name.Length > 1 && !Names.Contains(name, StringComparer.OrdinalIgnoreCase)) Names.Add(name);
        NewName = "";
        _pin.SearchNames = [.. Names];
    }

    [RelayCommand]
    private void RemoveName(string name)
    {
        Names.Remove(name);
        _pin.SearchNames = [.. Names];
    }

    [RelayCommand]
    private static void Copy(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) System.Windows.Clipboard.SetText(text);
    }

    [RelayCommand]
    private void OpenLink(OfficeLink link) => _browser.Launch(link.Url);

    [RelayCommand]
    private void Done() => CompleteDialog(true);
}
