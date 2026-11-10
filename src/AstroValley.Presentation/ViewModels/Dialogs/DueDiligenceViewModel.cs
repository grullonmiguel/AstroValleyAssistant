using System.Collections.ObjectModel;
using AstroValley.Domain.DueDiligence;
using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>One record office and its checklist lines.</summary>
public record ChecklistGroup(string Office, List<DueDiligenceItem> Items);

/// <summary>
/// The due-diligence checklist for one pin: risk flags, names to search and the encumbrance checklist from its jurisdiction profile.
/// Edits go straight onto the pin's items, so they are kept even when the dialog is closed with X; Save Pins writes them to disk.
/// </summary>
public partial class DueDiligenceViewModel : DialogViewModelBase<bool>
{
    private readonly MarkerLocation _pin;

    public DueDiligenceViewModel(MarkerLocation pin)
    {
        _pin = pin;
        Risks = pin.Risks ?? [];
        Groups = [.. (pin.Checklist ?? []).GroupBy(i => i.Office).Select(g => new ChecklistGroup(g.Key, [.. g]))];
        Names = [.. pin.SearchNames ?? []];
        UpdateProgress();
    }

    public override string Title => "Due diligence";
    public string Address => _pin.Address;
    public string Subtitle => string.Join(" · ", new[] { _pin.ParcelID, _pin.County, Jurisdictions.For(_pin.State, _pin.County).Name + " checklist" }.Where(s => !string.IsNullOrWhiteSpace(s)));
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
    private void Done() => CompleteDialog(true);
}
