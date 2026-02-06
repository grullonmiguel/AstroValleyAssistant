using System.Net.Http;

namespace AstroValleyAssistant.Core.Networking
{
    public class RegridHttpClient : IRegridHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();

        public RegridHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            ConfigureHeaders();
        }

        public async Task<byte[]> DownloadImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();

            // Polite throttling: 500ms to 1.5s delay
            await Task.Delay(_random.Next(500, 1500));

            return await _httpClient.GetByteArrayAsync(url);
        }

        private void ConfigureHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/144.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,*/*;q=0.8");

            // Session cookies from your existing logic
            string sessionCookies = "_session_id=7a8e35ecd5d318d2bd2331704a4e60ce; user.id=BAgw--79292e56d5866cc41199e6f92f32032686d8e164;";
            _httpClient.DefaultRequestHeaders.Add("Cookie", sessionCookies);
        }
    }
}
