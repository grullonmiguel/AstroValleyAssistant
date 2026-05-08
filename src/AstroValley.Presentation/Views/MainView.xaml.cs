using AstroValley.Presentation.ViewModels;
using System.Windows;

namespace AstroValley.Presentation.Views;

public partial class MainView : Window
{
    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}