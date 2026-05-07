namespace AstroValley.Application.Interfaces.Scraping;

public interface IRegridHttpClient
{
    Task<byte[]> DownloadImageAsync(string url);
}
