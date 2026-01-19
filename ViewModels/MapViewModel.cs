using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class MapViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly GeographyDataService _geoService;

        private AsyncRelayCommand<StateViewModel>? _showMapCommand;
        public ICommand ShowCountyMapCommand => _showMapCommand ??= new AsyncRelayCommand<StateViewModel>(ShowCountyMap);

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
        private bool _isLoading;

        // Store the state's listbox scroll bar's vertical offset.
        public double ListBoxScrollOffset { get; set; }

        public StateViewModel SelectedState
        {
            get => _selectedState;
            set
            {
                // Un-select the previously selected state, if there was one
                if (_selectedState != null)
                    _selectedState.IsSelected = false;

                Set(ref _selectedState, value);

                // Select the new state, if it's not null
                if (_selectedState != null)
                    _selectedState.IsSelected = true;
            }
        }
        private StateViewModel? _selectedState;

        public ObservableCollection<StateViewModel> States { get; } = [];

        public MapViewModel(IDialogService dialogService, GeographyDataService geoService)
        {
            // Run validation
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(geoService);

            _dialogService = dialogService;
            _geoService = geoService;

            _ = LoadStatesAsync();
        }
                
        private async Task ShowCountyMap(StateViewModel? state)
        {
            var vm = new CountyMapDialogViewModel(state, _geoService);

            // Opens the county map dialog for a given state.
            _dialogService.ShowDialog(vm);

            // Add short delay then load data
            await Task.Delay(300);
            await vm.InitializeAsync();
        }

        private async Task LoadStatesAsync()
        {
            IsLoading = true;

            try
            {
                // This runs the CPU/IO-bound work on a background thread
                List<StateViewModel> loadedStates = await Task.Run(() =>
                {
                    var tempStates = new List<StateViewModel>
                    {
                        CreateState("Alabama",              "AL", 67, TaxSaleType.TaxLien),
                        CreateState("Alaska",               "AK", 29, TaxSaleType.TaxDeed),
                        CreateState("Arizona",              "AZ", 15, TaxSaleType.TaxLien),
                        CreateState("Arkansas",             "AR", 75, TaxSaleType.TaxDeed),
                        CreateState("California",           "CA", 58, TaxSaleType.TaxDeed),
                        CreateState("Colorado",             "CO", 64, TaxSaleType.TaxLien),
                        CreateState("Connecticut",          "CT", 8,TaxSaleType.RedeemableDeed),
                        CreateState("District of Columbia", "DC", 1, TaxSaleType.TaxLien),
                        CreateState("Delaware",             "DE", 3, TaxSaleType.RedeemableDeed),
                        CreateState("Florida",              "FL", 67, TaxSaleType.Hybrid),
                        CreateState("Georgia",              "GA", 159, TaxSaleType.RedeemableDeed),
                        CreateState("Hawaii",               "HI", 5, TaxSaleType.RedeemableDeed),
                        CreateState("Idaho",                "ID", 44, TaxSaleType.TaxDeed),
                        CreateState("Illinois",             "IL", 102, TaxSaleType.Hybrid),
                        CreateState("Indiana",              "IN", 92, TaxSaleType.Hybrid),
                        CreateState("Iowa",                 "IA", 99, TaxSaleType.TaxLien),
                        CreateState("Kansas",               "KS", 105, TaxSaleType.TaxDeed),
                        CreateState("Kentucky",             "KY", 120, TaxSaleType.TaxLien),
                        CreateState("Louisiana",            "LA", 64, TaxSaleType.RedeemableDeed),
                        CreateState("Maine",                "ME", 16, TaxSaleType.TaxDeed),
                        CreateState("Maryland",             "MD", 24, TaxSaleType.TaxLien),
                        CreateState("Massachusetts",        "MA", 14, TaxSaleType.RedeemableDeed),
                        CreateState("Michigan",             "MI", 83, TaxSaleType.TaxDeed),
                        CreateState("Minnesota",            "MN", 87, TaxSaleType.TaxDeed),
                        CreateState("Mississippi",          "MS", 82, TaxSaleType.TaxLien),
                        CreateState("Missouri",             "MO", 115, TaxSaleType.TaxLien),
                        CreateState("Montana",              "MT", 56, TaxSaleType.TaxLien),
                        CreateState("Nebraska",             "NE", 93, TaxSaleType.TaxLien),
                        CreateState("Nevada",               "NV", 17, TaxSaleType.Hybrid),
                        CreateState("New Hampshire",        "NH", 10, TaxSaleType.TaxDeed),
                        CreateState("New Jersey",           "NJ", 21, TaxSaleType.TaxLien),
                        CreateState("New Mexico",           "NM", 33, TaxSaleType.TaxDeed),
                        CreateState("New York",             "NY", 62, TaxSaleType.Hybrid),
                        CreateState("North Carolina",       "NC", 100, TaxSaleType.TaxDeed),
                        CreateState("North Dakota",         "ND", 53, TaxSaleType.TaxDeed),
                        CreateState("Ohio",                 "OH", 88, TaxSaleType.Hybrid),
                        CreateState("Oklahoma",             "OK", 77, TaxSaleType.TaxDeed),
                        CreateState("Oregon",               "OR", 36, TaxSaleType.TaxDeed),
                        CreateState("Pennsylvania",         "PA", 67, TaxSaleType.TaxDeed),
                        CreateState("Rhode Island",         "RI", 5, TaxSaleType.RedeemableDeed),
                        CreateState("South Carolina",       "SC", 46, TaxSaleType.TaxLien),
                        CreateState("South Dakota",         "SD", 66, TaxSaleType.TaxLien),
                        CreateState("Tennessee",            "TN", 95, TaxSaleType.RedeemableDeed),
                        CreateState("Texas",                "TX", 254, TaxSaleType.RedeemableDeed),
                        CreateState("Utah",                 "UT", 29, TaxSaleType.TaxDeed),
                        CreateState("Vermont",              "VT", 14, TaxSaleType.TaxLien),
                        CreateState("Virginia",             "VA", 133, TaxSaleType.TaxDeed),
                        CreateState("Washington",           "WA", 39, TaxSaleType.TaxDeed),
                        CreateState("West Virginia",        "WV", 55, TaxSaleType.Hybrid),
                        CreateState("Wisconsin",            "WI", 72, TaxSaleType.TaxDeed),
                        CreateState("Wyoming",              "WY", 23, TaxSaleType.TaxLien)
                    };

                    return tempStates;
                });

                // Now that we are back on the UI thread, update the collection
                States.Clear();
                foreach (var state in loadedStates)
                {
                    States.Add(state);
                }

                // Set a default selection
                if (States?.Count > 0)
                    SelectedState = States.FirstOrDefault(s => s.Abbreviation == "FL");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load state geometry: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Factory method that creates a StateViewModel from basic state data.
        // Looks up the corresponding Geometry in the ResourceDictionary using the state abbreviation.
        private StateViewModel CreateState(string name, string abbr, int countyCount, TaxSaleType taxStatus)
        {
            // Build the resource key (e.g., "Geometry.FL") used in the XAML ResourceDictionary.
            var key = $"Geometry.{abbr}";

            var statesDictionary = new ResourceDictionary { Source = new Uri("/Themes/Assets/Geography/States.xaml", UriKind.Relative) };

            // Check that the resource key exists before accessing it.
            if (!statesDictionary.Contains(key)) throw new KeyNotFoundException($"No geometry resource found for key '{key}'.");

            // Retrieve the resource and ensure it is actually a Geometry instance.
            if (statesDictionary[key] is not Geometry geometry) throw new InvalidCastException($"Resource '{key}' is not of type Geometry.");

            // Create and return the view model populated with state metadata and its path geometry.
            return new StateViewModel
            {
                Name = name,
                Abbreviation = abbr,
                PathData = geometry,
                CountyCount = countyCount,
                TaxStatus = taxStatus
            };
        }
    }
}