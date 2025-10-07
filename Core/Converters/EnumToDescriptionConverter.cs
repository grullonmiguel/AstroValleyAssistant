using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace AstroValleyAssistant.Core.Converters
{
    public class EnumToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            Enum myEnum = (Enum)value;
            return GetEnumDescription(myEnum);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private string GetEnumDescription(Enum enumObj)
        {
            FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());
            if (fieldInfo == null) return enumObj.ToString();

            object[] attribArray = fieldInfo.GetCustomAttributes(false);

            if (attribArray.Length == 0)
            {
                return enumObj.ToString();
            }

            var attrib = attribArray[0] as DescriptionAttribute;
            return attrib?.Description ?? enumObj.ToString();
        }
    }
}
