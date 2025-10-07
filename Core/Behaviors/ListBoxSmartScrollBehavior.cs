using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroValleyAssistant.Core.Behaviors
{
    public static class ListBoxSmartScrollBehavior
    {
        private static bool _isScrolling;

        // --- Property to Sync Scroll Position ---
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ListBoxSmartScrollBehavior), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static double GetVerticalOffset(DependencyObject obj) => (double)obj.GetValue(VerticalOffsetProperty);
        public static void SetVerticalOffset(DependencyObject obj, double value) => obj.SetValue(VerticalOffsetProperty, value);

        // --- Property to Trigger Auto-Scroll ---
        public static readonly DependencyProperty ScrollToSelectedItemProperty =
            DependencyProperty.RegisterAttached("ScrollToSelectedItem", typeof(object), typeof(ListBoxSmartScrollBehavior), new PropertyMetadata(null, OnScrollToSelectedItemChanged));

        public static object GetScrollToSelectedItem(DependencyObject obj) => obj.GetValue(ScrollToSelectedItemProperty);
        public static void SetScrollToSelectedItem(DependencyObject obj, object value) => obj.SetValue(ScrollToSelectedItemProperty, value);

        // --- Behavior Logic ---
        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListBox listBox) return;
            var scrollViewer = FindScrollViewer(listBox);
            if (scrollViewer == null) return;

            listBox.Loaded -= OnListBoxLoaded;
            listBox.Loaded += OnListBoxLoaded;

            // Restore scroll position when loaded
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }

        private static void OnListBoxLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindScrollViewer(sender as ListBox);
            if (scrollViewer == null) return;

            scrollViewer.ScrollChanged -= OnScrollChanged;
            scrollViewer.ScrollChanged += OnScrollChanged;
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // When the user scrolls manually, save the position back to the ViewModel
            if (_isScrolling) return;
            var scrollViewer = sender as ScrollViewer;
            SetVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);
        }

        private static void OnScrollToSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListBox listBox || e.NewValue == null) return;

            _isScrolling = true; // Prevent OnScrollChanged from saving this programmatic scroll
            listBox.Dispatcher.InvokeAsync(() =>
            {
                var scrollViewer = FindScrollViewer(listBox);
                if (scrollViewer == null)
                {
                    _isScrolling = false;
                    return;
                }

                var selectedItem = listBox.ItemContainerGenerator.ContainerFromItem(e.NewValue) as FrameworkElement;
                if (selectedItem == null)
                {
                    _isScrolling = false;
                    return;
                }

                double center = selectedItem.TranslatePoint(new Point(0, selectedItem.ActualHeight / 2), scrollViewer).Y;
                double targetOffset = scrollViewer.VerticalOffset + center - (scrollViewer.ViewportHeight / 2);
                scrollViewer.ScrollToVerticalOffset(targetOffset);

                _isScrolling = false;
            });
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
