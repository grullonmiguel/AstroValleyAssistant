using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroValleyAssistant.Themes.Controls
{
    public class IconTextButton : Button
    {
        static IconTextButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(IconTextButton), new FrameworkPropertyMetadata(typeof(IconTextButton)));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconTextButton), new FrameworkPropertyMetadata(string.Empty));

        public Geometry Icon
        {
            get => (Geometry)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Geometry), typeof(IconTextButton), new FrameworkPropertyMetadata(null));
    }
}