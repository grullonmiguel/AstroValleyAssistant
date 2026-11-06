using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroValley.Presentation.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;
}
