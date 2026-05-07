using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Data;

public interface IRealAuctionDataService
{
    Task InitializeAsync();
    IReadOnlyDictionary<string, List<RealAuctionCountyInfo>> CountyData { get; }
    Task<List<RealAuctionCountyInfo>> GetCountiesForStateAsync(string? stateAbbreviation);
}
