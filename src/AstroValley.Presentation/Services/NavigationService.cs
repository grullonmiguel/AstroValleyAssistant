using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AstroValley.Presentation.Services;

/// <summary>
/// Creates page view-models through DI and remembers the ones that opt in to keep-alive
/// (<see cref="IPageViewModel.KeepAlive"/>), so their data survives while the user visits other pages.
/// Pages that do not keep alive are created fresh on every visit and disposed when left.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NavigationService> _logger;
    private readonly Dictionary<Type, PageViewModelBase> _keptAlive = [];

    public NavigationService(IServiceProvider services, ILogger<NavigationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public IPageViewModel? CurrentPage { get; private set; }

    public event EventHandler? CurrentPageChanged;

    public TPage NavigateTo<TPage>() where TPage : PageViewModelBase
    {
        NavigateTo(typeof(TPage));
        return (TPage)CurrentPage!;
    }

    public void NavigateTo(Type pageType)
    {
        if (!typeof(PageViewModelBase).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"{pageType.Name} must derive from {nameof(PageViewModelBase)}.", nameof(pageType));
        }

        if (CurrentPage?.GetType() == pageType)
        {
            return;
        }

        var next = ResolvePage(pageType);

        if (CurrentPage is PageViewModelBase previous)
        {
            previous.OnNavigatedFrom();
            if (!previous.KeepAlive)
            {
                (previous as IDisposable)?.Dispose();
            }
        }

        CurrentPage = next;
        next.OnNavigatedTo();
        _logger.LogDebug("Navigated to {Page}", pageType.Name);
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }

    private PageViewModelBase ResolvePage(Type pageType)
    {
        if (_keptAlive.TryGetValue(pageType, out var existing))
        {
            return existing;
        }

        var page = (PageViewModelBase)_services.GetRequiredService(pageType);
        if (page.KeepAlive)
        {
            _keptAlive[pageType] = page;
        }

        return page;
    }
}
