using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class MainViewModel : ViewModelBase, IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        // This is the command that the RadioButtons in the view bind to.
        public ICommand NavigateCommand { get; }
        public ICommand CloseDialogCommand { get; }

        public MainViewModel(IServiceProvider serviceProvider, IDialogService dialogService)
        {
            _serviceProvider = serviceProvider;
            NavigateCommand = new RelayCommand(Navigate);
            CloseDialogCommand = new RelayCommand(_ => CloseDialog());

            // Set the initial view. Since "Maps" was checked by default in your XAML.
            CurrentViewModel = _serviceProvider.GetRequiredService<MapViewModel>();

            // Tell the dialog service what to do when a dialog is requested
            if (dialogService is DialogService concreteDialogService)
            {
                concreteDialogService.ShowDialogAction = vm => CurrentDialogViewModel = vm;
                concreteDialogService.CloseDialogAction = CloseDialog;
            }
        }

        // This property holds the active view model.
        // The ContentControl in the view binds to this.
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }
        private ViewModelBase _currentViewModel;


        public ViewModelDialogBase CurrentDialogViewModel
        {
            get => _currentDialogViewModel;
            set => Set(ref _currentDialogViewModel, value);
        }
        private ViewModelDialogBase _currentDialogViewModel;

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

        public void ShowDialog(ViewModelDialogBase viewModel)
        {
            CurrentDialogViewModel = viewModel;
        }
        private void CloseDialog()
        {
            CurrentDialogViewModel = null;
        }

        #endregion
    }
}
