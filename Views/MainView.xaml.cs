using AstroValleyAssistant.ViewModels;
using System.Windows;

namespace AstroValleyAssistant.Views
{
    public partial class MainView : Window
    {
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}