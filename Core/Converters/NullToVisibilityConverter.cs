using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value. If the value is null, returns Collapsed. Otherwise, returns Visible.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle strings: null, empty, or just spaces should collapse the button
            if (value is string s)
            {
                return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
            }

            // Handle objects: null should collapse the sidebar/button
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// This method is not used.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
