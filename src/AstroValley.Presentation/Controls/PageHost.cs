using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AstroValley.Presentation.ViewModels.Pages;

namespace AstroValley.Presentation.Controls;

/// <summary>
/// Shows the view for the current page view-model with an entrance animation (fade + slide).
/// Views of keep-alive pages are created once and only hidden when the user leaves, so their state survives.
/// Views of non-keep-alive pages are removed when the user leaves.
/// The view for a view-model comes from the implicit DataTemplates in Views/ViewTemplates.xaml.
/// Durations and distances come from Styles/Tokens/Motion.xaml.
/// </summary>
public class PageHost : Grid
{
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(
        nameof(Page), typeof(object), typeof(PageHost), new PropertyMetadata(null, OnPageChanged));

    private readonly Dictionary<object, FrameworkElement> _views = new(ReferenceEqualityComparer.Instance);

    public PageHost()
    {
        ClipToBounds = true;
    }

    /// <summary>The page view-model to display.</summary>
    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    private static void OnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PageHost)d).ShowPage(e.OldValue, e.NewValue);

    private void ShowPage(object? oldPage, object? newPage)
    {
        if (oldPage is not null && _views.TryGetValue(oldPage, out var oldView))
        {
            if (oldPage is IPageViewModel { KeepAlive: false })
            {
                Children.Remove(oldView);
                _views.Remove(oldPage);
                oldView.DataContext = null;
            }
            else
            {
                oldView.Visibility = Visibility.Collapsed;
            }
        }

        if (newPage is null)
        {
            return;
        }

        if (!_views.TryGetValue(newPage, out var view))
        {
            view = CreateView(newPage);
            _views[newPage] = view;
            Children.Add(view);
        }

        view.Visibility = Visibility.Visible;
        PlayEntranceAnimation(view);
    }

    private FrameworkElement CreateView(object page)
    {
        if (TryFindResource(new DataTemplateKey(page.GetType())) is not DataTemplate template)
        {
            throw new InvalidOperationException(
                $"No DataTemplate found for {page.GetType().Name}. Add one to Views/ViewTemplates.xaml.");
        }

        if (template.LoadContent() is not FrameworkElement view)
        {
            throw new InvalidOperationException($"The DataTemplate for {page.GetType().Name} must produce a FrameworkElement.");
        }

        view.DataContext = page;
        return view;
    }

    private void PlayEntranceAnimation(FrameworkElement view)
    {
        var duration = TryFindResource("PageTransitionDuration") is Duration d ? d : new Duration(TimeSpan.FromMilliseconds(300));
        var offset = TryFindResource("PageTransitionOffset") is double o ? o : 28d;
        var easing = TryFindResource("StandardEase") as IEasingFunction;

        var translate = new TranslateTransform(0, 0);
        view.RenderTransform = translate;

        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offset, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        view.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }
}
