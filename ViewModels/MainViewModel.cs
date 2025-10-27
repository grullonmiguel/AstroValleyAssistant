using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class MainViewModel : ViewModelBase, IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        #region Commands

        public ICommand NavigateCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand OpenMenuCommand => new RelayCommand(_ => IsMenuOpen = true);
        public ICommand OpenDrawerCommand => new RelayCommand<string>(OnMenuOptionSelected);
        public ICommand CloseDrawerCommand => new RelayCommand(_ => CloseDrawer());
        #endregion

        #region Properties

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set => Set(ref _isMenuOpen, value);
        }
        private bool _isMenuOpen;

        public ViewModelBase? CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }
        private ViewModelBase? _currentViewModel;

        public ViewModelDialogBase? CurrentDialogViewModel
        {
            get => _currentDialogViewModel;
            set => Set(ref _currentDialogViewModel, value);
        }
        private ViewModelDialogBase? _currentDialogViewModel;

        public ViewModelDialogBase? CurrentDrawerViewModel
        {
            get => _currentDrawerViewModel;
            set => Set(ref _currentDrawerViewModel, value);
        }
        private ViewModelDialogBase? _currentDrawerViewModel;

        #endregion

        #region Constructor

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

        #endregion

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

        public void ShowDialog(ViewModelDialogBase viewModel) => CurrentDialogViewModel = viewModel;

        private void CloseDialog() => CurrentDialogViewModel = null;

        public void OpenDrawer(ViewModelDialogBase drawerViewModel) => CurrentDrawerViewModel = drawerViewModel;

        public void CloseDrawer() => CurrentDrawerViewModel = null;
        
        private void OnMenuOptionSelected(string option)
        {
            // Close menu
            IsMenuOpen = false;

            // Trigger the correct drawer dialog based on option
            switch (option)
            {
                case "OptionRealAuction":
                    CurrentDrawerViewModel = new RealAuctionSettingsViewModel();
                    break;
                case "OptionRegrid":
                    CurrentDrawerViewModel = new RegridSettingsViewModel();
                    break;
                case "OptionThemes":
                    CurrentDrawerViewModel = new ThemeSettingsViewModel();
                    break;
            }
        }

        #endregion
    }
}