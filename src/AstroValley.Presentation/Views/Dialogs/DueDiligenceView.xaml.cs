using System.Windows.Controls;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Views.Dialogs;

public partial class DueDiligenceView : UserControl
{
    public DueDiligenceView() => InitializeComponent();

    // a status changed: refresh "n of m checked" (the items are plain objects, so the view model is told here)
    private void OnStatusChanged(object sender, SelectionChangedEventArgs e) => (DataContext as DueDiligenceViewModel)?.UpdateProgress();
}
