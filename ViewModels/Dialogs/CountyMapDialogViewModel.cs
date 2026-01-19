using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels.Dialogs
{
    public class CountyMapDialogViewModel : ViewModelDialogBase
    {
        private readonly GeographyDataService _geoService; 
        
        // Cache the calculated map bounds
        private Rect? _mapBoundsCache;

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
        private bool _isLoading;

        // Properties to bind the map dimensions to
        public double MapWidth => CalculateMapDimensions().Width;
        
        public double MapHeight => CalculateMapDimensions().Height;

        public StateViewModel State { get; }

        public ObservableCollection<CountyViewModel> Counties { get; } = new();

        public CountyViewModel SelectedCounty
        {
            get => _selectedCounty;
            set
            {
                if (_selectedCounty != null)
                    _selectedCounty.IsSelected = false;

                Set(ref _selectedCounty, value);
                if (_selectedCounty != null)
                    _selectedCounty.IsSelected = true;
            }
        }
        private CountyViewModel? _selectedCounty;

        #endregion

        #region Constructor

        public CountyMapDialogViewModel(StateViewModel state, GeographyDataService geoService)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(geoService);

            _geoService = geoService;

            State = state;
            Title = $"{State.Name}: {State.CountyCount} Counties";

            IsLoading = true;
        }

        #endregion

        #region Methods

        private Rect CalculateMapDimensions()
        {
            // 1. Check the cache: If the value exists AND is not Empty, return it immediately.
            // Add the !totalBounds.IsEmpty check here to ensure we don't cache a failed result.
            if (_mapBoundsCache.HasValue && !_mapBoundsCache.Value.IsEmpty)
            {
                return _mapBoundsCache.Value;
            }

            Rect totalBounds = Rect.Empty;

            // 2. Calculation: Iterate through all loaded counties to find the total bounding box.
            foreach (var county in Counties)
            {
                if (county.PathData != null)
                {
                    // Union combines the current total area with the new county's bounds
                    totalBounds.Union(county.PathData.Bounds);
                }
            }

            // 3. Conditional Caching: Only cache the result if the calculation was successful (i.e., not empty).
            if (!totalBounds.IsEmpty)
            {
                _mapBoundsCache = totalBounds;
            }

            // 4. Return the result.
            return totalBounds;
        }

        public async Task InitializeAsync()
        {
            await LoadCountiesAsync();
            IsLoading = false;
        }

        private async Task LoadCountiesAsync()
        {
            try
            {
                // 1. Get the county info from the service
                var countiesForState = await _geoService.GetCountiesForStateAsync(State?.Abbreviation);

                var loadedCounties = await Task.Run(() =>
                {
                    var tempList = new List<CountyViewModel>();
                    var dictionary = new ResourceDictionary { Source = new Uri($"/Themes/Assets/Geography/{State.Abbreviation}.xaml", UriKind.Relative) };

                    CountyViewModel CreateCounty(GeographyDataService.CountyInfo info)
                    {
                        var geometry = dictionary[info.key] as Geometry;
                        return new CountyViewModel { Name = info.name, PathData = geometry };
                    }

                    // 2. Loop through the data and create ViewModels
                    foreach (var countyInfo in countiesForState)
                    {
                        tempList.Add(CreateCounty(countyInfo));
                    }
                    return tempList;
                });

                // --- We are now back on the UI thread ---

                // 1. Populate the ObservableCollection
                Counties.Clear();
                foreach (var county in loadedCounties)
                {
                    Counties.Add(county);
                }

                // Trigger property change notifications for the calculated properties
                // This makes the Viewbox instantly size itself correctly.
                OnPropertyChanged(nameof(MapWidth));
                OnPropertyChanged(nameof(MapHeight));

                // 2. Set the SelectedCounty property to the first item in the list
                SelectedCounty = Counties?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load county geometry for {State.Abbreviation}: {ex.Message}");
            }
        }
        #endregion
    }
}
