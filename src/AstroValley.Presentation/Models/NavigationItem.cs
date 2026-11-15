using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Models;

/// <summary>One entry in the sidebar. <paramref name="PageType"/> is the page view-model it opens.</summary>
public sealed record NavigationItem(string Title, PackIconKind Icon, Type PageType);
