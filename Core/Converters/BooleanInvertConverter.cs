using System.Globalization;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    /// <summary>
    /// Inverts a boolean value. True → False, False → True.
    /// </summary>
    public class BooleanInvertConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool flag && !flag;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool flag && !flag;
        }
    }
}