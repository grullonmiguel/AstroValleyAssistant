using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Export;

public interface IExcelExporter : IExporter<IEnumerable<PropertyRecord>, string?>
{
}
