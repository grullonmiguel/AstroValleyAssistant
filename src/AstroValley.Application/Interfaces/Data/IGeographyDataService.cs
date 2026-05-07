using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Data;

public interface IGeographyDataService
{
    Task<List<CountyInfo>> GetCountiesForStateAsync(string? stateAbbreviation);
}
