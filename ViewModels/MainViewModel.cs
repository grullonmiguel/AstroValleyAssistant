using AstroValleyAssistant.Core;
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

        public ICommand NavigateCommand => field ??= new RelayCommand(Navigate);
        public ICommand CloseDialogCommand => field ??= new RelayCommand(_ => CloseDialog());
        public ICommand OpenMenuCommand => field ??= new RelayCommand<Button>(OnOpenContextMenu);
        public ICommand OpenDrawerCommand => field ??= new RelayCommand<MenuOption>(OnMenuOptionSelected);
        public ICommand CloseDrawerCommand => field ??= new RelayCommand(_ => CloseDrawer());

        #endregion

        #region Properties

        public bool IsMenuOpen
        {
            get => field;
            set => Set(ref field, value);
        }

        public ViewModelBase? CurrentViewModel
        {
            get => field;
            set => Set(ref field, value);
        }

        public ViewModelDialogBase? CurrentDialogViewModel
        {
            get => field;
            set => Set(ref field, value);
        }

        public ViewModelDialogBase? CurrentDrawerViewModel
        {
            get => field;
            set => Set(ref field, value);
        }

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
                dialog.ShowDrawerDialogAction = vm => CurrentDrawerViewModel = vm;
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

        public void ShowDialog(ViewModelDialogBase viewModel, DialogOption dialogType = DialogOption.Default)
        {
            CurrentDialogViewModel = viewModel;
        }

        public void CloseDialog() => CurrentDialogViewModel = null;

        public void OpenDrawer(ViewModelDialogBase drawerViewModel) => CurrentDrawerViewModel = drawerViewModel;

        public void CloseDrawer() => CurrentDrawerViewModel = null;

        private void OnMenuOptionSelected(MenuOption option)
        {
            // Close the menu as soon as a valid option is selected.
            IsMenuOpen = false;

            // Resolve the appropriate drawer view model for the selected option.
            switch (option)
            {
                case MenuOption.Regrid:
                    {
                        var vm = _serviceProvider.GetRequiredService<RegridSettingsViewModel>();
                        vm.Saved = CloseDrawer;
                        CurrentDrawerViewModel = vm;
                        break;
                    }

                case MenuOption.PinMap:
                    {
                        var vm = _serviceProvider.GetRequiredService<MarkerMapViewModel>();
                        CurrentDialogViewModel = vm;
                        break;
                    }
                case MenuOption.Themes:
                    {
                        var vm = _serviceProvider.GetRequiredService<ThemeSettingsViewModel>();
                        CurrentDrawerViewModel = vm;
                        break;
                    }

                default:
                    // Unknown option; no drawer to open.
                    break;
            }
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

                case MenuOption.PinMap:
                    {
                        var vm = _serviceProvider.GetRequiredService<MarkerMapViewModel>();
                        //vm.Saved = CloseDrawer;
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