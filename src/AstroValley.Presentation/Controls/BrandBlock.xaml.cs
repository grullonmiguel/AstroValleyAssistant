using System.Windows;
using System.Windows.Controls;

namespace AstroValley.Presentation.Controls;

public partial class BrandBlock : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(BrandBlock), new PropertyMetadata(string.Empty));

    public BrandBlock()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
