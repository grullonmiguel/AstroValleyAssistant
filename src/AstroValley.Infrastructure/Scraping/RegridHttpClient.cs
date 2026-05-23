using AstroValley.Application.Interfaces.Scraping;
using System.Net.Http;

namespace AstroValley.Infrastructure.Scraping;

/// <summary>
/// HTTP client for downloading parcel images from Regrid's CDN.
/// Implements polite rate limiting with random delays to avoid overwhelming the server.
/// </summary>
/// <remarks>
/// Best Practice: Headers are configured in DI registration (App.xaml.cs) rather than
/// in the constructor. This ensures headers are set once during client creation,
/// not on every instantiation, improving performance and maintainability.
/// </remarks>
public class RegridHttpClient : IRegridHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    public RegridHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Note: Headers are now configured in App.xaml.cs during DI registration
        // This follows the best practice of centralizing HTTP client configuration
    }

    /// <summary>
    /// Downloads an image from the specified URL with polite rate limiting.
    /// </summary>
    /// <param name="url">The URL of the image to download.</param>
    /// <returns>The image data as a byte array, or an empty array if the URL is invalid.</returns>
    /// <remarks>
    /// Implements client-side rate limiting with random delays (500-1500ms) to:
    /// 1. Avoid overwhelming Regrid's servers
    /// 2. Reduce the likelihood of triggering rate limits
    /// 3. Appear more like human browsing behavior
    /// </remarks>
    public async Task<byte[]> DownloadImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();

        // Polite throttling: Random delay between 500ms and 1.5 seconds
        // This prevents burst requests and mimics human behavior
        await Task.Delay(_random.Next(500, 1500));

        // The actual HTTP request is handled by the configured HttpClient
        // Retry logic and resilience are managed by the resilience handler in DI
        return await _httpClient.GetByteArrayAsync(url);
    }
}
