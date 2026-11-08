using System.Windows;
using System.Windows.Controls;

namespace AstroValley.Presentation.Views;

public partial class RegridView : UserControl
{
    public RegridView()
    {
        InitializeComponent();
    }

    private bool CanDrop(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is ViewModels.RegridViewModel { IsScraping: false };

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (CanDrop(e)) DropOverlay.Visibility = Visibility.Visible;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = CanDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnOverlayDragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (CanDrop(e) && DataContext is ViewModels.RegridViewModel vm && ((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault() is { } file)
            await vm.ImportFile(file);
    }

    private void PasteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }
}
