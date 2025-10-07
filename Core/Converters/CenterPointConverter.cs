using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    public class CenterPointConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not Rect bounds || values[1] is not double elementSize)
            {
                return 0;
            }

            string coordinate = parameter as string ?? "X";

            if (coordinate.Equals("X", StringComparison.OrdinalIgnoreCase))
            {
                // Calculate center of the state and subtract half the label's width
                return bounds.Left + (bounds.Width / 2) - (elementSize / 2);
            }

            // Calculate center of the state and subtract half the label's height
            return bounds.Top + (bounds.Height / 2) - (elementSize / 2);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
