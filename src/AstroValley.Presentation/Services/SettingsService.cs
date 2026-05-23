using AstroValley.Application.Interfaces.Settings;
using AstroValley.Domain.Models;
using System.IO;
using System.Text.Json;

namespace AstroValley.Presentation.Services;

public class SettingsService : IRegridSettings, IRealAuctionSettings
{
    private readonly string _filePath;
    private AppSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValley");

        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "appsettings.user.json");
        _settings = Load();
    }

    // ── IRegridSettings ──────────────────────────────────────────────────
    public string RegridUserName
    {
        get => _settings.Regrid.UserName;
        set => _settings.Regrid.UserName = value;
    }

    public string RegridPassword
    {
        get => _settings.Regrid.Password;
        set => _settings.Regrid.Password = value;
    }

    // ── IRealAuctionSettings ─────────────────────────────────────────────
    public string Url
    {
        get => _settings.RealAuction.Url;
        set => _settings.RealAuction.Url = value;
    }

    public List<string> RecentUrls => _settings.RealAuction.RecentUrls;

    // ── Theme (used by ThemeService) ─────────────────────────────────────
    public string ThemeName
    {
        get => _settings.Theme.Name;
        set => _settings.Theme.Name = value;
    }

    // ── Persistence ──────────────────────────────────────────────────────
    public void Save()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
