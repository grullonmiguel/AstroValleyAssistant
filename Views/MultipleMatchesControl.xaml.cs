using AstroValleyAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValleyAssistant.Views
{
    public partial class MultipleMatchesControl : UserControl
    {
        public MultipleMatchesControl()
        {
            InitializeComponent();
        }

        public ICommand? SelectMatchCommand
        {
            get => (ICommand)GetValue(SelectMatchCommandProperty);
            set => SetValue(SelectMatchCommandProperty, value);
        }
        public static readonly DependencyProperty SelectMatchCommandProperty = DependencyProperty.Register(nameof(SelectMatchCommand), typeof(ICommand), typeof(MultipleMatchesControl));

        public PropertyDataViewModel? SelectedItem
        {
            get => (PropertyDataViewModel?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(PropertyDataViewModel), typeof(MultipleMatchesControl));

    }
}