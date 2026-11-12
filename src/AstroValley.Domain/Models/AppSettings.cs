namespace AstroValley.Domain.Models;

public class AppSettings
{
    public RegridSettings Regrid { get; set; } = new();
    public RealAuctionSettings RealAuction { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public SellerSettings Seller { get; set; } = new();
}

/// <summary>What My Properties puts on a term sheet and starts a new property with.</summary>
public class SellerSettings
{
    /// <summary>The line under the logo: city, e-mail, phone.</summary>
    public string ContactLine { get; set; } = string.Empty;

    /// <summary>The financing offers a new property starts with; null = the built-in three.</summary>
    public List<AstroValley.Domain.Entities.FinancingTier>? FinancingTiers { get; set; }
}

public class RegridSettings
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RealAuctionSettings
{
    public string Url { get; set; } = string.Empty;
    public List<string> RecentUrls { get; set; } = [];

    /// <summary>Property count from the last scrape of each recent URL (key = URL).</summary>
    public Dictionary<string, int> RecentCounts { get; set; } = [];
}

public class ThemeSettings
{
    public string Name { get; set; } = "Light-Purple";
}

