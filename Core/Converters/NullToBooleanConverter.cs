using System.Globalization;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    // <summary>
    /// Converts a value to a boolean based on whether it is null.
    /// Returns true if the value is null, false otherwise.
    /// Supports an optional "Invert" parameter to reverse the result.
    /// </summary>

    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Determine if the value is null
            bool isNull = value == null;

            // Check if inversion is requested
            bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;

            return invert ? !isNull : isNull;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
