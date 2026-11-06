using AstroValley.Presentation.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace AstroValley.Presentation.Views;

public partial class HomeView : UserControl
{
    private bool _initialized;
    private bool _pageLoaded;

    public HomeView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_initialized) return; // view is kept alive; load the map once
            _initialized = true;

            if (DataContext is HomeViewModel vm) vm.PropertyChanged += OnViewModelPropertyChanged;

            await MapWebView.EnsureCoreWebView2Async();
            // https origin (not file://) so OSM tile servers receive a Referer and don't block us.
            MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.local",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"),
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            MapWebView.NavigationCompleted += (_, _) =>
            {
                _pageLoaded = true;
                HighlightSelectedState();
            };
            MapWebView.CoreWebView2.Navigate("https://appassets.local/homemap.html");
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedState)) HighlightSelectedState();
    }

    private async void HighlightSelectedState()
    {
        if (!_pageLoaded || DataContext is not HomeViewModel vm) return;
        // JSON-encode so names are safely escaped as a JS string (or null when nothing is selected).
        var name = JsonSerializer.Serialize(vm.SelectedState?.Name);
        await MapWebView.CoreWebView2.ExecuteScriptAsync($"highlightState({name});");
    }
}
