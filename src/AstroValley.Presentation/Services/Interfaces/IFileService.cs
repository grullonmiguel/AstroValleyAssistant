namespace AstroValley.Presentation.Services.Interfaces;

public interface IFileService
{
    string? OpenFile(string title, string filter);

    /// <summary>Asks where to save; returns the chosen path, or null when cancelled.</summary>
    string? SaveFile(string title, string filter, string fileName);
}
