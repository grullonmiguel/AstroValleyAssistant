using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace AstroValley.Presentation.ViewModels;

public class CountyViewModel : ObservableObject
{
    public string? Name { get; set; }

    public Geometry? PathData { get; set; }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsHovered
    {
        get;
        set => SetProperty(ref field, value);
    }
}