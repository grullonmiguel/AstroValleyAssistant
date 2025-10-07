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
