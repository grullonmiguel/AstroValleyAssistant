using System.Windows;
using System.Windows.Controls;

namespace AstroValleyAssistant.Views
{
    /// <summary>
    /// Interaction logic for RegridView.xaml
    /// </summary>
    public partial class RegridView : UserControl
    {
        public RegridView()
        {
            InitializeComponent();
        }

        private void PasteButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
}
