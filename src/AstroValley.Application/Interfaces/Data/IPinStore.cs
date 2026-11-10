using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Data;

/// <summary>
/// The automatic, on-disk copy of the pins: one set per auction, kept up to date as pins reach the map and are worked on,
/// so a crash or a closed app loses nothing. Each set is a plain pin list, the same format as Export Pins / Import Pins.
/// </summary>
public interface IPinStore
{
    /// <summary>Where the sets are kept.</summary>
    string Folder { get; }

    /// <summary>
    /// Adds the pins to their sets, replacing an earlier save of the same parcel. With <paramref name="restoreWork"/> a pin first
    /// takes the user's work from that earlier save (for pins that have just arrived on the map, e.g. scraped again).
    /// </summary>
    void Save(IEnumerable<MarkerLocation> pins, bool restoreWork = false);

    /// <summary>The saved sets, newest first.</summary>
    IReadOnlyList<SavedPinSet> List();

    IReadOnlyList<MarkerLocation> Load(string setName);

    /// <summary>Moves the set to the Recycle Bin.</summary>
    void Delete(string setName);
}
