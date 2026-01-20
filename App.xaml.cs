using AstroValleyAssistant.Core.Abstract;
using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.ViewModels;
using AstroValleyAssistant.ViewModels.Dialogs;
using AstroValleyAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace AstroValleyAssistant
{
    public partial class App : Application
    {
        private readonly IHost _host;
        public IServiceProvider Services => _host.Services;

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
            services.AddSingleton<SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<GeographyDataService>();

            // Typed Client registration for the Scraper
            services.AddHttpClient<IRealTaxDeedClient, RealTaxDeedClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Point the interfaces to that same singleton instance
            services.AddSingleton<IRegridSettings>(x => x.GetRequiredService<SettingsService>());
            services.AddSingleton<IRealAuctionSettings>(x => x.GetRequiredService<SettingsService>());
            services.AddTransient<RegridSettingsViewModel>();
            services.AddTransient<RealAuctionSettingsViewModel>();
        }
        
        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. -- Start the host
            await _host.StartAsync();

            // 2. -- Initialize the theme service
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.Initialize();

            // 3. -- Resolve the main window and show it
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
