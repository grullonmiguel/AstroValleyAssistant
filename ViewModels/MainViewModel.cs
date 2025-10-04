using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;

        // This is the command that the RadioButtons in the view bind to.
        public ICommand NavigateCommand { get; }

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            NavigateCommand = new RelayCommand(Navigate);

            // Set the initial view. Since "Maps" was checked by default in your XAML.
            CurrentViewModel = _serviceProvider.GetRequiredService<MapViewModel>();
        }

        // This property holds the active view model.
        // The ContentControl in the view binds to this.
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }
        private ViewModelBase _currentViewModel;

        #region Methods

        private void Navigate(object? parameter)
        {
            // The CommandParameter from the XAML ("Regrid", "Map", etc.) comes in here.
            string? viewName = parameter as string;

            if (string.IsNullOrEmpty(viewName)) return;

            // Use a switch to set the CurrentViewModel based on the parameter.
            // It requests the ViewModel from the DI container. Since they are registered
            // as Singletons, it will always return the same instance, preserving state.
            CurrentViewModel = viewName switch
            {
                "Regrid" => _serviceProvider.GetRequiredService<RegridViewModel>(),
                "RealAuction" => _serviceProvider.GetRequiredService<RealAuctionViewModel>(),
                "Map" => _serviceProvider.GetRequiredService<MapViewModel>(),
                _ => CurrentViewModel // Default case, does nothing
            };
        }

        #endregion
    }
}
