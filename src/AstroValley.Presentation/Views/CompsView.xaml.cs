using System.IO;
using System.Windows.Controls;
using AstroValley.Presentation.ViewModels;

namespace AstroValley.Presentation.Views;

public partial class CompsView : UserControl
{
    private bool _initialized;

    // A comp's address: open its listing in the default browser (not the embedded one).
    private void OnListingClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CompsViewModel vm && sender is System.Windows.FrameworkElement { DataContext: AstroValley.Domain.Models.Comp comp })
            vm.OpenListing(comp.Url);
    }

    public CompsView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (_initialized || DataContext is not CompsViewModel vm) return; // the view is kept alive; start the browser once
            _initialized = true;

            // Own profile folder (a shared one blanked other WebView2 views); it also keeps the sites' cookies between sessions.
            WebView.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant", "WebView2Comps")
            };
            try { await WebView.EnsureCoreWebView2Async(); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"The browser could not start:\n{ex.Message}", "Comps");
                return;
            }

            var reader = await File.ReadAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "compsreader.js"));
            // Runtime.evaluate waits for the reader's promise (it scrolls Zillow's list first); ExecuteScriptAsync would return "{}"
            vm.ReadPage = async () =>
            {
                var reply = await WebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
                    System.Text.Json.JsonSerializer.Serialize(new { expression = reader, awaitPromise = true, returnByValue = true }));
                using var doc = System.Text.Json.JsonDocument.Parse(reply);
                if (doc.RootElement.TryGetProperty("exceptionDetails", out var error))
                    throw new InvalidOperationException(error.TryGetProperty("exception", out var ex) && ex.TryGetProperty("description", out var d) ? d.GetString() : error.GetRawText());
                return doc.RootElement.GetProperty("result").TryGetProperty("value", out var value) ? value.GetRawText() : "[]";
            };
            vm.NavigateTo = url => WebView.CoreWebView2.Navigate(url);
            vm.GoBackRequested = () => { if (WebView.CoreWebView2.CanGoBack) WebView.CoreWebView2.GoBack(); };
            WebView.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; WebView.CoreWebView2.Navigate(e.Uri); }; // stay in this view
            WebView.CoreWebView2.HistoryChanged += (_, _) => vm.CanGoBack = WebView.CoreWebView2.CanGoBack;
            WebView.CoreWebView2.Navigate(vm.StartUrl);

            // The map: same profile, started once; the VM loads pins into it and gets each pick back live.
            MapWebView.CreationProperties = WebView.CreationProperties;
            try { await MapWebView.EnsureCoreWebView2Async(); }
            catch (Exception ex) { System.Windows.MessageBox.Show($"The map could not start:\n{ex.Message}", "Comps"); return; }
            MapWebView.CoreWebView2.NewWindowRequested += (_, e) => // listing links open in the default browser
            {
                e.Handled = true;
                vm.OpenListing(e.Uri);
            };
            MapWebView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.GetString() == "pick")
                    vm.OnMapPick(root.GetProperty("key").GetString() ?? "", root.GetProperty("excluded").GetBoolean());
            };
            var mapLoaded = new TaskCompletionSource();
            vm.LoadMap = async html =>
            {
                mapLoaded = new TaskCompletionSource();
                MapWebView.CoreWebView2.NavigationCompleted += Done;
                void Done(object? s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs a) { MapWebView.CoreWebView2.NavigationCompleted -= Done; mapLoaded.TrySetResult(); }
                MapWebView.NavigateToString(html);
                await mapLoaded.Task; // shown only once loaded, so the old map never flashes
            };
            vm.RunMapScript = js => MapWebView.CoreWebView2.ExecuteScriptAsync(js);
        };
    }
}
