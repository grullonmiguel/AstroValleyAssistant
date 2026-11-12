using AstroValley.Domain.Entities;

namespace AstroValley.Application.Interfaces.Data;

/// <summary>The properties already bought (My Properties), kept on disk one file per property.</summary>
public interface IOwnedPropertyStore
{
    /// <summary>Where the files are kept; a property's photos and documents go in a folder named by its Id beside its file.</summary>
    string Folder { get; }

    /// <summary>Every readable property, most recently purchased first.</summary>
    IReadOnlyList<OwnedProperty> List();

    /// <summary>Writes the property, replacing an earlier save with the same Id.</summary>
    void Save(OwnedProperty property);

    /// <summary>Reads a property file from anywhere and saves it here as a new property. Throws when the file is not a property.</summary>
    OwnedProperty Import(string path);

    /// <summary>Writes a copy of the property's file to <paramref name="path"/>.</summary>
    void Export(OwnedProperty property, string path);

    /// <summary>Moves the property's file to the Recycle Bin.</summary>
    void Delete(string id);
}
