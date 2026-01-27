using AstroValleyAssistant.ViewModels.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AstroValleyAssistant.Views.Dialogs
{
    public partial class ImportView : UserControl
    {
        public ImportView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ImportViewModel vm)
            {
                vm.PreviewRows.CollectionChanged += (_, __) => BuildColumns(vm);
                BuildColumns(vm);
            }
        }

        private void BuildColumns(ImportViewModel vm)
        {
            if (PreviewGrid == null)
                return;

            PreviewGrid.Columns.Clear();

            foreach (var col in vm.DetectedColumns)
            {
                PreviewGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = col,
                    Binding = new Binding($"[{col}]")
                });
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (DataContext is ImportViewModel vm)
                vm.IsDragOver = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            if (DataContext is ImportViewModel vm)
                vm.IsDragOver = false;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (DataContext is ImportViewModel vm)
            {
                vm.IsDragOver = false;

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0)
                        vm.LoadFile(files[0]);
                }
            }
        }
    }
}
