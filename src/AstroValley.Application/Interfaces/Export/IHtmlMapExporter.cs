using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Export;

public interface IHtmlMapExporter : IExporter<IEnumerable<MarkerLocation>, string?>
{
}
