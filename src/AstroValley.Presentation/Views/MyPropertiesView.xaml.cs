using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;
using AstroValley.Presentation.ViewModels;

namespace AstroValley.Presentation.Views;

public partial class MyPropertiesView : UserControl
{
    public MyPropertiesView()
    {
        InitializeComponent();
        // the expense and neighbor grids change the property's lists in place, so the view says when to save: after a row is
        // committed (OnRowEditEnding) and after one is deleted
        foreach (var grid in new[] { ExpensesGrid, NeighborsGrid })
            ((INotifyCollectionChanged)grid.Items).CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Remove) SaveSelected();
            };
    }

    private void OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        // the row's values land after this event, so save once they have
        if (e.EditAction == DataGridEditAction.Commit) Dispatcher.BeginInvoke(SaveSelected, DispatcherPriority.Background);
    }

    private void SaveSelected() => (Detail.DataContext as OwnedPropertyViewModel)?.Touch();
}
