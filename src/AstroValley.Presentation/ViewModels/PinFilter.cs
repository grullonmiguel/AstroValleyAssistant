using AstroValley.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace AstroValley.Presentation.ViewModels;

/// <summary>
/// What the Home map's Tools dialog edits: from / to boxes (text, blank = no limit) plus type and undivided.
/// The dialog edits a copy; the view model applies it when the user presses Show.
/// </summary>
public partial class PinFilter : ObservableObject
{
    public static readonly string[] Types = ["All", "Vacant land", "Structures"];

    [ObservableProperty] private string _minBid = "";
    [ObservableProperty] private string _maxBid = "";
    [ObservableProperty] private string _minAssessed = "";
    [ObservableProperty] private string _maxAssessed = "";
    [ObservableProperty] private string _minBidPercent = "";
    [ObservableProperty] private string _maxBidPercent = "";
    [ObservableProperty] private string _minAcres = "";
    [ObservableProperty] private string _maxAcres = "";
    [ObservableProperty] private string _minSlope = "";
    [ObservableProperty] private string _maxSlope = "";
    [ObservableProperty] private string _propertyType = Types[0];
    [ObservableProperty] private bool _undividedOnly;

    public PinFilter Clone() => new()
    {
        MinBid = MinBid, MaxBid = MaxBid, MinAssessed = MinAssessed, MaxAssessed = MaxAssessed,
        MinBidPercent = MinBidPercent, MaxBidPercent = MaxBidPercent, MinAcres = MinAcres, MaxAcres = MaxAcres,
        MinSlope = MinSlope, MaxSlope = MaxSlope, PropertyType = PropertyType, UndividedOnly = UndividedOnly
    };

    // Pins missing the number a filled-in range needs (no bid, no assessed value, no acreage...) are hidden by that range.
    public bool Matches(MarkerLocation m) =>
        In(m.BidAmount is { } bid ? (double)bid : null, MinBid, MaxBid)
        && In(m.AssessedAmount is { } assessed ? (double)assessed : null, MinAssessed, MaxAssessed)
        && In(m.BidToAssessedPercent, MinBidPercent, MaxBidPercent)
        && In(double.TryParse(m.Acres, CultureInfo.InvariantCulture, out var acres) ? acres : null, MinAcres, MaxAcres)
        && In(m.SlopePercent, MinSlope, MaxSlope)
        && (PropertyType == Types[0] || m.PropertyType == PropertyType)
        && (!UndividedOnly || m.UndividedInterest);

    private static bool In(double? value, string min, string max)
    {
        var (lo, hi) = (Number(min), Number(max));
        if (lo is null && hi is null) return true;
        return value is { } v && (lo is null || v >= lo) && (hi is null || v <= hi);
    }

    // "$5,000" and "5000" both work; anything unreadable counts as blank
    private static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out var n) ? n : null;
}
