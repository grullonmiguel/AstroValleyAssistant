using CommunityToolkit.Mvvm.ComponentModel;

namespace AstroValley.Presentation.ViewModels.Pages;

/// <summary>
/// Base class for every page shown in the main content area.
/// </summary>
public abstract class PageViewModelBase : ObservableObject, IPageViewModel
{
    public abstract string Title { get; }

    /// <summary>
    /// Override and return false for pages that should start fresh on every visit.
    /// </summary>
    public virtual bool KeepAlive => true;

    /// <summary>
    /// Called each time the page becomes the current page.
    /// </summary>
    public virtual void OnNavigatedTo()
    {
    }

    /// <summary>
    /// Called each time the user leaves the page.
    /// </summary>
    public virtual void OnNavigatedFrom()
    {
    }
}
