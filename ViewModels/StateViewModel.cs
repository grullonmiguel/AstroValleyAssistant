using AstroValleyAssistant.Core;
using AstroValleyAssistant.Models;
using System.Windows.Media;

namespace AstroValleyAssistant.ViewModels
{
    public class StateViewModel : ViewModelBase
    {
        public string? Name { get; set; }
        public string? Abbreviation { get; set; }
        public Geometry? PathData { get; set; }
        public int CountyCount { get; set; }
        public TaxSaleType TaxStatus { get; set; }

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