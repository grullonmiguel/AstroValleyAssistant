using AstroValley.Presentation.ViewModels.Pages;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Switches the page shown in the main content area.
/// </summary>
public interface INavigationService
{
    IPageViewModel? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    void NavigateTo<TPage>() where TPage : PageViewModelBase;

    void NavigateTo(Type pageType);
}
