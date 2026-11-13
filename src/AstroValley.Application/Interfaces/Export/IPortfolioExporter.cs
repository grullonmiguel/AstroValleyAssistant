using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Export;

/// <summary>Writes the owned properties (My Properties) to an Excel workbook: the list, every expense line, and spending by year.</summary>
public interface IPortfolioExporter : IExporter<IEnumerable<OwnedProperty>, string>
{
}
