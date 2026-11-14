using System.Collections.ObjectModel;
using System.IO;
using AstroValley.Application.Interfaces.Data;
using AstroValley.Domain.Models;
using AstroValley.Presentation.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels.Dialogs;

/// <summary>
/// The pins the app saved automatically, one set per auction: put a set back on the map, or delete it.
/// The map page owns the pins, so it supplies <c>addToMap</c> (returns how many were new).
/// </summary>
public partial class SavedPinsViewModel(IPinStore store, IBrowserService browser, Func<IEnumerable<MarkerLocation>, int> addToMap)
    : DialogViewModelBase<bool>
{
    public override string Title => "Saved pins";

    public ObservableCollection<SavedPinSet> Sets { get; } = [.. store.List()];
    public bool IsEmpty => Sets.Count == 0;

    [ObservableProperty]
    private string _status = "Pins are saved here automatically when they reach the map and whenever you work on one.";

    [RelayCommand]
    private void Load(SavedPinSet set) => Try(() =>
    {
        var added = addToMap(store.Load(set.Name));
        Status = added == 0 ? $"{set.Title}: those pins are already on the map." : $"Added {added} pins from {set.Title} to the map.";
    });

    [RelayCommand]
    private void Delete(SavedPinSet set) => Try(() =>
    {
        store.Delete(set.Name);
        Sets.Remove(set);
        OnPropertyChanged(nameof(IsEmpty));
        Status = $"Moved {set.Title} to the Recycle Bin. Pins already on the map stay there.";
    });

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(store.Folder); // nothing saved yet: still show where it will go
        browser.Launch(store.Folder);
    }

    [RelayCommand]
    private void Close() => CompleteDialog(true);

    private void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"That did not work: {ex.Message}";
        }
    }
}
