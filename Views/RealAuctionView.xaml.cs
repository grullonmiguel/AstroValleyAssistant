using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace AstroValleyAssistant.Views
{
    public partial class RealAuctionView : UserControl
    {
        public RealAuctionView()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
