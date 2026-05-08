using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Services;

public interface ICountyMapDialogFactory
{
    CountyMapDialogViewModel Create(StateViewModel state);
}
