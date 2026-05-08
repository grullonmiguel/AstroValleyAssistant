using AstroValley.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace AstroValley.Presentation.ViewModels;

public class StateViewModel : ObservableObject
{
    public string? Name { get; set; }
    public string? Abbreviation { get; set; }
    public Geometry? PathData { get; set; }
    public int CountyCount { get; set; }
    public TaxSaleType TaxStatus { get; set; }

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