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
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            MapWebView.NavigationCompleted += (_, _) =>
            {
                _pageLoaded = true;
                HighlightSelectedState();
            };
            // homemaplibre.html = MapLibre globe; homeglobe.html = Cesium globe; homemap.html = Leaflet flat map.
            // Same functions/messages in all three, so switching is this one name.
            MapWebView.CoreWebView2.Navigate("https://appassets.local/homemaplibre.html");
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedState)) HighlightSelectedState();
        else if (e.PropertyName == nameof(HomeViewModel.SelectedCounty)) HighlightSelectedCounty();
        else if (e.PropertyName == nameof(HomeViewModel.IsImagery) && _pageLoaded && sender is HomeViewModel vm)
            _ = MapWebView.CoreWebView2.ExecuteScriptAsync($"setImagery({(vm.IsImagery ? "true" : "false")});");
    }

    // The map posts the selected state's counties once its county GeoJSON has loaded.
    private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;

        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        var root = doc.RootElement;
        if (root.GetProperty("type").GetString() != "counties") return;
        // Ignore a late reply for a state that is no longer selected.
        if (root.GetProperty("state").GetString() != vm.SelectedState?.Name) return;

        vm.Counties.Clear();
        foreach (var item in root.GetProperty("items").EnumerateArray())
            vm.Counties.Add(new CountyItem(item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!));
    }

    private async void HighlightSelectedCounty()
    {
        if (!_pageLoaded || DataContext is not HomeViewModel vm) return;
        var id = JsonSerializer.Serialize(vm.SelectedCounty?.Id);
        await MapWebView.CoreWebView2.ExecuteScriptAsync($"highlightCounty({id});");
    }

    private async void HighlightSelectedState()
    {
        if (!_pageLoaded || DataContext is not HomeViewModel vm) return;
        // JSON-encode so names are safely escaped as a JS string (or null when nothing is selected).
        var name = JsonSerializer.Serialize(vm.SelectedState?.Name);
        await MapWebView.CoreWebView2.ExecuteScriptAsync($"highlightState({name});");
    }
}
