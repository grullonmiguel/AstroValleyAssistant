using AstroValleyAssistant.Core.Data;
using AstroValleyAssistant.Core.Export;
using AstroValleyAssistant.Core.Services;
using AstroValleyAssistant.Models;
using AstroValleyAssistant.Models.Domain;
using AstroValleyAssistant.ViewModels;
using AstroValleyAssistant.ViewModels.Dialogs;
using AstroValleyAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace AstroValleyAssistant
{
    /// <summary>
    /// Manages the application lifecycle and service registrations using the Generic Host.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Gets the current application instance with proper type casting.
        /// </summary>
        public static new App Current => (App)Application.Current;

        private readonly IHost _host;
        
        /// <summary>
        /// Gets the service provider from the Generic Host for dependency injection.
        /// </summary>
        public IServiceProvider Services => _host.Services;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Creates and configures the Generic Host with all application services.
        /// </summary>
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

        /// <summary>
        /// Configures dependency injection services for the application.
        /// Registers views, view models, services, exporters, and HTTP clients.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        private void ConfigureServices(IServiceCollection services)
        {
            // Register Shell 
            services.AddSingleton<MainView>();
            services.AddSingleton<MainViewModel>();

            // Register Page ViewModels as
            // Singletons to preserve their state
            services.AddSingleton<RegridViewModel>();
            services.AddSingleton<RealAuctionViewModel>();
            services.AddSingleton<MapViewModel>();
            services.AddSingleton<MarkerMapViewModel>();

            // Register ViewModels
            services.AddTransient<ImportViewModel>();
            services.AddTransient<RegridSettingsViewModel>();
            services.AddTransient<RealAuctionCalendarDataViewModel>();
            services.AddTransient<ThemeSettingsViewModel>();

            // Register Services
            services.AddSingleton<IBrowserService, BrowserService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IRegridService, RegridService>();
            services.AddSingleton<IThemeService, ThemeService>();

            services.AddSingleton<GeographyDataService>();
            services.AddSingleton<RealAuctionDataService>();
            services.AddSingleton<SettingsService>();

            services.AddTransient<IMarkerMapParserService, MarkerMapParserService>();

            // Register Exporters
            services.AddTransient<IExporter<IEnumerable<PropertyRecord>, string?>, ClipboardExporter>();
            services.AddTransient<IExporter<IEnumerable<MarkerLocation>, string?>, HtmlMarkerMapExporter>();

            // Typed Client registration for the Scraper
            services.AddHttpClient<IRealTaxDeedClient, RealTaxDeedClient>(client => { client.Timeout = TimeSpan.FromSeconds(30); });
            services.AddHttpClient<IRegridScraper, RegridScraper>(client => { client.Timeout = TimeSpan.FromSeconds(30); });

            // Point the interfaces to that same singleton instance
            services.AddSingleton<IRegridSettings>(x => x.GetRequiredService<SettingsService>());
            services.AddSingleton<IRealAuctionSettings>(x => x.GetRequiredService<SettingsService>());
        }
        
        /// <summary>
        /// Handles application startup. Starts the Generic Host, initializes the theme service,
        /// and displays the main window.
        /// </summary>
        /// <param name="e">Startup event arguments.</param>
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

        /// <summary>
        /// Handles application exit. Stops and disposes the Generic Host to ensure proper cleanup.
        /// </summary>
        /// <param name="e">Exit event arguments.</param>
        protected override async void OnExit(ExitEventArgs e)
        {
            // 5. Stop and dispose of the host on exit
            await _host.StopAsync();
            _host.Dispose();

            base.OnExit(e);
        }
    }

}
