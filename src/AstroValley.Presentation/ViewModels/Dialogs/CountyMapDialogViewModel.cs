using AstroValley.Application.Interfaces.Data;
using AstroValley.Domain.Models;
using AstroValley.Infrastructure.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace AstroValley.Presentation.ViewModels.Dialogs;

public class CountyMapDialogViewModel : DialogViewModelBase
{
    private readonly IGeographyDataService _geoService; 
    
    // Cache the calculated map bounds
    private Rect? _mapBoundsCache;

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double MapWidth => CalculateMapDimensions().Width;        
    public double MapHeight => CalculateMapDimensions().Height;

    public StateViewModel State { get; }

    public ObservableCollection<CountyViewModel> Counties { get; } = [];

    public CountyViewModel? SelectedCounty
    {
        get;
        set
        {
            field?.IsSelected = false;

            SetProperty(ref field, value);

            field?.IsSelected = true;
        }
    }

    public CountyMapDialogViewModel(StateViewModel state, IGeographyDataService geoService)
    {
        // Run validation
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(geoService);

        _geoService = geoService;

        State = state;
        IsLoading = true;
        Title = $"{State.Name}: {State.CountyCount} Counties";
    }

    private Rect CalculateMapDimensions()
    {
        // 1. Check the cache: If the value exists AND is not Empty, return it immediately.
        // Add the !totalBounds.IsEmpty check here to ensure we don't cache a failed result.
        if (_mapBoundsCache.HasValue && !_mapBoundsCache.Value.IsEmpty)
            return _mapBoundsCache.Value;

        Rect totalBounds = Rect.Empty;

        // 2. Calculation: Iterate through all loaded counties to find the total bounding box.
        foreach (var county in Counties)
        {                
            if (county.PathData != null)
                totalBounds.Union(county.PathData.Bounds); // Union combines the current total area with the new county's bounds
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

                CountyViewModel CreateCounty(CountyInfo info)
                {
                    var geometry = dictionary[info.Key] as Geometry;
                    return new CountyViewModel { Name = info.Name, PathData = geometry };
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
}