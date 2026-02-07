namespace AstroValleyAssistant.Core.Services
{
    public interface IRealAuctionSettings
    {
        string Url { get; set; }
        string State { get; set; }
        string County { get; set; }
        string LastAuctionDate { get; set; }
        void Save();
    }
}
