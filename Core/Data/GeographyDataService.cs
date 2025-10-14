using System.IO;
using System.Text.Json;

namespace AstroValleyAssistant.Core.Data
{
    public class GeographyDataService
    {
        // C# record to match our JSON structure
        public record CountyInfo(string name, string key);

        // This will cache the data after it's loaded from the file once
        private static Dictionary<string, List<CountyInfo>> _countyDataCache;

        // The public method to get data for a state
        public async Task<List<CountyInfo>> GetCountiesForStateAsync(string stateAbbreviation)
        {
            // 1. Ensure the data is loaded and cached
            await LoadDataIfNeededAsync();

            // 2. Return the requested data from the cache
            return _countyDataCache.TryGetValue(stateAbbreviation, out var counties)
                ? counties
                : new List<CountyInfo>();
        }

        // This method handles the file reading and deserialization
        private async Task LoadDataIfNeededAsync()
        {
            // Only read the file from disk the very first time this is called
            if (_countyDataCache != null)
            {
                return;
            }

            string filePath = "Themes/Assets/Geography/Counties.json";
            if (!File.Exists(filePath))
            {
                _countyDataCache = new Dictionary<string, List<CountyInfo>>();
                return;
            }

            await using FileStream stream = File.OpenRead(filePath);
            _countyDataCache = await JsonSerializer.DeserializeAsync<Dictionary<string, List<CountyInfo>>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Makes it flexible (name vs Name)
            });
        }
    }
}
