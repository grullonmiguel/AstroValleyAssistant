using System.Globalization;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    public class DragPromptConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value
                ? "Release to import file"
                : "Drag and Drop file or";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
