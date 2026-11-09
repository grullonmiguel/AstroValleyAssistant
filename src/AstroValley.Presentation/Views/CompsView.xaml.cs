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
            vm.ReadPage = async () => await WebView.CoreWebView2.ExecuteScriptAsync(reader);
            vm.NavigateTo = url => WebView.CoreWebView2.Navigate(url);
            vm.GoBackRequested = () => { if (WebView.CoreWebView2.CanGoBack) WebView.CoreWebView2.GoBack(); };
            WebView.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; WebView.CoreWebView2.Navigate(e.Uri); }; // stay in this view
            WebView.CoreWebView2.HistoryChanged += (_, _) => vm.CanGoBack = WebView.CoreWebView2.CanGoBack;
            WebView.CoreWebView2.Navigate(vm.StartUrl);
        };
    }
}
