using AstroValley.Application.Interfaces.Data;
using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.ViewModels.Dialogs;

namespace AstroValley.Presentation.Services;

public class CountyMapDialogFactory : ICountyMapDialogFactory
{
    private readonly IGeographyDataService _geoService;

    public CountyMapDialogFactory(IGeographyDataService geoService)
    {
        _geoService = geoService;
    }

    public CountyMapDialogViewModel Create(StateViewModel state)
        => new CountyMapDialogViewModel(state, _geoService);
}
