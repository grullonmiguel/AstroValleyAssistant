using AstroValley.Domain.Models;

namespace AstroValley.Application.Interfaces.Scraping;

public interface IRegridScraper
{
    Task<RegridParcelResult?> GetPropertyDetailsAsync(string data, CancellationToken ct = default);

    Task<RegridParcelResult?> ScrapeParcelFromUrlAsync(string fullUrl, CancellationToken ct = default);

    Task<bool> AuthenticateAsync(string email, string password, CancellationToken ct = default);
}