using System.Windows;
using System.Windows.Controls;

namespace AstroValleyAssistant.Core.Behaviors
{
    public class ComboBoxItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SelectedTemplate { get; set; }
        public DataTemplate DropDownTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // container is commonly a ContentPresenter; determine context by ancestry
            var comboBoxItem = FindAncestor<ComboBoxItem>(container);
            return comboBoxItem != null ? DropDownTemplate : SelectedTemplate;
        }

        private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }


}
