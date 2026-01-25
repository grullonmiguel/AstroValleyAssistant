using AstroValleyAssistant.Core;
using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Commands;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.ViewModels
{
    public class MainViewModel : ViewModelBase, IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        #region Commands

        private ICommand? _navigateCommand;
        public ICommand NavigateCommand => _navigateCommand ??= new RelayCommand(Navigate);

        private ICommand? _closeDialogCommand;
        public ICommand CloseDialogCommand => _closeDialogCommand ??= new RelayCommand(_ => CloseDialog());

        private ICommand? _openMenuCommand;
        public ICommand OpenMenuCommand => _openMenuCommand ??= new RelayCommand<Button>(OnOpenContextMenu);

        private ICommand? _openDrawerCommand;
        public ICommand OpenDrawerCommand => _openDrawerCommand ??= new RelayCommand<MenuOption>(OnMenuOptionSelected);

        private ICommand? _closeDrawerCommand;
        public ICommand CloseDrawerCommand => _closeDrawerCommand ??= new RelayCommand(_ => CloseDrawer());

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
            // Run validation
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(serviceProvider);
           
            // Set the initial view.
            _serviceProvider = serviceProvider;
            CurrentViewModel = _serviceProvider.GetRequiredService<MapViewModel>();

            // Tell the dialog service what to do when a dialog is requested
            if (dialogService is DialogService dialog)
            {
                dialog.ShowDialogAction = vm => CurrentDialogViewModel = vm;
                dialog.CloseDialogAction = CloseDialog;
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

        private void OnMenuOptionSelected(MenuOption option)
        {
            // Close the menu as soon as a valid option is selected.
            IsMenuOpen = false;

            // Resolve the appropriate drawer view model for the selected option.
            CurrentDrawerViewModel = CreateDrawerViewModel(option);
        }

        /// <summary>
        /// Maps a menu option key to the corresponding drawer view model instance.
        /// </summary>
        private ViewModelDialogBase? CreateDrawerViewModel(MenuOption option)
        {
            switch (option)
            {
                case MenuOption.Regrid:
                    {
                        var vm = _serviceProvider.GetRequiredService<RegridSettingsViewModel>();
                        vm.Saved = CloseDrawer;
                        return vm;
                    }

                case MenuOption.Themes:
                    // This could also be resolved via DI for consistency.
                    return new ThemeSettingsViewModel();

                default:
                    // Unknown option; no drawer to open.
                    return null;
            }
        }

        private void OnOpenContextMenu(Button? button)
        {
            // Guard clause
            if (button?.ContextMenu is null)
                return;

            // Share the button's DataContext with the context menu for consistent bindings.
            button.ContextMenu.DataContext = button.DataContext;

            // Open the context menu.
            button.ContextMenu.IsOpen = true;
        }

        #endregion
    }
}