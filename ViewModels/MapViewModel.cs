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

        //private ICommand? _showMapCommand;
        //public ICommand ShowCountyMapCommand => _showMapCommand ??= new RelayCommand<StateViewModel>(ShowCountyMap);

        private AsyncRelayCommand<StateViewModel>? _showMapCommand;
        public ICommand ShowCountyMapCommand =>
            _showMapCommand ??= new AsyncRelayCommand<StateViewModel>(ShowCountyMap);

        // This property can be used to show a loading indicator in the UI
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

            // Fire and forget the async loading method from the constructor
            _ = LoadStatesAsync();
        }

        private void ShowMap()
        {
            //_dialogService.ShowDialog(new CountyMapDialogViewModel(SelectedState));
        }

        // Opens the county map dialog for a given state.
        private async Task ShowCountyMap(StateViewModel? state)
        {
            // Create and show the county map dialog, passing the selected state and geo service 
            //_dialogService.ShowDialog(new CountyMapDialogViewModel(state, _geoService));

            var vm = new CountyMapDialogViewModel(state, _geoService);

            // Show dialog first so animation can run
            _dialogService.ShowDialog(vm);

            await Task.Delay(300);

            // Then load data asynchronously
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
                    var tempStates = new List<StateViewModel>();

                    // Add U.S. states
                    tempStates.Add(CreateState("Alabama", "AL", 67, TaxSaleType.TaxLien));           
                    tempStates.Add(CreateState("Alaska", "AK", 29, TaxSaleType.TaxDeed));            
                    tempStates.Add(CreateState("Arizona", "AZ", 15, TaxSaleType.TaxLien));           
                    tempStates.Add(CreateState("Arkansas", "AR", 75, TaxSaleType.TaxDeed));          
                    tempStates.Add(CreateState("California", "CA", 58, TaxSaleType.TaxDeed));        
                    tempStates.Add(CreateState("Colorado", "CO", 64, TaxSaleType.TaxLien));
                    tempStates.Add(CreateState("Connecticut", "CT", 8, TaxSaleType.RedeemableDeed));
                    tempStates.Add(CreateState("District of Columbia", "DC", 1, TaxSaleType.TaxLien)); ; 
                    tempStates.Add(CreateState("Delaware", "DE", 3, TaxSaleType.RedeemableDeed));    
                    tempStates.Add(CreateState("Florida", "FL", 67, TaxSaleType.Hybrid));            
                    tempStates.Add(CreateState("Georgia", "GA", 159, TaxSaleType.RedeemableDeed));   
                    tempStates.Add(CreateState("Hawaii", "HI", 5, TaxSaleType.RedeemableDeed));      
                    tempStates.Add(CreateState("Idaho", "ID", 44, TaxSaleType.TaxDeed));             
                    tempStates.Add(CreateState("Illinois", "IL", 102, TaxSaleType.Hybrid));          
                    tempStates.Add(CreateState("Indiana", "IN", 92, TaxSaleType.Hybrid));            
                    tempStates.Add(CreateState("Iowa", "IA", 99, TaxSaleType.TaxLien));              
                    tempStates.Add(CreateState("Kansas", "KS", 105, TaxSaleType.TaxDeed));           
                    tempStates.Add(CreateState("Kentucky", "KY", 120, TaxSaleType.TaxLien));         
                    tempStates.Add(CreateState("Louisiana", "LA", 64, TaxSaleType.RedeemableDeed));  
                    tempStates.Add(CreateState("Maine", "ME", 16, TaxSaleType.TaxDeed));             
                    tempStates.Add(CreateState("Maryland", "MD", 24, TaxSaleType.TaxLien));          
                    tempStates.Add(CreateState("Massachusetts", "MA", 14, TaxSaleType.RedeemableDeed)); 
                    tempStates.Add(CreateState("Michigan", "MI", 83, TaxSaleType.TaxDeed));           
                    tempStates.Add(CreateState("Minnesota", "MN", 87, TaxSaleType.TaxDeed));          
                    tempStates.Add(CreateState("Mississippi", "MS", 82, TaxSaleType.TaxLien));        
                    tempStates.Add(CreateState("Missouri", "MO", 115, TaxSaleType.TaxLien));          
                    tempStates.Add(CreateState("Montana", "MT", 56, TaxSaleType.TaxLien));            
                    tempStates.Add(CreateState("Nebraska", "NE", 93, TaxSaleType.TaxLien));           
                    tempStates.Add(CreateState("Nevada", "NV", 17, TaxSaleType.Hybrid));              
                    tempStates.Add(CreateState("New Hampshire", "NH", 10, TaxSaleType.TaxDeed));      
                    tempStates.Add(CreateState("New Jersey", "NJ", 21, TaxSaleType.TaxLien));         
                    tempStates.Add(CreateState("New Mexico", "NM", 33, TaxSaleType.TaxDeed));         
                    tempStates.Add(CreateState("New York", "NY", 62, TaxSaleType.Hybrid));            
                    tempStates.Add(CreateState("North Carolina", "NC", 100, TaxSaleType.TaxDeed));    
                    tempStates.Add(CreateState("North Dakota", "ND", 53, TaxSaleType.TaxDeed));       
                    tempStates.Add(CreateState("Ohio", "OH", 88, TaxSaleType.Hybrid));                
                    tempStates.Add(CreateState("Oklahoma", "OK", 77, TaxSaleType.TaxDeed));           
                    tempStates.Add(CreateState("Oregon", "OR", 36, TaxSaleType.TaxDeed));             
                    tempStates.Add(CreateState("Pennsylvania", "PA", 67, TaxSaleType.TaxDeed));       
                    tempStates.Add(CreateState("Rhode Island", "RI", 5, TaxSaleType.RedeemableDeed)); 
                    tempStates.Add(CreateState("South Carolina", "SC", 46, TaxSaleType.TaxLien));     
                    tempStates.Add(CreateState("South Dakota", "SD", 66, TaxSaleType.TaxLien));       
                    tempStates.Add(CreateState("Tennessee", "TN", 95, TaxSaleType.RedeemableDeed));   
                    tempStates.Add(CreateState("Texas", "TX", 254, TaxSaleType.RedeemableDeed));      
                    tempStates.Add(CreateState("Utah", "UT", 29, TaxSaleType.TaxDeed));               
                    tempStates.Add(CreateState("Vermont", "VT", 14, TaxSaleType.TaxLien));            
                    tempStates.Add(CreateState("Virginia", "VA", 133, TaxSaleType.TaxDeed));          
                    tempStates.Add(CreateState("Washington", "WA", 39, TaxSaleType.TaxDeed));         
                    tempStates.Add(CreateState("West Virginia", "WV", 55, TaxSaleType.Hybrid));       
                    tempStates.Add(CreateState("Wisconsin", "WI", 72, TaxSaleType.TaxDeed));          
                    tempStates.Add(CreateState("Wyoming", "WY", 23, TaxSaleType.TaxLien));

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
                // Log the error or show a message to the user
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
            if (!statesDictionary.Contains(key))
                throw new KeyNotFoundException($"No geometry resource found for key '{key}'.");

            // Retrieve the resource and ensure it is actually a Geometry instance.
            if (statesDictionary[key] is not Geometry geometry)
                throw new InvalidCastException($"Resource '{key}' is not of type Geometry.");

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
