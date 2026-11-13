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

public enum BuyerType { Builder, Investor, Neighbor, Individual, Other }

/// <summary>One step of getting a property ready to sell and sold (see <see cref="OwnedProperty.NewSaleChecklist"/>).</summary>
public record SaleStep
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Done { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>Someone to call about the property: the Register of Deeds, the Treasurer, a title company.</summary>
public record Contact
{
    public string Office { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>A place the property is advertised. A price change is a new line, so the history stays.</summary>
public record Listing
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Site { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>Someone who asked about the property or made an offer.</summary>
public record BuyerLead
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public BuyerType Type { get; set; } = BuyerType.Individual;
    public decimal? Offer { get; set; }
    public bool Financed { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>One owner-financing offer: how much down, at what rate, over how many months.</summary>
public record FinancingTier
{
    public FinancingTier() { }
    public FinancingTier(string name, decimal downPercent, decimal apr, int months) => (Name, DownPercent, Apr, Months) = (name, downPercent, apr, months);

    public string Name { get; set; } = string.Empty;
    public decimal DownPercent { get; set; }
    public decimal Apr { get; set; }
    public int Months { get; set; }

    /// <summary>The terms on the current term sheet.</summary>
    public static List<FinancingTier> Defaults() => [new("Entry", 20, 9.5m, 30), new("Standard", 25, 8.5m, 18), new("Premium", 30, 7.9m, 12)];

    public FinancingQuote Quote(decimal price)
    {
        var down = Math.Round(price * DownPercent / 100, 2, MidpointRounding.AwayFromZero);
        var financed = price - down;
        var rate = (double)Apr / 1200;
        // level payment; at 0% (or a nonsense term) it is the balance split evenly
        var monthly = Months <= 0 ? 0 : rate <= 0 ? financed / Months : financed * (decimal)(rate / (1 - Math.Pow(1 + rate, -Months)));
        monthly = Math.Round(monthly, 2, MidpointRounding.AwayFromZero);
        var payments = monthly * Math.Max(Months, 0);
        return new FinancingQuote(Name, DownPercent, Apr, Months, price, down, financed, monthly, down + payments, Math.Max(0, payments - financed));
    }
}

public record FinancingQuote(string Name, decimal DownPercent, decimal Apr, int Months, decimal Price,
    decimal DownPayment, decimal Financed, decimal Monthly, decimal TotalReceived, decimal TotalInterest);

/// <summary>What a sale at one price would leave over everything spent.</summary>
public record PriceScenario(string Label, decimal Price, decimal Profit, decimal? RoiPercent);

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

    /// <summary>Where the property is on the way to sold; one of <see cref="Statuses"/>.</summary>
    public string Status { get; init; } = Statuses[0];
    public static readonly string[] Statuses = ["Acquired", "Title / prep", "Listed", "Offer", "Under contract", "Sold (cash)", "Sold (financed)"];
    [JsonIgnore] public bool IsSold => Status.StartsWith("Sold", StringComparison.OrdinalIgnoreCase);

    public string Notes { get; init; } = string.Empty;

    // --- Selling ---
    public List<SaleStep> SaleChecklist { get; init; } = NewSaleChecklist();
    public List<Contact> Contacts { get; init; } = [];
    public List<Listing> Listings { get; init; } = [];
    public List<BuyerLead> Buyers { get; init; } = [];

    public decimal? ConservativeValue { get; init; }            // MarketValue is the base case between these two
    public decimal? OptimisticValue { get; init; }
    public decimal? NegotiationTarget { get; init; }

    public List<FinancingTier> FinancingTiers { get; init; } = FinancingTier.Defaults();
    public decimal? FinancingPrice { get; init; }               // blank = the asking price, else the market value

    public DateTime? SaleDate { get; init; }
    public decimal? SalePrice { get; init; }
    public decimal SaleClosingCosts { get; init; }

    /// <summary>The steps from deed in hand to listed, in the order to do them. Keys are saved; add new steps at the end of their group with a new key.</summary>
    public static List<SaleStep> NewSaleChecklist() =>
    [
        new() { Key = "deed-recorded", Title = "Deed recorded with the Register of Deeds (get the liber/page or instrument number)" },
        new() { Key = "deed-copy", Title = "Certified copy of the deed in hand" },
        new() { Key = "title-review", Title = "Title company reviewed the auction deed and foreclosure history" },
        new() { Key = "quiet-title", Title = "Quiet title: confirmed not needed, or completed" },
        new() { Key = "taxes-current", Title = "Taxes, special assessments and municipal charges current (ask the Treasurer)" },
        new() { Key = "zoning", Title = "Zoning district confirmed with Planning and Zoning" },
        new() { Key = "buildable", Title = "Buildable lot confirmed in writing (width, area, setbacks)" },
        new() { Key = "utilities", Title = "Utilities confirmed: at the street or connected, and the connection fees" },
        new() { Key = "cleanup", Title = "Lot cleaned: brush, weeds and trash" },
        new() { Key = "photos", Title = "Photos taken: front, side street, opposite corner, neighbors" },
        new() { Key = "parcel-map", Title = "Parcel map or survey ready for buyers" },
        new() { Key = "price", Title = "Asking, target and quick-sale prices set from comps" },
        new() { Key = "listed", Title = "Listing published" },
        new() { Key = "outreach", Title = "Builders, investors and neighbors contacted (15 to 20)" },
    ];

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

    // --- To Claude (the property-intel skill) ---

    /// <summary>
    /// What the property-intel skill needs to plan the sale, as text for the clipboard: the facts on file, the money spent, and the
    /// shape of the JSON the app can paste back (see IOwnedPropertyStore.ApplyResearch). Empty facts are left out, not guessed.
    /// </summary>
    public string BriefForClaude()
    {
        var us = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var r = Record;
        var lines = new List<string> { "Use the property-intel skill in Exit mode. I own this property and want to sell it.", "" };
        void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value.Trim()}"); }
        static string Words(UtilityStatus s) => s switch { UtilityStatus.AtStreet => "at the street", UtilityStatus.Connected => "connected", UtilityStatus.No => "not available", _ => "" };

        Add("Address", string.Join(", ", new[] { r.Address, r.City, $"{r.State} {r.Zip}".Trim() }.Where(s => s.Length > 0)));
        Add("Parcel ID", r.ParcelId);
        Add("County", r.County);
        Add("Property type", r.PropertyType);
        Add("Lot", string.Join(", ", new[]
        {
            LotWidthFt is { } w && LotDepthFt is { } d ? string.Create(us, $"{w:0.##} x {d:0.##} ft ({LotSqft:N0} sq ft)") : null,
            Acres is { } acres ? string.Create(us, $"{acres:0.####} acres") : null,
            CornerLot ? "corner lot" : null,
            RoadFrontage switch { true => "road frontage", false => "no road frontage", _ => null },
        }.Where(s => s is not null)));
        Add("Zoning", $"{r.ZoningCode} {r.ZoningType}");
        Add("Flood zone", r.FloodZone);
        Add("Easements", Easements);
        Add("Utilities", string.Join(", ", new (string Name, UtilityStatus Status)[] { ("water", Water), ("sewer", Sewer), ("electricity", Electricity), ("gas", Gas), ("phone", Phone) }
            .Where(u => u.Status != UtilityStatus.Unknown).Select(u => $"{u.Name} {Words(u.Status)}")));
        if (r.LivingSqft is not null || r.Beds is not null || r.YearBuilt is not null)
            Add("House", string.Join(", ", new[] { r.HomeType, r.Beds is { } b ? $"{b} beds" : null, r.Baths is { } ba ? $"{ba} baths" : null,
                r.LivingSqft is { } sq ? string.Create(us, $"{sq:N0} sq ft") : null, r.YearBuilt is { } y ? $"built {y}" : null }.Where(s => !string.IsNullOrWhiteSpace(s))));
        Add("Deed", string.Join(", ", new[] { DeedType, DeedRecordedDate is { } rec ? string.Create(us, $"recorded {rec:d}") : DeedType.Length > 0 ? "not recorded yet, or not confirmed" : "", DeedInstrument }.Where(s => s.Length > 0)));
        Add("Bought", string.Join(", ", new[] { PurchaseDate is { } pd ? string.Create(us, $"{pd:d}") : null, LotNumber.Length > 0 ? $"auction lot {LotNumber}" : null, PurchasedBy.Length > 0 ? $"by {PurchasedBy}" : null }.Where(s => s is not null)));
        if (TotalSpent > 0) Add("Total spent so far", string.Create(us, $"{TotalSpent:C} (purchase {PurchaseTotal:C}, later expenses {ExpensesTotal:C})"));
        if (r.AssessedValue is { } assessed) Add("Assessed value", assessed.ToString("C0", us));
        if (MarketValue is { } mv) Add("My market value so far", mv.ToString("C0", us));
        if (r.Comps is { EstimatedValue: { } cv } comps) Add("My comps", string.Create(us, $"{cv:C0} from {comps.Count} comps"));
        if (AskingPrice is { } ask) Add("Asking price", ask.ToString("C0", us));
        Add("Status", Status);
        if (Neighbors.Count > 0) Add("Neighbors", string.Join("; ", Neighbors.Select(n => string.Join(", ", new[] { n.Name, n.PropertyAddress }.Where(s => s.Length > 0)))));
        Add("Notes", Notes);

        lines.Add("");
        lines.Add("Give me the exit strategy. Then, at the end, add one JSON block with what you found so I can paste it into my app.");
        lines.Add("Include only what you verified or are recommending; leave the rest out. Shape:");
        lines.Add(ResearchJsonShape);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The fields the app accepts back from Claude; also documented in the property-intel skill.</summary>
    public const string ResearchJsonShape = """
        {
          "Overview": "one paragraph about the property",
          "LocationOverview": "schools, parks, shopping nearby",
          "ListingAd": "the ready-to-post ad",
          "ConservativeValue": 5000, "MarketValue": 7500, "OptimisticValue": 10000,
          "AskingPrice": 9900, "NegotiationTarget": 8000, "QuickSalePrice": 5500,
          "LotWidthFt": 88, "LotDepthFt": 120, "CornerLot": true, "RoadFrontage": true, "Easements": "none found",
          "Water": "AtStreet", "Sewer": "AtStreet", "Electricity": "AtStreet", "Gas": "Unknown", "Phone": "Unknown",
          "GisUrl": "https://...", "PropertyDetailUrl": "https://...",
          "Record": { "ParcelId": "", "County": "", "ZoningCode": "", "ZoningType": "", "FloodZone": "", "Owner": "", "LegalDescription": "", "AssessedValue": 0 },
          "Neighbors": [ { "Name": "", "PropertyAddress": "", "MailingAddress": "", "Note": "" } ],
          "Contacts": [ { "Office": "Register of Deeds", "Phone": "", "Note": "what to ask" } ],
          "Buyers": [ { "Name": "a local builder", "Contact": "", "Type": "Builder", "Status": "To contact", "Note": "" } ]
        }
        """;

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

    /// <summary>Days from purchase to the sale, or to today while it is still held.</summary>
    public int? DaysHeld(DateTime today) => PurchaseDate is { } d ? Math.Max(0, ((SaleDate ?? today).Date - d.Date).Days) : null;

    // --- Selling: pricing, financing and the sale itself ---

    /// <summary>Each price that has been set, with the profit and return it would leave over everything spent (before closing costs).</summary>
    [JsonIgnore] public IReadOnlyList<PriceScenario> PriceScenarios => [.. new (string Label, decimal? Price)[]
        {
            ("Optimistic value", OptimisticValue), ("Asking price", AskingPrice), ("Market value (base)", MarketValue), ("Negotiation target", NegotiationTarget),
            ("Conservative value", ConservativeValue), ("Quick-sale price", QuickSalePrice), ("Minimum sale", MinimumSale),
        }
        .Where(s => s.Price is > 0)
        .Select(s => new PriceScenario(s.Label, s.Price!.Value, s.Price.Value - TotalSpent, GainPercent(s.Price)))];

    /// <summary>The price the financing offers are worked out on.</summary>
    [JsonIgnore] public decimal? FinancingBasis => FinancingPrice ?? AskingPrice ?? MarketValue;

    [JsonIgnore] public IReadOnlyList<FinancingQuote> FinancingQuotes =>
        FinancingBasis is > 0 and var price ? [.. FinancingTiers.Where(t => t.Months > 0).Select(t => t.Quote(price))] : [];

    [JsonIgnore] public decimal? NetProceeds => SalePrice - SaleClosingCosts;
    [JsonIgnore] public decimal? NetProfit => NetProceeds - TotalSpent;
    [JsonIgnore] public decimal? NetRoiPercent => GainPercent(NetProceeds);

    /// <summary>The net return as a yearly rate; null when sold within a month (a yearly rate from a few days means nothing) or at a total loss.</summary>
    [JsonIgnore] public decimal? AnnualizedRoiPercent =>
        NetProceeds is > 0 and var net && TotalSpent > 0 && SaleDate is { } sold && DaysHeld(sold) is >= 30 and var days
            && Math.Pow((double)(net / TotalSpent), 365.0 / days) - 1 is var rate && rate < 1000
            ? Math.Round((decimal)rate * 100, 2) : null;

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

        // the term sheet in use today: $12,200 at Entry / Standard / Premium
        var quotes = (p with { AskingPrice = null }).FinancingQuotes;
        Debug.Assert(quotes.Count == 3 && quotes[0] is { Name: "Entry", DownPayment: 2440m, Financed: 9760m, Monthly: 366.77m }
            && quotes[1] is { DownPayment: 3050m, Financed: 9150m, Monthly: 543.22m } && quotes[2] is { DownPayment: 3660m, Financed: 8540m, Monthly: 742.49m },
            "financing payments match the term sheet");
        Debug.Assert(quotes[0].TotalReceived == 2440m + 366.77m * 30 && quotes[0].TotalInterest == 366.77m * 30 - 9760m, "total received and interest");
        Debug.Assert(new FinancingTier("Cash-like", 20, 0, 10).Quote(10000m) is { Monthly: 800m, TotalInterest: 0m } && new FinancingTier("Broken", 20, 9, 0).Quote(10000m).Monthly == 0m, "0% and a zero term do not divide by zero");
        Debug.Assert((p with { FinancingPrice = 10500m, AskingPrice = 9900m }).FinancingBasis == 10500m && (p with { AskingPrice = 9900m }).FinancingBasis == 9900m && new OwnedProperty().FinancingQuotes.Count == 0, "financing price, else asking, else market value");

        var priced = p with { AskingPrice = 9900m, QuickSalePrice = 5500m };
        Debug.Assert(priced.PriceScenarios.Select(s => s.Label).SequenceEqual(["Asking price", "Market value (base)", "Quick-sale price", "Minimum sale"])
            && priced.PriceScenarios[0] is { Profit: 7750.78m, RoiPercent: 360.63m }, "a profit line for each price that is set");

        var sold = later with { SaleDate = new DateTime(2026, 9, 4), SalePrice = 9000m, SaleClosingCosts = 640m, Status = "Sold (cash)" };
        Debug.Assert(sold.NetProceeds == 8360m && sold.NetProfit == 6000m && sold.NetRoiPercent == Math.Round(6000m / 2360m * 100, 2) && sold.IsSold && !later.IsSold, "net profit after closing costs and every expense");
        Debug.Assert(sold.DaysHeld(new DateTime(2030, 1, 1)) == 365 && sold.AnnualizedRoiPercent == sold.NetRoiPercent, "held exactly a year: the yearly rate is the return, and the clock stops at the sale");
        Debug.Assert((sold with { SaleDate = new DateTime(2025, 9, 10) }).AnnualizedRoiPercent is null && later.NetProfit is null, "no yearly rate for a week, no profit before a sale");
        Debug.Assert(NewSaleChecklist().Select(s => s.Key).Distinct().Count() == NewSaleChecklist().Count, "checklist keys are unique");

        var brief = (p with { Record = p.Record with { City = "Saginaw", State = "MI", Zip = "48602", ZoningCode = "R-2" }, CornerLot = true, Water = UtilityStatus.AtStreet }).BriefForClaude();
        Debug.Assert(brief.Contains("Address: 2916 Mackinaw St, Saginaw, MI 48602") && brief.Contains("88 x 120 ft (10,560 sq ft), 0.2424 acres, corner lot") && brief.Contains("Utilities: water at the street")
            && brief.Contains("Total spent so far: $2,149.22") && brief.Contains("\"ListingAd\"") && !brief.Contains("Flood zone") && !brief.Contains("Deed:"), "the brief states what is on file and leaves the rest out");

        var blank = new OwnedProperty();
        Debug.Assert(blank.TotalSpent == 0 && blank.MarketGainPercent is null && blank.MinimumSale is null && blank.CostPerAcre is null && blank.Acres is null && blank.DaysHeld(DateTime.Today) is null,
            "a blank property computes nothing instead of dividing by zero");
        Debug.Assert((new OwnedProperty { Record = new PropertyRecord { Acres = 0.24 } }).Acres == 0.24, "scraped acres until the dimensions are typed");
    }
}
