namespace AstroValleyAssistant.Models
{
    public record AuctionRecord(
        string ParcelId,
        string PropertyAddress,
        decimal OpeningBid,
        decimal? AssessedValue,
        DateTime AuctionDate,
        int PageNumber,
        string AppraiserUrl
    );
}