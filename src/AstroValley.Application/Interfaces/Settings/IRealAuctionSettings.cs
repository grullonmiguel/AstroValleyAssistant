namespace AstroValley.Application.Interfaces.Settings;

public interface IRealAuctionSettings
{
    string Url { get; set; }
    void Save();
}
