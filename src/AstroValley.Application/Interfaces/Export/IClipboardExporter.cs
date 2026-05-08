using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Export;

public interface IClipboardExporter : IExporter<IEnumerable<PropertyRecord>, string?>
{
}
