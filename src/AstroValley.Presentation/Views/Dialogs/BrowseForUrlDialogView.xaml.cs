using AstroValley.Presentation.ViewModels.Dialogs;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AstroValley.Presentation.Views.Dialogs;

/// <summary>
/// WebView2 plumbing the view-model cannot do: a browser per dialog, its profile folder, popups, Back, and reporting where it is.
/// </summary>
public partial class BrowseForUrlDialogView : UserControl
{
    // Own profile folder (a shared one blanked other WebView2 views); it also keeps site sign-ins between sessions.
    private static readonly string ProfileFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant", "WebView2Browse");

    private WebView2? _webView;

    public BrowseForUrlDialogView()
    {
        InitializeComponent();

        // DialogHost reuses this view for the next dialog of the same kind (it only swaps the DataContext), so a new browser is
        // made for every dialog: it always starts on the dialog's start page, with no history from the last one.
        Loaded += (_, _) => CreateBrowser();
        Unloaded += (_, _) => DisposeBrowser();
        DataContextChanged += (_, _) => { if (IsLoaded) CreateBrowser(); };
    }

    private void CreateBrowser()
    {
        DisposeBrowser();
        if (DataContext is not BrowseForUrlDialogViewModel vm) return;

        var webView = new WebView2 { CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = ProfileFolder } };

        webView.NavigationCompleted += (_, e) =>
        {
            if (e.IsSuccess && webView.Source is { } source)
                vm.CurrentUrl = source.ToString();
        };

        webView.CoreWebView2InitializationCompleted += (_, e) =>
        {
            // Otherwise a failed start-up just leaves a blank dialog.
            if (!e.IsSuccess)
            {
                MessageBox.Show($"The browser could not start:\n{e.InitializationException?.Message}", vm.Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Links that open a new window stay in this one, so the page can still be selected.
            webView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                webView.CoreWebView2.Navigate(args.Uri);
            };
            webView.CoreWebView2.HistoryChanged += (_, _) => BackButton.IsEnabled = webView.CanGoBack;
        };

        _webView = webView;
        BrowserHost.Child = webView;
        webView.Source = new Uri(vm.StartUrl);
    }

    private void DisposeBrowser()
    {
        BackButton.IsEnabled = false;
        BrowserHost.Child = null;
        _webView?.Dispose();
        _webView = null;
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _webView?.GoBack();
}
