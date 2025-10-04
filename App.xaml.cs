using AstroValleyAssistant.ViewModels;
using AstroValleyAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace AstroValleyAssistant
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            // 1. Create a host builder
            _host = Host.CreateDefaultBuilder() // Provides default logging, config, etc.
                .ConfigureServices((context, services) =>
                {
                    // 2. Call our registration method
                    ConfigureServices(services);
                })
                .Build();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Shell 
            services.AddSingleton<MainView>();
            services.AddSingleton<MainViewModel>();

            // Register Page ViewModels as Singletons to preserve their state
            services.AddSingleton<RegridViewModel>();
            services.AddSingleton<RealAuctionViewModel>();
            services.AddSingleton<MapViewModel>();

            // Register ViewModels

            // Register other services
            // services.AddTransient<IDataService, ApiDataService>();
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            // 3. Start the host
            await _host.StartAsync();

            // 4. Resolve the main window and show it
            // The DI container will create the MainViewModel and inject it into the MainView
            var mainWindow = _host.Services.GetRequiredService<MainView>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // 5. Stop and dispose of the host on exit
            await _host.StopAsync();
            _host.Dispose();

            base.OnExit(e);
        }
    }

}
