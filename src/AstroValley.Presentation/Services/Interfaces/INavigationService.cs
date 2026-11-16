using AstroValley.Presentation.ViewModels.Pages;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Switches the page shown in the main content area.
/// </summary>
public interface INavigationService
{
    IPageViewModel? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    /// <summary>Shows the page and returns it (the kept-alive instance when it has one), so callers can hand it data.</summary>
    TPage NavigateTo<TPage>() where TPage : PageViewModelBase;

    void NavigateTo(Type pageType);
}
