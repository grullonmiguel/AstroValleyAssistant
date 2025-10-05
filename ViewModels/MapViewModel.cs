using AstroValleyAssistant.Core;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class MapViewModel : ViewModelBase
    {
        // This property can be used to show a loading indicator in the UI
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
        private bool _isLoading;

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

        public MapViewModel()
        {
            // Fire and forget the async loading method from the constructor
            _ = LoadStatesAsync();
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
                    StateViewModel CreateState(string name, string abbr)
                    {
                        var geometry = statesDictionary[$"Geometry.{abbr}"] as Geometry;
                        return new StateViewModel { Name = name, Abbreviation = abbr, PathData = geometry };
                    }

                    // ... add all 50 states ...
                    tempStates.Add(CreateState("Alabama",  "AL"));
                    tempStates.Add(CreateState("Alaska",  "AK"));
                    tempStates.Add(CreateState("Arizona",  "AZ"));
                    tempStates.Add(CreateState("Arkansas",  "AR"));
                    tempStates.Add(CreateState("California",  "CA"));
                    tempStates.Add(CreateState("Colorado",  "CO"));
                    tempStates.Add(CreateState("Connecticut",  "CT"));
                    tempStates.Add(CreateState("Delaware",  "DE"));
                    tempStates.Add(CreateState("Florida",  "FL"));
                    tempStates.Add(CreateState("Georgia",  "GA"));
                    tempStates.Add(CreateState("Hawaii",  "HI"));
                    tempStates.Add(CreateState("Idaho",  "ID"));
                    tempStates.Add(CreateState("Illinois",  "IL"));
                    tempStates.Add(CreateState("Indiana",  "IN"));
                    tempStates.Add(CreateState("Iowa",  "IA"));
                    tempStates.Add(CreateState("Kansas",  "KS"));
                    tempStates.Add(CreateState("Kentucky",  "KY"));
                    tempStates.Add(CreateState("Louisiana",  "LA"));
                    tempStates.Add(CreateState("Maine",  "ME"));
                    tempStates.Add(CreateState("Maryland",  "MD"));
                    tempStates.Add(CreateState("Massachusetts",  "MA"));
                    tempStates.Add(CreateState("Michigan",  "MI"));
                    tempStates.Add(CreateState("Minnesota",  "MN"));
                    tempStates.Add(CreateState("Mississippi",  "MS"));
                    tempStates.Add(CreateState("Missouri",  "MO"));
                    tempStates.Add(CreateState("Montana",  "MT"));
                    tempStates.Add(CreateState("Nebraska",  "NE"));
                    tempStates.Add(CreateState("Nevada",  "NV"));
                    tempStates.Add(CreateState("New Hampshire",  "NH"));
                    tempStates.Add(CreateState("New Jersey",  "NJ"));
                    tempStates.Add(CreateState("New Mexico",  "NM"));
                    tempStates.Add(CreateState("New York",  "NY"));
                    tempStates.Add(CreateState("North Carolina",  "NC"));
                    tempStates.Add(CreateState("North Dakota",  "ND"));
                    tempStates.Add(CreateState("Ohio",  "OH"));
                    tempStates.Add(CreateState("Oklahoma",  "OK"));
                    tempStates.Add(CreateState("Oregon",  "OR"));
                    tempStates.Add(CreateState("Pennsylvania",  "PA"));
                    tempStates.Add(CreateState("Rhode Island",  "RI"));
                    tempStates.Add(CreateState("South Carolina",  "SC"));
                    tempStates.Add(CreateState("South Dakota",  "SD"));
                    tempStates.Add(CreateState("Tennessee",  "TN"));
                    tempStates.Add(CreateState("Texas",  "TX"));
                    tempStates.Add(CreateState("Utah",  "UT"));
                    tempStates.Add(CreateState("Vermont",  "VT"));
                    tempStates.Add(CreateState("Virginia",  "VA"));
                    tempStates.Add(CreateState("Washington",  "WA"));
                    tempStates.Add(CreateState("West Virginia",  "WV"));
                    tempStates.Add(CreateState("Wisconsin",  "WI"));
                    tempStates.Add(CreateState("Wyoming",  "WY"));

                    return tempStates;
                });

                // Now that we are back on the UI thread, update the collection
                States.Clear();
                foreach (var state in loadedStates)
                {
                    States.Add(state);
                }
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
