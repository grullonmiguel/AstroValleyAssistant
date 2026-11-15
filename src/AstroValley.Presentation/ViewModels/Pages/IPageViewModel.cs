namespace AstroValley.Presentation.ViewModels.Pages;

/// <summary>
/// What the navigation system and PageHost need to know about a page.
/// </summary>
public interface IPageViewModel
{
    string Title { get; }

    /// <summary>
    /// True: the view-model and its view are created once and kept (hidden) while other pages are shown,
    /// so typed text, scroll position, live connections and so on persist.
    /// False: both are created fresh on every visit and thrown away when the user leaves.
    /// </summary>
    bool KeepAlive { get; }
}
