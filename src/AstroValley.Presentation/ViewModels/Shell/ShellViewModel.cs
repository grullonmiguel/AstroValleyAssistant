using AstroValley.Presentation.Helpers;
using AstroValley.Presentation.Models;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System.Reflection;

namespace AstroValley.Presentation.ViewModels.Shell;

/// <summary>
/// Drives the main window: sidebar, current page and status bar.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;

    public ShellViewModel(INavigationService navigation, ISettingsService settings, ISnackbarService snackbar)
    {
        _navigation = navigation;
        _settings = settings;
        SnackbarMessageQueue = snackbar.MessageQueue;

        isSidebarExpanded = settings.Current.IsSidebarExpanded;

        NavigationItems =
        [
            new NavigationItem("Home", PackIconKind.HomeOutline, typeof(HomeViewModel)),
            new NavigationItem("Parcel Lookup", PackIconKind.MapSearchOutline, typeof(ParcelLookupViewModel)),
            //new NavigationItem("Regrid", PackIconKind.FlaskOutline, typeof(RegridViewModel)),
            //new NavigationItem("About", PackIconKind.InformationOutline, typeof(AboutViewModel)),
        ];

        _navigation.CurrentPageChanged += OnCurrentPageChanged;
        SelectedNavigationItem = NavigationItems[0];
    }

    public string AppName => AppDefaults.AppName;

    public string Version { get; } = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public ISnackbarMessageQueue SnackbarMessageQueue { get; }

    [ObservableProperty]
    private bool isSidebarExpanded;

    [ObservableProperty]
    private NavigationItem? selectedNavigationItem;

    [ObservableProperty]
    private IPageViewModel? currentPage;

    [RelayCommand]
    private void OpenSettings() => _navigation.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    partial void OnIsSidebarExpandedChanged(bool value)
    {
        _settings.Current.IsSidebarExpanded = value;
        _settings.Save();
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            _navigation.NavigateTo(value.PageType);
        }
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        CurrentPage = _navigation.CurrentPage;

        // Keep the highlighted sidebar item in sync when code navigates (not only when the user clicks).
        var type = CurrentPage?.GetType();
        SelectedNavigationItem = NavigationItems.FirstOrDefault(item => item.PageType == type);
    }
}
