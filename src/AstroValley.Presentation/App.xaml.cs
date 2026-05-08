using AstroValley.Application.Interfaces.Data;
using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Application.Services;
using AstroValley.Infrastructure.Data;
using AstroValley.Infrastructure.Export;
using AstroValley.Infrastructure.Scraping;
using AstroValley.Presentation.Export;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.ViewModels.Dialogs;
using AstroValley.Presentation.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace AstroValley.Presentation
{
    public partial class App : System.Windows.Application
    {
        public static new App Current => (App)System.Windows.Application.Current;
        private readonly IHost _host;
        public IServiceProvider Services => _host.Services;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, config) =>
                {
                    var userSettingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValley", "appsettings.user.json");

                    config.AddJsonFile(userSettingsPath, optional: true, reloadOnChange: false);
                })
                .ConfigureServices((ctx, services) => ConfigureServices(ctx, services))
                .Build();

        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            // ── Shell ────────────────────────────────────────────────────────
            services.AddSingleton<MainView>();
            services.AddSingleton<MainViewModel>();

            // ── Page ViewModels (Singleton) ──────────────────────────────────
            services.AddSingleton<RegridViewModel>();
            services.AddSingleton<RealAuctionViewModel>();
            services.AddSingleton<MapViewModel>();
            services.AddSingleton<MarkerMapViewModel>();

            // ── Dialog ViewModels (Transient) ────────────────────────────────
            services.AddTransient<ImportViewModel>();
            services.AddTransient<RegridSettingsViewModel>();
            services.AddTransient<RealAuctionCalendarDataViewModel>();
            services.AddTransient<ThemeSettingsViewModel>();

            // ── Presentation: WPF Services ───────────────────────────────────
            services.AddSingleton<IBrowserService, BrowserService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IThemeService, ThemeService>();

            // ── Presentation: Settings (stub — pending JSON migration) ───────
            services.AddSingleton<SettingsService>();
            services.AddSingleton<IRegridSettings>(x => x.GetRequiredService<SettingsService>());
            services.AddSingleton<IRealAuctionSettings>(x => x.GetRequiredService<SettingsService>());

            // ── Presentation: Factories ──────────────────────────────────────
            services.AddSingleton<ICountyMapDialogFactory, CountyMapDialogFactory>();

            // ── Application: Use-case Services ──────────────────────────────
            services.AddSingleton<IRegridService, RegridService>();

            // ── Infrastructure: Data Services ────────────────────────────────
            services.AddSingleton<IGeographyDataService, GeographyDataService>();
            services.AddSingleton<IRealAuctionDataService, RealAuctionDataService>();

            // ── Infrastructure: Exporters ────────────────────────────────────
            services.AddTransient<IClipboardExporter, ClipboardExporter>();
            services.AddTransient<IExcelExporter, ExcelPropertyExporter>();
            services.AddTransient<IHtmlMapExporter, HtmlMarkerMapExporter>();
            services.AddTransient<IMarkerMapParserService, MarkerMapParserService>();

            // ── Infrastructure: Typed HTTP Clients ───────────────────────────
            services.AddHttpClient<IRegridHttpClient, RegridHttpClient>();
            services.AddHttpClient<IRealTaxDeedClient, RealTaxDeedClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddHttpClient<IRegridScraper, RegridScraper>(client => client.Timeout = TimeSpan.FromSeconds(30));
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();
            _host.Services.GetRequiredService<IThemeService>().Initialize();
            _host.Services.GetRequiredService<MainView>().Show();
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
