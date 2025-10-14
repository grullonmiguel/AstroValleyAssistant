using AstroValleyAssistant.Core;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class CountyViewModel : ViewModelBase
    {
        public string Name { get; set; }

        public Geometry PathData { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
        private bool _isSelected;

        public bool IsHovered
        {
            get => _isHovered;
            set => Set(ref _isHovered, value);
        }
        private bool _isHovered;
    }
}
