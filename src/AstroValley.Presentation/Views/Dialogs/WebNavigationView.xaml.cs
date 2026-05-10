using System.Windows.Controls;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Views.Dialogs;

public partial class WebNavigationView : UserControl
{
    public WebNavigationView()
    {
        InitializeComponent();

        // Track URL changes as user navigates
        WebView.NavigationCompleted += (s, e) =>
        {
            if (DataContext is WebNavigationDialogViewModel vm && e.IsSuccess)
            {
                vm.CurrentUrl = WebView.Source?.ToString() ?? vm.InitialUrl;
            }
        };

        // Intercept new window requests and navigate in the same window
        WebView.CoreWebView2InitializationCompleted += (s, e) =>
        {
            if (e.IsSuccess && WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.NewWindowRequested += (sender, args) =>
                {
                    // Cancel the new window request
                    args.Handled = true;

                    // Navigate in the current window instead
                    WebView.CoreWebView2.Navigate(args.Uri);
                };

                // Track navigation history changes
                WebView.CoreWebView2.HistoryChanged += (sender, args) =>
                {
                    if (DataContext is WebNavigationDialogViewModel vm)
                    {
                        vm.CanGoBack = WebView.CoreWebView2.CanGoBack;
                        vm.GoBackCommand.NotifyCanExecuteChanged();
                    }
                };
            }
        };

        Loaded += (s, e) =>
        {
            if (DataContext is WebNavigationDialogViewModel vm)
            {
                // Wire up back navigation
                vm.OnGoBackRequested = () =>
                {
                    if (WebView.CoreWebView2?.CanGoBack == true)
                    {
                        WebView.CoreWebView2.GoBack();
                    }
                };
            }
        };
    }
}
