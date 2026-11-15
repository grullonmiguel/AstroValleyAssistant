using AstroValley.Application.Interfaces.Settings;
using AstroValley.Domain.Models;
using AstroValley.Presentation.Helpers;
using AstroValley.Presentation.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace AstroValley.Presentation.Services;

/// <summary>
/// The one settings store. Implements <see cref="ISettingsService"/> for the shell/theme, and the older
/// <see cref="IRegridSettings"/> / <see cref="IRealAuctionSettings"/> so Application-layer consumers keep working.
/// </summary>
public sealed class SettingsService : ISettingsService, IRegridSettings, IRealAuctionSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _filePath = Path.Combine(AppDefaults.DataFolder, "settings.json");

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDefaults.DataFolder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save settings to {Path}", _filePath);
        }
    }

    // ── IRegridSettings ──────────────────────────────────────────────────
    public string RegridUserName { get => Current.Regrid.UserName; set => Current.Regrid.UserName = value; }
    public string RegridPassword { get => Current.Regrid.Password; set => Current.Regrid.Password = value; }

    // ── IRealAuctionSettings ─────────────────────────────────────────────
    public string Url { get => Current.RealAuction.Url; set => Current.RealAuction.Url = value; }
    public List<string> RecentUrls => Current.RealAuction.RecentUrls;
    public Dictionary<string, int> RecentCounts => Current.RealAuction.RecentCounts;

    private AppSettings Load()
    {
        // One-time carry-over of the pre-unification file (credentials, recent URLs, seller terms).
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValley", "appsettings.user.json");
        var path = File.Exists(_filePath) ? _filePath : legacy;

        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read settings from {Path}; using defaults", path);
        }

        return new AppSettings();
    }
}
