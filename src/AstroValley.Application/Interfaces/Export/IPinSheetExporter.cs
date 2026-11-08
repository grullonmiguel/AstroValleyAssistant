using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Export;

/// <summary>Writes pins to an Excel research sheet (one row per pin, with blank columns for the manual research steps).</summary>
public interface IPinSheetExporter : IExporter<IEnumerable<MarkerLocation>, string>
{
}
