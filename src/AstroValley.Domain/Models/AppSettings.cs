namespace AstroValley.Domain.Models;

public class AppSettings
{
    public RegridSettings Regrid { get; set; } = new();
    public RealAuctionSettings RealAuction { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
}

public class RegridSettings
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RealAuctionSettings
{
    public string Url { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string LastAuctionDate { get; set; } = string.Empty;
}

public class ThemeSettings
{
    public string Name { get; set; } = "Light-Purple";
}

