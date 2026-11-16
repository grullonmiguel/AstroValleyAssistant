using AstroValley.Application.Interfaces.Data;
using AstroValley.Application.Interfaces.Export;
using AstroValley.Application.Interfaces.Import;
using AstroValley.Application.Interfaces.Scraping;
using AstroValley.Application.Interfaces.Settings;
using AstroValley.Application.Services;
using AstroValley.Infrastructure.Data;
using AstroValley.Infrastructure.Export;
using AstroValley.Infrastructure.Import;
using AstroValley.Presentation.Export;
using AstroValley.Presentation.Services;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.ViewModels.Dialogs;
using AstroValley.Presentation.ViewModels.Pages;
using AstroValley.Presentation.ViewModels.Shell;
using AstroValley.Presentation.ViewModels.Sources;
using AstroValley.Presentation.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace AstroValley.Presentation.Extensions;

/// <summary>All DI registrations for the app, in one place.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services) => services
        .AddPresentation()
        .AddApplicationAndInfrastructure()
        .AddScrapingHttpClients();

    private static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // Services (one instance for the whole app).
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IBrowserService, BrowserService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();

        // One settings store behind all three interfaces.
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IRegridSettings>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IRealAuctionSettings>(sp => sp.GetRequiredService<SettingsService>());

        // Shell. 
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellWindow>();

        // Pages are transient: NavigationService decides which instances to keep alive.
        // Register new pages here and map them to a view in Views/ViewTemplates.xaml.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ParcelLookupViewModel>();
        services.AddTransient<RealAuctionSourceViewModel>();

        //services.AddSingleton<MainViewModel>();
        //services.AddSingleton<IDialogService>(sp => new DialogService(sp.GetRequiredService<MainViewModel>()));
        //services.AddSingleton<MainView>();

        // Page view-models (singleton: pages keep their state).
        services.AddSingleton<CompsViewModel>();
        services.AddSingleton<RegridViewModel>();
        services.AddSingleton<MyPropertiesViewModel>();
        services.AddSingleton<RealAuctionViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Dialog view-models (transient: one per dialog).
        services.AddTransient<RegridSettingsViewModel>();
        services.AddTransient<ResolveMatchesDialogViewModel>();

        return services;
    }

    private static IServiceCollection AddApplicationAndInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<RegridService>();
        services.AddSingleton<IRegridService>(sp => sp.GetRequiredService<RegridService>());

        // Parcel Lookup providers: one instance per provider, shared with the old pages so the Regrid login is reused.
        services.AddSingleton<IParcelDataProvider>(sp => sp.GetRequiredService<RegridService>());

        // First importer that recognizes the headers wins; generic must stay last.
        services.AddSingleton<IPropertyImporter, ParcelFairImporter>();
        services.AddSingleton<IPropertyImporter, GenericImporter>();

        services.AddSingleton<IGeocoder, Geocoder>();
        // Per-user and writable without admin rights (unlike ProgramData), beside the app's logs.
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant");
        services.AddSingleton<IPinStore>(new JsonPinStore(Path.Combine(data, "SavedPins")));
        services.AddSingleton<IOwnedPropertyStore>(new JsonOwnedPropertyStore(Path.Combine(data, "MyProperties")));

        services.AddTransient<IClipboardExporter, ClipboardExporter>();
        services.AddTransient<IExcelExporter, ExcelPropertyExporter>();
        services.AddTransient<IPinSheetExporter, PinResearchSheetExporter>();
        services.AddTransient<IPortfolioExporter, PortfolioExcelExporter>();

        // Transient so each HTTP client gets its own instance.
        services.AddTransient<LoggingDelegatingHandler>();

        return services;
    }
}
