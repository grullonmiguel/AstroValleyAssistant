using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value.
    /// True → Visible, False → Collapsed.
    /// Supports an optional "Invert" parameter to reverse the logic.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Ensure the input is a boolean
            if (value is not bool flag)
                return Visibility.Collapsed;

            // Check if the caller wants to invert the logic
            bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;

            if (invert)
                flag = !flag;

            // Standard boolean-to-visibility mapping
            return flag 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is optional; implement only if needed
            if (value is Visibility visibility)
            {
                bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
                bool result = visibility == Visibility.Visible;
                return invert ? !result : result;
            }

            return false;
        }
    }
}