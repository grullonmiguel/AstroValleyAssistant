using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace AstroValley.Presentation.Models;

/// <summary>A selectable color swatch on the Settings page.</summary>
public sealed partial class ColorOption : ObservableObject
{
    public ColorOption(string name, string hex)
    {
        Name = name;
        Color = (Color)ColorConverter.ConvertFromString(hex);
        Brush = new SolidColorBrush(Color);
        Brush.Freeze();
    }

    public string Name { get; }

    public Color Color { get; }

    public Brush Brush { get; }

    [ObservableProperty]
    private bool isSelected;
}
