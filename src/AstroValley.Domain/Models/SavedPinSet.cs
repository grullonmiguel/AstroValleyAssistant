using System.Globalization;

namespace AstroValley.Domain.Models;

/// <summary>One automatically saved file of pins (see <see cref="MarkerLocation.SetName"/>), as listed in the Saved Pins dialog.</summary>
public record SavedPinSet(string Name, string? State, string? County, DateTime? AuctionDate, int PinCount, DateTime SavedAt)
{
    /// <summary>"Miami-Dade County, FL"; the file name when the pins carry no place.</summary>
    public string Title => string.Join(", ", new[] { County, State }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } place ? place : Name;

    /// <summary>"Auction Dec 10, 2026 · 42 pins · saved Nov 10, 2026 3:15 PM"</summary>
    public string Summary => string.Join(" · ",
        AuctionDate is { } d ? $"Auction {d.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}" : "No auction date",
        PinCount == 1 ? "1 pin" : $"{PinCount} pins",
        $"saved {SavedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)}");
}
