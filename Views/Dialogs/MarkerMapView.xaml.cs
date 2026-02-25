using AstroValleyAssistant.ViewModels.Dialogs;
using System.Collections.Specialized;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Controls;

namespace AstroValleyAssistant.Views.Dialogs
{
    /// <summary>
    /// Handles the bridge between the  
    /// MarkerMapViewModel and the WebView2 JavaScript environment.
    /// </summary>
    public partial class MarkerMapView : UserControl
    {
        private MarkerMapViewModel? _viewModel;

        public MarkerMapView()
        {
            InitializeComponent();

            // Subscribe to the Loaded event to initialize the browser
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel = DataContext as MarkerMapViewModel;

            if (_viewModel != null)
            {
                // Subscribe to collection changes to update the map in real-time
                _viewModel.Markers.CollectionChanged += OnMarkersCollectionChanged;

                await InitializeWebViewAsync();
            }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.Markers.CollectionChanged -= OnMarkersCollectionChanged;
            }
        }

        private async Task InitializeWebViewAsync()
        {
            LoadingOverlay.Visibility = System.Windows.Visibility.Visible;

            try
            {

                // 1. Initialize the underlying Chromium engine
                await MapWebView.EnsureCoreWebView2Async();

                // ENABLE TROUBLESHOOTING TOOLS
                MapWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                MapWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                // Listen for messages from JavaScript
                MapWebView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    string message = e.TryGetWebMessageAsString();
                    if (_viewModel != null) _viewModel.Status = $"JS Error: {message}";
                };

                // 2. Configure the 'Bridge' to open external links in the default OS browser
                // This is essential for the Google Maps/Street View links to work properly
                MapWebView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    // Block the WebView from opening a new window internally
                    e.Handled = true;

                    // Start the system process to open the URL (e.g., in Chrome or Edge)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true // Required for .NET Core/5+ to launch external apps
                    });
                };

                // 3. Resolve the path to our local HTML asset
                // Note: Included the 'MarkerMap' subfolder as per our updated hierarchy
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "Assets", "markermap.html");

                if (!File.Exists(htmlPath))
                {
                    throw new FileNotFoundException("Could not find the map asset file.", htmlPath);
                }

                // 4. Navigate to the local map
                MapWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);

                // 5. Wire up the post-load synchronization
                MapWebView.NavigationCompleted += (s, args) =>
                {
                    LoadingOverlay.Visibility = System.Windows.Visibility.Collapsed;

                    // Initial sync to plot any markers already present in the Singleton ViewModel
                    SyncMarkers(true);
                };
            }
            catch (Exception ex)
            {
                // Provide feedback via the ViewModel status property
                if (_viewModel != null)
                {
                    _viewModel.Status = $"Map Init Failed: {ex.Message}";
                }
            }
        }

        private async void OnMarkersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Decide whether to append markers or refresh the whole map
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // Efficiently add only the new items
                    var newItems = e.NewItems?.Cast<Models.MarkerLocation>();
                    if (newItems != null) await PushMarkersToJs(newItems, false);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Map was cleared, tell JS to reset
                    await MapWebView.CoreWebView2.ExecuteScriptAsync("clearMap();");
                    break;

                default:
                    // For complex changes, just sync the whole list
                    SyncMarkers(true);
                    break;
            }
        }

        private async void SyncMarkers(bool clearExisting)
        {
            if (_viewModel?.Markers != null)
            {
                await PushMarkersToJs(_viewModel.Markers, clearExisting);
            }
        }

        private async Task PushMarkersToJs(IEnumerable<Models.MarkerLocation> locations, bool clearExisting)
        {
            if (MapWebView.CoreWebView2 == null) return;

            var options = new JsonSerializerOptions
            {
                // Keeps property names as PascalCase (Latitude, Longitude, etc.)
                PropertyNamingPolicy = null,
                // CRITICAL: This ensures internal quotes in your Excel strings are escaped
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Serialize data for JS (preserve PascalCase to match interop.js)
            string json = JsonSerializer.Serialize(locations, options);

            // We escape backslashes and single quotes so the JS string literal doesn't break
            string escapedJson = json.Replace("\\", "\\\\").Replace("'", "\\'");

            await MapWebView.CoreWebView2.ExecuteScriptAsync($"renderMarkers('{escapedJson}', true);");

            // Execute the JS function defined in markermap_interop.js
            //string script = $"renderMarkers('{json.Replace("'", "\\'")}', {clearExisting.ToString().ToLower()});";
            //await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async void MarkerList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Models.MarkerLocation selected)
            {
                if (MapWebView.CoreWebView2 != null)
                {
                    // Call our JS focus function with the selected coordinates
                    string script = $"focusMarker({selected.Latitude}, {selected.Longitude});";
                    await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
        }
    }
}
