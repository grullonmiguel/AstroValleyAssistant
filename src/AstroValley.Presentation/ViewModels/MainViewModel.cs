using AstroValley.Domain.Enums;
using AstroValley.Presentation.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AstroValley.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
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
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsFullBleed));
            OnPropertyChanged(nameof(IsHomeActive));
            OnPropertyChanged(nameof(IsRegridActive));
            OnPropertyChanged(nameof(IsRealAuctionActive));
        }
    }

    // Header tab state follows the current page, so code that navigates (e.g. the Map button) updates the tabs too.
    // The no-op setters exist only so the RadioButtons can bind TwoWay (a OneWay binding is dropped on the first click).
    public bool IsHomeActive { get => CurrentViewModel is HomeViewModel; set { } }
    public bool IsRegridActive { get => CurrentViewModel is RegridViewModel; set { } }
    public bool IsRealAuctionActive { get => CurrentViewModel is RealAuctionViewModel; set { } }

    // Home's view is created once in MainView (not via DataTemplate) so the map persists.
    public HomeViewModel Home { get; }

    // Home draws edge to edge behind the header; other pages sit below it.
    public bool IsFullBleed => CurrentViewModel is HomeViewModel;

    // Dialog overlay — only DialogViewModelBase subclasses
    public object? CurrentDialogViewModel
    {
        get;
        set => SetProperty(ref field, value);
    }

    // Drawer overlay — only DialogViewModelBase subclasses  
    public object? CurrentDrawerViewModel
    {
        get;
        set => SetProperty(ref field, value);
    }

    public MainViewModel(IServiceProvider serviceProvider)
    {
        // Run validation
        ArgumentNullException.ThrowIfNull(serviceProvider);
       
        // Set the initial view.
        _serviceProvider = serviceProvider;
        Home = serviceProvider.GetRequiredService<HomeViewModel>();
        //CurrentViewModel = _serviceProvider.GetRequiredService<MapViewModel>();
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
            "Home" => _serviceProvider.GetRequiredService<HomeViewModel>(),
            "Regrid" => _serviceProvider.GetRequiredService<RegridViewModel>(),
            "RealAuction" => _serviceProvider.GetRequiredService<RealAuctionViewModel>(),
            "Map" => _serviceProvider.GetRequiredService<MapViewModel>(),
            _ => CurrentViewModel // Default case, does nothing
        };
    }

    [RelayCommand]
    private void OpenDrawer(MenuOption option)
    {
        IsMenuOpen = false;

        switch (option)
        {
            case MenuOption.Regrid:
                {
                    var vm = _serviceProvider.GetRequiredService<RegridSettingsViewModel>();
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
                break;
        }
    }

    [RelayCommand]
    private void CloseDrawer()
    {
        CurrentDrawerViewModel = null;
    }

    [RelayCommand]
    private void OpenMenu(Button? button)
    {
        if (button?.ContextMenu is null)
            return;

        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.IsOpen = true;
    }
}