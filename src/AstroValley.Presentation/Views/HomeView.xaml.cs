using System.IO;
using System.Windows.Controls;

namespace AstroValley.Presentation.Views;

public partial class HomeView : UserControl
{
    private bool _initialized;

    public HomeView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_initialized) return; // view is kept alive; load the map once
            _initialized = true;
            await MapWebView.EnsureCoreWebView2Async();
            // https origin (not file://) so OSM tile servers receive a Referer and don't block us.
            MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.local",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"),
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            MapWebView.CoreWebView2.Navigate("https://appassets.local/homemap.html");
        };
    }
}
