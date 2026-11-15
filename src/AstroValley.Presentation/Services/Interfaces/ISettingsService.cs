using AstroValley.Domain.Models;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON in the user's local app data folder.
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    void Save();
}
