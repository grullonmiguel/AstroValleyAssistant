using AstroValley.Domain.Enums;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AstroValley.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject, IDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public bool IsMenuOpen
    {
        get;
        set => SetProperty(ref field, value);
    }

    // Page navigation — any ObservableObject can be a page
    public ObservableObject? CurrentViewModel
    {
        get;
        set => SetProperty(ref field, value);
    }

    // Dialog overlay — only DialogViewModelBase subclasses
    public DialogViewModelBase? CurrentDialogViewModel
    {
        get;
        set => SetProperty(ref field, value);
    }

    // Drawer overlay — only DialogViewModelBase subclasses  
    public DialogViewModelBase? CurrentDrawerViewModel
    {
        get;
        set => SetProperty(ref field, value);
    }

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

    [RelayCommand]
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

    public void ShowDialog(DialogViewModelBase viewModel, DialogOption dialogType = DialogOption.Default)
    {
        CurrentDialogViewModel = viewModel;
    }

    [RelayCommand]
    public void CloseDialog()
    {
        // Call lifecycle hook if the ViewModel implements it
        if (CurrentDialogViewModel != null)
        {
            // Use reflection to check if OnDialogClosing method exists
            var method = CurrentDialogViewModel.GetType().GetMethod("OnDialogClosing");
            method?.Invoke(CurrentDialogViewModel, null);
        }

        CurrentDialogViewModel = null;
    }

    public void OpenDrawer(DialogViewModelBase drawerViewModel) => CurrentDrawerViewModel = drawerViewModel;

    [RelayCommand]
    public void CloseDrawer() => CurrentDrawerViewModel = null;

    [RelayCommand]
    private void OpenDrawer(MenuOption option)
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
    private DialogViewModelBase? CreateDrawerViewModel(MenuOption option)
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

    [RelayCommand]
    private void OpenMenu(Button? button)
    {
        // Guard clause
        if (button?.ContextMenu is null)
            return;

        // Share the button's DataContext with the context menu for consistent bindings.
        button.ContextMenu.DataContext = button.DataContext;

        // Open the context menu.
        button.ContextMenu.IsOpen = true;
    }
}