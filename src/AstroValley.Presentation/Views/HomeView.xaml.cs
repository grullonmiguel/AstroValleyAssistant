using AstroValley.Presentation.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media.Animation;

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

            if (DataContext is HomeViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.MarkersAdded += PushMarkers;
                vm.FilterChanged += () => _ = PushFilter();
                vm.TerrainToggleRequested += () => _ = RunScript("toggle3d();");
                vm.OverlayToggleRequested += key => _ = RunScript($"toggleOverlay('{key}');");
                vm.TourToggleRequested += () => _ = RunScript("toggleTour();");
                vm.MarkerRemoved += async m =>
                {
                    if (!_pageLoaded) return;
                    var (lat, lon) = (Inv(m.Latitude), Inv(m.Longitude));
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"removeMarker({lat}, {lon});");
                };
                vm.MarkersCleared += async () =>
                {
                    if (_pageLoaded) await MapWebView.CoreWebView2.ExecuteScriptAsync("clearMarkers();");
                };
            }

            // Own browser profile, so Home's composition-hosted WebView2 never shares an environment with the
            // regular WebView2 used by the Real Auction dialog (sharing one folder left that dialog blank).
            MapWebView.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AstroValleyAssistant", "WebView2Home")
            };
            await MapWebView.EnsureCoreWebView2Async();
            // https origin (not file://) so OSM tile servers receive a Referer and don't block us.
            MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.local",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"),
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            // Pin popups link to Google Maps / Street View; open those in the default browser, not inside the map.
            MapWebView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri, UseShellExecute = true });
            };
            MapWebView.NavigationCompleted += (_, _) =>
            {
                _pageLoaded = true;
                HighlightSelectedState();
                if (DataContext is HomeViewModel { Markers.Count: > 0 } loaded) PushMarkers(loaded.Markers);
            };
            // homemaplibre.html = MapLibre globe; homeglobe.html = Cesium globe; homemap.html = Leaflet flat map.
            // Same functions/messages in all three, so switching is this one name.
            MapWebView.CoreWebView2.Navigate("https://appassets.local/homemaplibre.html");
            // Safety net: never leave the cover up if the page can't report ready (e.g. offline tiles).
            await Task.Delay(TimeSpan.FromSeconds(20));
            HideLoading();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedState)) HighlightSelectedState();
        else if (e.PropertyName == nameof(HomeViewModel.SelectedCounty)) HighlightSelectedCounty();
        else if (e.PropertyName == nameof(HomeViewModel.IsSidebarOpen) && sender is HomeViewModel sb)
            _ = RunScript($"setSidebarOpen({(sb.IsSidebarOpen ? "true" : "false")});");
        else if (e.PropertyName == nameof(HomeViewModel.IsImagery) && _pageLoaded && sender is HomeViewModel vm)
            _ = MapWebView.CoreWebView2.ExecuteScriptAsync($"setImagery({(vm.IsImagery ? "true" : "false")});");
    }

    // The map posts the selected state's counties once its county GeoJSON has loaded.
    private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;

        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetString();
        if (type == "ready") { HideLoading(); return; }
        if (type == "pinEdit")
        {
            vm.UpdatePin(root.GetProperty("lat").GetDouble(), root.GetProperty("lon").GetDouble(),
                root.GetProperty("status").GetString(), root.GetProperty("notes").GetString(),
                root.GetProperty("accessStatus").GetString(),
                root.GetProperty("accessFt").ValueKind == JsonValueKind.Number ? root.GetProperty("accessFt").GetDouble() : null,
                root.GetProperty("accessRoad").GetString(), root.GetProperty("accessChecked").GetString());
            return;
        }
        if (type == "mode")
        {
            vm.IsTerrain = root.GetProperty("terrain").GetBoolean();
            vm.IsTouring = root.GetProperty("touring").GetBoolean();
            vm.IsFlood = root.GetProperty("flood").GetBoolean();
            vm.IsWetlands = root.GetProperty("wetlands").GetBoolean();
            vm.IsEpa = root.GetProperty("epa").GetBoolean();
            return;
        }
        if (type != "counties") return;
        // Ignore a late reply for a state that is no longer selected.
        if (root.GetProperty("state").GetString() != vm.SelectedState?.Name) return;

        vm.Counties.Clear();
        foreach (var item in root.GetProperty("items").EnumerateArray())
            vm.Counties.Add(new CountyItem(item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!));
    }

    private void OnMenuClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        PinsMenu.PlacementTarget = MenuButton;
        PinsMenu.IsOpen = true;
    }

    private void HideLoading()
    {
        if (MapLoading.Visibility != System.Windows.Visibility.Visible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
        fade.Completed += (_, _) => MapLoading.Visibility = System.Windows.Visibility.Collapsed;
        MapLoading.BeginAnimation(OpacityProperty, fade);
    }

    // Before the page loads this is a no-op: NavigationCompleted pushes every stored pin.
    private async void PushMarkers(IReadOnlyList<AstroValley.Domain.Models.MarkerLocation> locations)
    {
        if (!_pageLoaded) return;
        // Property names stay PascalCase to match the JS (loc.Latitude, loc.ParcelID ...).
        var json = JsonSerializer.Serialize(locations, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await MapWebView.CoreWebView2.ExecuteScriptAsync($"addMarkers({json});");
        await PushFilter(); // new pins may be hidden by the active filter
    }

    private async Task RunScript(string script)
    {
        if (_pageLoaded) await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    // Tells the map which pins the active filter hides (same "lat,lon" key the map uses).
    private Task PushFilter()
    {
        if (DataContext is not HomeViewModel vm) return Task.CompletedTask;
        var keys = vm.HiddenMarkers.Select(m => $"{Inv(m.Latitude)},{Inv(m.Longitude)}");
        return RunScript($"setHidden({JsonSerializer.Serialize(keys)});");
    }

    // Invariant culture so the decimal separator is always '.' in scripts.
    private static string Inv(double d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async void OnParcelClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_pageLoaded || sender is not ListBoxItem { DataContext: AstroValley.Domain.Models.MarkerLocation m }) return;

        // A click on the row's trash button must remove the pin, not fly to it.
        for (var d = e.OriginalSource as System.Windows.DependencyObject; d != null && d != sender; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is Button) return;

        await MapWebView.CoreWebView2.ExecuteScriptAsync($"flyToMarker({Inv(m.Latitude)}, {Inv(m.Longitude)});");
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
