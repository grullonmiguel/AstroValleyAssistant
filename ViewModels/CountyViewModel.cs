using AstroValleyAssistant.Core;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class CountyViewModel : ViewModelBase
    {
        public string? Name { get; set; }

        public Geometry? PathData { get; set; }

        public bool IsSelected
        {
            get => field;
            set => Set(ref field, value);
        }

        public bool IsHovered
        {
            get => field;
            set => Set(ref field, value);
        }
    }
}