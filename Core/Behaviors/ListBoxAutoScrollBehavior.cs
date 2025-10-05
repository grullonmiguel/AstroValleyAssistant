using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroValleyAssistant.Core.Behaviors
{
    /// <summary>
    /// An attached behavior that automatically scrolls a ListBox to its selected item
    /// when the selection changes programmatically.
    /// </summary> 
    public class ListBoxScrollBehavior : Behavior<ListBox>
    {
        private ScrollViewer? _scrollViewer;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += OnSelectionChanged;
            AssociatedObject.Loaded += OnListBoxLoaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
            AssociatedObject.Loaded -= OnListBoxLoaded;
            base.OnDetaching();
        }

        private void OnListBoxLoaded(object sender, RoutedEventArgs e)
        {
            // Find the ScrollViewer within the ListBox's template once it's loaded.
            _scrollViewer = FindVisualChild<ScrollViewer>(AssociatedObject);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_scrollViewer == null || AssociatedObject.SelectedItem == null)
            {
                return;
            }

            // --- CORRECTED LOGIC ---

            // 1. Get the index of the currently selected item.
            var selectedIndex = AssociatedObject.Items.IndexOf(AssociatedObject.SelectedItem);
            if (selectedIndex < 0)
            {
                return;
            }

            // 2. Calculate the target index for the TOP of the viewport. This will
            //    position the selected item in the middle of the visible area.
            //    When CanContentScroll is true (default for ListBox), ViewportHeight
            //    is the number of visible items, not a pixel value.
            double centerIndex = selectedIndex - (_scrollViewer.ViewportHeight / 2);

            // 3. Scroll to the calculated index. The ScrollToVerticalOffset method, when
            //    used with logical scrolling, scrolls to an item index. The ScrollViewer
            //    automatically handles clamping the value if it's near the top or bottom.
            _scrollViewer.ScrollToVerticalOffset(centerIndex);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : Visual
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
