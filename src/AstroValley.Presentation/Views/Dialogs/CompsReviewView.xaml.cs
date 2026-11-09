using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Views.Dialogs;

public partial class CompsReviewView : UserControl
{
    private bool _started;

    public CompsReviewView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_started || DataContext is not CompsReviewViewModel vm) return;
            _started = true;
            Web.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant", "WebView2Comps")
            };
            try { await Web.EnsureCoreWebView2Async(); }
            catch (Exception ex) { System.Windows.MessageBox.Show($"The map could not start:\n{ex.Message}", "Review comps"); return; }
            Web.CoreWebView2.NewWindowRequested += (_, e) => // listing links open in the default browser
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = u.AbsoluteUri, UseShellExecute = true });
            };
            Web.NavigateToString(vm.Html);
        };
    }

    // The page keeps the excluded keys in EXCLUDED; ask it for them.
    private async void OnDone(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not CompsReviewViewModel vm) return;
        try
        {
            var raw = await Web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(Object.keys(EXCLUDED).filter(function (k) { return EXCLUDED[k]; }))");
            vm.Finish(JsonSerializer.Deserialize<string[]>(JsonSerializer.Deserialize<string>(raw) ?? "[]") ?? []);
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not read the selection:\n{ex.Message}", "Review comps"); }
    }
}
