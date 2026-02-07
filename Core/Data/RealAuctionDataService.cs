using System.IO;
using System.Text.Json;

namespace AstroValleyAssistant.Core.Data
{
    public class RealAuctionDataService
    {
        public record RealAuctionCountyInfo
        {
            public string Name { get; init; } = "";
            public string Calendar { get; init; } = "";
            public string Auction { get; init; } = "";
        }

        // This will cache the data after it's loaded from the file once
        private static Dictionary<string, List<RealAuctionCountyInfo>> _countyDataCache = new(); // never null
        private static readonly JsonSerializerOptions CountyJsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Ensures the JSON file is loaded and cached. Safe to call multiple times.
        /// </summary>
        public async Task InitializeAsync() => await LoadDataAsync();

        /// <summary>
        /// Returns the full state->counties map as a read-only dictionary,
        /// for scenarios like populating state lists in a ViewModel.
        /// </summary>
        public IReadOnlyDictionary<string, List<RealAuctionCountyInfo>> CountyData
        {
            get
            {
                // defensive: ensure loaded if caller forgot to call InitializeAsync
                if (_countyDataCache.Count == 0)
                {
                    LoadDataAsync().GetAwaiter().GetResult();
                }

                return _countyDataCache;
            }
        }

        /// <summary>
        /// Convenience method: get all counties for a given state code (e.g., "FL").
        /// </summary>
        public async Task<List<RealAuctionCountyInfo>> GetCountiesForStateAsync(string? stateAbbreviation)
        {
            // 1. Ensure the data is loaded and cached
            await LoadDataAsync();

            // 2. Return the requested data from the cache
            return _countyDataCache.TryGetValue(stateAbbreviation, out var counties)
                ? counties : [];
        }

        // This method handles the file reading and deserialization
        private async Task LoadDataAsync()
        {
            // If already loaded, no work to do.
            if (_countyDataCache.Count > 0)
                return;

            // Build an absolute path based on the app's base directory.
            var filePath = Path.Combine(AppContext.BaseDirectory, "Core", "Data", "Counties_RealAuction.json");

            // File missing: keep an empty cache but make it obvious during development.
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Counties_RealAuction.json was not found.", filePath);

            await using var stream = File.OpenRead(filePath);

            var data = await JsonSerializer.DeserializeAsync<Dictionary<string, List<RealAuctionCountyInfo>>>(stream, CountyJsonOptions);

            // JSON shape doesn't match the expected type.
            if (data is null)
                throw new InvalidOperationException($"Failed to deserialize {filePath} into Dictionary<string, List<RealAuctionCountyInfo>>.");

            _countyDataCache = data;
        }
    }
}