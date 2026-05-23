namespace AstroValley.Application.Interfaces.Settings;

public interface IRealAuctionSettings
{
    /// <summary>
    /// The current URL for the real auction. This is where the application will fetch data from.
    /// </summary>
    string Url { get; set; }

    /// <summary>
    /// Display a list of most recent URLs
    /// </summary>
    List<string> RecentUrls { get; }

    /// <summary>
    /// Saves the current URL to storage and updates the list of recent URLs.
    /// </summary>
    void Save();
}
