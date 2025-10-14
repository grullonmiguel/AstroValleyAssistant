using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class MapViewModel : ViewModelBase
    {

        private readonly IDialogService _dialogService;
        private readonly GeographyDataService _geoService;
        public ICommand ShowCountyMapCommand { get; }

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
        private StateViewModel _selectedState;

        public ObservableCollection<StateViewModel> States { get; } = [];

        public MapViewModel(IDialogService dialogService, GeographyDataService geoService)
        {
            _dialogService = dialogService;
            _geoService = geoService;
            ShowCountyMapCommand = new RelayCommand(ShowCountyMap);

            // Fire and forget the async loading method from the constructor
            _ = LoadStatesAsync();
        }

        private void ShowMap()
        {
            //_dialogService.ShowDialog(new CountyMapDialogViewModel(SelectedState));
        }

        private void ShowCountyMap(object? parameter)
        {
            if (parameter is StateViewModel state)
            {
                _dialogService.ShowDialog(new CountyMapDialogViewModel(state, _geoService));
            }
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
                    var statesDictionary = new ResourceDictionary
                    {
                        Source = new Uri("/Themes/Assets/Geography/States.xaml", UriKind.Relative)
                    };

                    // Create a helper method or loop to avoid repeating code
                    StateViewModel CreateState(string name, string abbr, int countyCount, TaxSaleType taxStatus)
                    {
                        var geometry = statesDictionary[$"Geometry.{abbr}"] as Geometry;
                        return new StateViewModel
                        {
                            Name = name,
                            Abbreviation = abbr,
                            PathData = geometry,
                            CountyCount = countyCount,
                            TaxStatus = taxStatus
                        };
                    }

                    // ... add all 50 states ...
                    tempStates.Add(CreateState("Alabama", "AL", 67, TaxSaleType.TaxLien));           
                    tempStates.Add(CreateState("Alaska", "AK", 29, TaxSaleType.TaxDeed));            
                    tempStates.Add(CreateState("Arizona", "AZ", 15, TaxSaleType.TaxLien));           
                    tempStates.Add(CreateState("Arkansas", "AR", 75, TaxSaleType.TaxDeed));          
                    tempStates.Add(CreateState("California", "CA", 58, TaxSaleType.TaxDeed));        
                    tempStates.Add(CreateState("Colorado", "CO", 64, TaxSaleType.TaxLien));          
                    tempStates.Add(CreateState("Connecticut", "CT", 8, TaxSaleType.RedeemableDeed)); 
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
                // You could set a property here to show an error message in the UI
            }
            finally
            {
                IsLoading = false;
            }
        }

        
    }
}
