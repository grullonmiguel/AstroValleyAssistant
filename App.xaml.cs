using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Core.Export;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models.Domain;
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
        // Static helper to avoid casting throughout the app
        public static new App Current => (App)Application.Current;

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
            services.AddSingleton<IBrowserService, BrowserService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IRegridService, RegridService>();

            services.AddSingleton<GeographyDataService>();
            services.AddSingleton<RealAuctionDataService>();
            services.AddSingleton<SettingsService>();

            // Register Exporters
            services.AddTransient<IExporter<IEnumerable<PropertyRecord>, string?>, ClipboardExporter>();
            //services.AddTransient<IExporter<IEnumerable<PropertyRecord>, string?>, ExcelPropertyExporter>();

            // Typed Client registration for the Scraper
            services.AddHttpClient<IRealTaxDeedClient, RealTaxDeedClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient<IRegridScraper, RegridScraper>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Point the interfaces to that same singleton instance
            services.AddSingleton<IRegridSettings>(x => x.GetRequiredService<SettingsService>());
            services.AddSingleton<IRealAuctionSettings>(x => x.GetRequiredService<SettingsService>());

            services.AddTransient<ImportViewModel>();
            services.AddTransient<RegridSettingsViewModel>();
            services.AddTransient<RealAuctionCalendarDataViewModel>();
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
