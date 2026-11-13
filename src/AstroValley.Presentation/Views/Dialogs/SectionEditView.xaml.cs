using System.Windows;
using System.Windows.Controls;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Views.Dialogs;

public partial class SectionEditView : UserControl
{
    public SectionEditView()
    {
        InitializeComponent();
        // the dialog's body is the template named after the section ("Edit.Property", "Edit.Purchase", ...)
        DataContextChanged += (_, _) => Body.ContentTemplate = DataContext is SectionEditViewModel vm ? (DataTemplate)FindResource("Edit." + vm.Section) : null;
    }
}
