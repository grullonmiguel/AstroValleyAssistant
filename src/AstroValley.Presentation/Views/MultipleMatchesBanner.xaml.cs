using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroValley.Presentation.Views;

public partial class MultipleMatchesBanner : UserControl
{
    public MultipleMatchesBanner()
    {
        InitializeComponent();
    }

    // ── MatchCount ───────────────────────────────────────────────────────────
    public static readonly DependencyProperty MatchCountProperty =
        DependencyProperty.Register(nameof(MatchCount), typeof(int), typeof(MultipleMatchesBanner), new PropertyMetadata(0));

    public int MatchCount
    {
        get => (int)GetValue(MatchCountProperty);
        set => SetValue(MatchCountProperty, value);
    }

    // ── ParcelId ─────────────────────────────────────────────────────────────
    public static readonly DependencyProperty ParcelIdProperty =
        DependencyProperty.Register(nameof(ParcelId), typeof(string), typeof(MultipleMatchesBanner), new PropertyMetadata(string.Empty));

    public string ParcelId
    {
        get => (string)GetValue(ParcelIdProperty);
        set => SetValue(ParcelIdProperty, value);
    }

    // ── ViewMatchesCommand ───────────────────────────────────────────────────
    public static readonly DependencyProperty ViewMatchesCommandProperty =
        DependencyProperty.Register(nameof(ViewMatchesCommand), typeof(ICommand), typeof(MultipleMatchesBanner), new PropertyMetadata(null));

    public ICommand ViewMatchesCommand
    {
        get => (ICommand)GetValue(ViewMatchesCommandProperty);
        set => SetValue(ViewMatchesCommandProperty, value);
    }

    // ── ViewMatchesCommandParameter ──────────────────────────────────────────
    public static readonly DependencyProperty ViewMatchesCommandParameterProperty =
        DependencyProperty.Register(nameof(ViewMatchesCommandParameter), typeof(object), typeof(MultipleMatchesBanner), new PropertyMetadata(null));

    public object ViewMatchesCommandParameter
    {
        get => GetValue(ViewMatchesCommandParameterProperty);
        set => SetValue(ViewMatchesCommandParameterProperty, value);
    }
}
