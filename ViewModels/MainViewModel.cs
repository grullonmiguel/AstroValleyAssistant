using AstroValleyAssistant.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AstroValleyAssistant.ViewModels
{
    public class MainViewModel : ViewModelBase
    {


        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }
        private ViewModelBase _currentViewModel;

        #region Methods

        private void ExecuteNavigate(object parameter)
        {
            //string viewName = parameter as string;
            //switch (viewName)
            //{
            //    case "Regrid":
            //        CurrentViewModel = _serviceProvider.GetRequiredService<RegridViewModel>();
            //        break;
            //    case "RealAuction":
            //        CurrentViewModel = _serviceProvider.GetRequiredService<RealAuctionViewModel>();
            //        break;
            //        // etc.
            //}
        }

        #endregion
    }
}
