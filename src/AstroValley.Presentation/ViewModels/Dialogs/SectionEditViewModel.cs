using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// The small dialog behind a card's Edit button in My Properties: the editable fields of one section of one property.
/// Edits go straight to the property, which saves itself, so closing never loses or discards anything.
/// </summary>
public partial class SectionEditViewModel(OwnedPropertyViewModel item, string section, ICommand openLinkCommand) : DialogViewModelBase<bool>
{
    /// <summary>Section key -> dialog title; the key also names the view's template ("Edit." + key).</summary>
    public static readonly IReadOnlyDictionary<string, string> Sections = new Dictionary<string, string>
    {
        ["Property"] = "Property information", ["Purchase"] = "Purchase information", ["Lot"] = "Lot and dimensions",
        ["Value"] = "Value and pricing", ["Utilities"] = "Utilities", ["Links"] = "County links",
    };

    public override string Title => $"{Sections[Section]} · {Item.Title}";
    public OwnedPropertyViewModel Item { get; } = item;
    public string Section { get; } = section;

    /// <summary>The Open buttons beside the link boxes.</summary>
    public ICommand OpenLinkCommand { get; } = openLinkCommand;

    [RelayCommand]
    private void Close() => CompleteDialog(true);
}
