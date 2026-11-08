using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Import;

/// <summary>Reads one provider's spreadsheet layout. Headers and row keys are normalized (lower case, no spaces or underscores).</summary>
public interface IPropertyImporter
{
    string Name { get; }
    bool CanRead(IReadOnlyCollection<string> headers);
    PropertyRecord Map(IReadOnlyDictionary<string, string> row);
}
