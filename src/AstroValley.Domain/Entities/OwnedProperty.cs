using System.Diagnostics;
using System.Text.Json.Serialization;

namespace AstroValley.Domain.Entities;

public enum ExpenseCategory { PropertyTax, DeedRecording, Title, Survey, GrassCutCleanup, Pictures, Flyers, Signs, ListingFees, Insurance, Utilities, Legal, Other }

public enum UtilityStatus { Unknown, AtStreet, Connected, No }

/// <summary>Money spent on a property after the purchase: a tax bill, a grass cut, a sign.</summary>
public record Expense
{
    public Expense() { } // settable and parameterless: the expenses grid adds and edits lines in place
    public Expense(DateTime date, ExpenseCategory category, decimal amount, string note = "", int? taxYear = null) =>
        (Date, Category, Amount, Note, TaxYear) = (date, category, amount, note, taxYear);

    public DateTime Date { get; set; } = DateTime.Today;
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public decimal Amount { get; set; }
    public string Note { get; set; } = string.Empty;
    public int? TaxYear { get; set; }
}

/// <summary>An adjacent owner: a title clue and one of the first buyers to call.</summary>
public record Neighbor
{
    public Neighbor() { }
    public Neighbor(string name, string propertyAddress = "") => (Name, PropertyAddress) = (name, propertyAddress);

    public string Name { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string MailingAddress { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// A property already bought (My Properties): the scraped <see cref="Record"/> plus what it cost, what it keeps costing and how it is priced.
/// Saved one file per property, named by <see cref="Id"/>.
/// </summary>
public record OwnedProperty
{
    /// <summary>The file name; never changes, so editing the parcel or address cannot orphan the file.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public PropertyRecord Record { get; init; } = new();

    // --- Identity ---
    public string LotNumber { get; init; } = string.Empty;      // the auction's lot, e.g. "8194"
    public string PurchasedBy { get; init; } = string.Empty;    // the buying entity
    public string AuctionUrl { get; init; } = string.Empty;
    public string GisUrl { get; init; } = string.Empty;
    public string PropertyDetailUrl { get; init; } = string.Empty;

    // --- Lot ---
    public double? LotWidthFt { get; init; }
    public double? LotDepthFt { get; init; }
    public bool CornerLot { get; init; }
    public bool? RoadFrontage { get; init; }                    // null = not checked
    public string Easements { get; init; } = string.Empty;

    // --- Purchase ---
    public DateTime? PurchaseDate { get; init; }
    public decimal? StartingBid { get; init; }
    public decimal PurchasePrice { get; init; }                 // the winning bid
    public decimal BuyerPremiumPercent { get; init; } = 10;
    public decimal TaxesAtPurchase { get; init; }               // taxes collected with the purchase; later tax bills are Expenses
    public decimal DeedFee { get; init; }
    public string DeedType { get; init; } = string.Empty;
    public DateTime? DeedRecordedDate { get; init; }
    public string DeedInstrument { get; init; } = string.Empty; // liber/page or instrument number

    public List<Expense> Expenses { get; init; } = [];

    // --- Utilities ---
    public UtilityStatus Phone { get; init; }
    public UtilityStatus Electricity { get; init; }
    public UtilityStatus Water { get; init; }
    public UtilityStatus Sewer { get; init; }
    public UtilityStatus Gas { get; init; }

    public List<Neighbor> Neighbors { get; init; } = [];

    // --- Pricing ---
    public decimal? MarketValue { get; init; }
    public decimal MinimumSalePercent { get; init; } = 50;      // of market value
    public decimal? AskingPrice { get; init; }
    public decimal? QuickSalePrice { get; init; }

    // --- Marketing text ---
    public string Overview { get; init; } = string.Empty;
    public string LocationOverview { get; init; } = string.Empty;
    public string ListingAd { get; init; } = string.Empty;

    public string Status { get; init; } = "Acquired";
    public string Notes { get; init; } = string.Empty;

    // --- To and from the Home map ---

    public const string PinColor = "#C9A227"; // gold: owned, as opposed to the auction colours

    /// <summary>The Home map pin for this property; null until it has coordinates (scrape it, or type them).</summary>
    public AstroValley.Domain.Models.MarkerLocation? ToPin()
    {
        // an owned property has no auction ahead of it: no date badge, no minimum bid
        var pin = AstroValley.Domain.Models.MarkerLocation.FromRecord(Record with { Acres = Acres, AuctionDate = default, OpeningBid = null }, AuctionUrl);
        if (pin is null) return null;
        pin = pin with
        {
            OwnedId = Id, PinColor = PinColor,
            Address = Record.Address is { Length: > 0 } a ? a : Record.ParcelId,
            AppraiserUrl = PropertyDetailUrl is { Length: > 0 } url ? url : pin.AppraiserUrl,
        };
        pin.Notes = Notes.Length > 0 ? Notes : null;
        return pin;
    }

    /// <summary>A property just bought, from its auction row: the research done on it comes along.</summary>
    public static OwnedProperty FromRecord(PropertyRecord r, string? auctionUrl, DateTime today) => new()
    {
        Record = r with { PotentialMatches = [] },
        StartingBid = r.OpeningBid,
        AuctionUrl = auctionUrl ?? "",
        PropertyDetailUrl = r.AppraiserUrl,
        PurchaseDate = r.AuctionDate.Year > 1 && r.AuctionDate <= today ? r.AuctionDate : today,
    };

    /// <summary>A property just bought, from its Home map pin (which keeps its details as text).</summary>
    public static OwnedProperty FromPin(AstroValley.Domain.Models.MarkerLocation pin, DateTime today)
    {
        var d = pin.ExtraDetails ?? [];
        string Text(string key) => d.GetValueOrDefault(key) ?? "";
        int? Int(string key) => int.TryParse(Text(key), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        double? Num(string? text) => double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return FromRecord(new PropertyRecord
        {
            ParcelId = pin.ParcelID ?? "", Address = pin.Address == "N/A" ? "" : pin.Address,
            City = Text("City"), County = pin.County ?? "", State = pin.State ?? "", Zip = Text("Zip"),
            Acres = Num(pin.Acres), Owner = Text("Owner"), OtherOwners = Text("Other Owners"),
            ZoningCode = Text("Zoning"), ZoningType = Text("Type"), FloodZone = Text("Flood Zone"),
            LegalDescription = Text("Legal Description"), PropertyType = Text("Property Type"),
            ElevationLow = Text("Elevation Low"), ElevationHigh = Text("Elevation High"),
            Latitude = pin.Latitude.ToString(inv), Longitude = pin.Longitude.ToString(inv), ParcelLines = pin.ParcelLines,
            RegridUrl = pin.RegridUrl ?? "", AppraiserUrl = pin.AppraiserUrl ?? "",
            AssessedValue = pin.AssessedAmount, OpeningBid = pin.BidAmount,
            AuctionDate = pin.AuctionDate ?? default,
            HomeType = Text("Home Type"), UseDescription = Text("Land Use"),
            Beds = Int("Beds"), Baths = Num(Text("Baths")), LivingSqft = Int("Living Sqft"), Units = Int("Units"),
            YearBuilt = Int("Year Built"), BuildingCount = Int("Buildings"),
            Comps = pin.Comps,
        }, pin.AuctionUrl, today) with { Notes = pin.Notes ?? "" };
    }

    // --- Computed; never saved, never typed ---

    /// <summary>Square feet from the dimensions; null until both are known.</summary>
    [JsonIgnore] public double? LotSqft => LotWidthFt * LotDepthFt;

    /// <summary>Acres from the dimensions when known, else the scraped figure.</summary>
    [JsonIgnore] public double? Acres => LotSqft is { } sqft ? Math.Round(sqft / 43560, 4) : Record.Acres;

    [JsonIgnore] public decimal BuyerPremium => Math.Round(PurchasePrice * BuyerPremiumPercent / 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>What the purchase itself cost: bid, premium, taxes collected with it and the deed fee.</summary>
    [JsonIgnore] public decimal PurchaseTotal => PurchasePrice + BuyerPremium + TaxesAtPurchase + DeedFee;

    [JsonIgnore] public decimal ExpensesTotal => Expenses.Sum(e => e.Amount);

    [JsonIgnore] public decimal TotalSpent => PurchaseTotal + ExpensesTotal;

    /// <summary>Gain over everything spent if sold at market value, as on the tracking sheet ("468%"); null without a market value or any spend.</summary>
    [JsonIgnore] public decimal? MarketGainPercent => GainPercent(MarketValue);

    [JsonIgnore] public decimal? MinimumSale => MarketValue is { } mv ? Math.Round(mv * MinimumSalePercent / 100, 2, MidpointRounding.AwayFromZero) : null;

    [JsonIgnore] public decimal? MinimumSaleGainPercent => GainPercent(MinimumSale);

    [JsonIgnore] public decimal? CostPerAcre => Acres is > 0 and var a ? Math.Round(TotalSpent / (decimal)a, 2) : null;

    public int? DaysHeld(DateTime today) => PurchaseDate is { } d ? Math.Max(0, (today.Date - d.Date).Days) : null;

    /// <summary>Spending after the purchase, per month held (a month = 30.44 days); null before a full month.</summary>
    public decimal? CostPerMonthHeld(DateTime today) =>
        DaysHeld(today) is >= 30 and var days ? Math.Round(ExpensesTotal / (days / 30.44m), 2) : null;

    private decimal? GainPercent(decimal? salePrice) =>
        salePrice is { } price && TotalSpent > 0 ? Math.Round((price - TotalSpent) / TotalSpent * 100, 2) : null;

    /// <summary>Assert-based check (no test project yet); run by tools/RegridSampleCheck. Figures are the Mackinaw St lot from the tracking sheet.</summary>
    public static void SelfCheck()
    {
        var p = new OwnedProperty
        {
            Record = new PropertyRecord { Address = "2916 Mackinaw St", Acres = 0.24 },
            LotWidthFt = 88, LotDepthFt = 120,
            PurchaseDate = new DateTime(2025, 9, 4), PurchasePrice = 1800m, TaxesAtPurchase = 139.22m, DeedFee = 30m,
            MarketValue = 12200m, MinimumSalePercent = 80,
        };
        Debug.Assert(p.BuyerPremium == 180m && p.PurchaseTotal == 2149.22m && p.TotalSpent == 2149.22m, "purchase total matches the sheet");
        Debug.Assert(p.LotSqft == 10560 && p.Acres == 0.2424, "dimensions give the area");
        Debug.Assert(p.MarketGainPercent == 467.65m && p.MinimumSale == 9760m && p.MinimumSaleGainPercent == 354.12m, "gain percentages match the sheet");

        var later = p with { Expenses = [new(new DateTime(2026, 2, 14), ExpenseCategory.PropertyTax, 150m, taxYear: 2025), new(new DateTime(2026, 6, 1), ExpenseCategory.GrassCutCleanup, 60.78m)] };
        Debug.Assert(later.ExpensesTotal == 210.78m && later.TotalSpent == 2360m && later.PurchaseTotal == 2149.22m, "expenses add to the total, not to the purchase");
        Debug.Assert(later.CostPerAcre == Math.Round(2360m / 0.2424m, 2), "cost per acre uses everything spent");
        Debug.Assert(later.DaysHeld(new DateTime(2025, 10, 4)) == 30 && later.DaysHeld(new DateTime(2025, 1, 1)) == 0, "days held");
        Debug.Assert(later.CostPerMonthHeld(new DateTime(2026, 9, 4)) == Math.Round(210.78m / (365 / 30.44m), 2) && later.CostPerMonthHeld(new DateTime(2025, 9, 20)) is null, "monthly cost");

        // auction row -> Home pin -> bought -> owned pin: the same place on the map, now gold and tied to the property
        var row = new PropertyRecord
        {
            ParcelId = "20 1952 00000", Address = "2916 Mackinaw St", City = "Saginaw", State = "MI", County = "Saginaw County", Zip = "48602",
            Latitude = "43.4195", Longitude = "-83.9812", Acres = 0.24, ZoningCode = "R-2", Owner = "SAGINAW COUNTY TREASURER",
            AssessedValue = 3500m, OpeningBid = 1041.13m, AuctionDate = new DateTime(2025, 9, 4), AppraiserUrl = "https://example.com/parcel",
        };
        var auctionPin = AstroValley.Domain.Models.MarkerLocation.FromRecord(row, "https://example.com/auction")!;
        auctionPin.Notes = "corner lot";
        var bought = FromPin(auctionPin, new DateTime(2025, 9, 10));
        Debug.Assert(bought.Record.ParcelId == row.ParcelId && bought.Record.Zip == "48602" && bought.Record.ZoningCode == "R-2" && bought.Record.AssessedValue == 3500m
            && bought.StartingBid == 1041.13m && bought.PurchaseDate == row.AuctionDate && bought.AuctionUrl == "https://example.com/auction"
            && bought.PropertyDetailUrl == "https://example.com/parcel" && bought.Notes == "corner lot", "a bought pin keeps its research");
        var ownedPin = bought.ToPin()!;
        Debug.Assert(ownedPin.Key == auctionPin.Key && ownedPin.OwnedId == bought.Id && ownedPin.PinColor == PinColor && ownedPin.AuctionDate is null
            && ownedPin.BidAmount is null && ownedPin.Notes == "corner lot" && auctionPin.OwnedId is null, "the owned pin replaces the auction pin");
        Debug.Assert(FromRecord(row with { AuctionDate = new DateTime(2030, 1, 1) }, null, new DateTime(2025, 9, 10)).PurchaseDate == new DateTime(2025, 9, 10), "a future auction date is not a purchase date");
        Debug.Assert(new OwnedProperty { Record = new PropertyRecord { Address = "no coordinates" } }.ToPin() is null, "no coordinates, no pin");

        var blank = new OwnedProperty();
        Debug.Assert(blank.TotalSpent == 0 && blank.MarketGainPercent is null && blank.MinimumSale is null && blank.CostPerAcre is null && blank.Acres is null && blank.DaysHeld(DateTime.Today) is null,
            "a blank property computes nothing instead of dividing by zero");
        Debug.Assert((new OwnedProperty { Record = new PropertyRecord { Acres = 0.24 } }).Acres == 0.24, "scraped acres until the dimensions are typed");
    }
}
