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
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.IO;
using System.Net;
using System.Net.Http;
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
            services.AddSingleton<MainViewModel>();

            // DialogService (singleton) - registered AFTER MainViewModel
            services.AddSingleton<IDialogService>(sp =>
            {
                var mainViewModel = sp.GetRequiredService<MainViewModel>();
                return new DialogService(mainViewModel);
            });

            services.AddSingleton<MainView>();

            // ── Page ViewModels (Singleton) ──────────────────────────────────
            services.AddSingleton<RegridViewModel>();
            services.AddSingleton<RealAuctionViewModel>();
            services.AddSingleton<MapViewModel>();
            services.AddSingleton<MarkerMapViewModel>();

            // ── Dialog ViewModels (Transient) ────────────────────────────────
            services.AddTransient<ImportViewModel>();
            services.AddTransient<RegridSettingsViewModel>();
            services.AddTransient<ThemeSettingsViewModel>();
            services.AddTransient<ResolveMatchesViewModel>();
            services.AddTransient<WebNavigationDialogViewModel>();

            // ── Presentation: WPF Services ───────────────────────────────────
            services.AddSingleton<IBrowserService, BrowserService>();
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

            // ── Infrastructure: Exporters ────────────────────────────────────
            services.AddTransient<IClipboardExporter, ClipboardExporter>();
            services.AddTransient<IExcelExporter, ExcelPropertyExporter>();
            services.AddTransient<IHtmlMapExporter, HtmlMarkerMapExporter>();
            services.AddTransient<IMarkerMapParserService, MarkerMapParserService>();

            // ── Infrastructure: HTTP Logging Handler ─────────────────────────
            // ── Infrastructure: HTTP Logging Handler ─────────────────────────
            // Register the logging handler as transient so each HTTP client gets its own instance
            services.AddTransient<LoggingDelegatingHandler>();

            // ── Infrastructure: Typed HTTP Clients with Resilience ───────────
            
            // ═══════════════════════════════════════════════════════════════════
            // REGRID HTTP CLIENT - Image Download Client
            // ═══════════════════════════════════════════════════════════════════
            // Purpose: Downloads parcel images from Regrid's CDN
            // Resilience: Basic retry for transient network failures
            // Rate Limiting: Built into the client implementation (500-1500ms random delay)
            services.AddHttpClient<IRegridHttpClient, RegridHttpClient>()
                .ConfigureHttpClient(client =>
                {
                    // Configure browser-like headers to avoid bot detection
                    // These headers make the request appear as if it's coming from Chrome
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/144.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept",
                        "image/avif,image/webp,image/apng,*/*;q=0.8");
                    
                    // Session cookies for authenticated image access
                    // Note: These should ideally come from configuration/settings
                    client.DefaultRequestHeaders.Add("Cookie",
                        "_session_id=7a8e35ecd5d318d2bd2331704a4e60ce; user.id=BAgw--79292e56d5866cc41199e6f92f32032686d8e164;");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    // Best Practice: Set connection lifetime to handle DNS changes
                    // Connections are recycled every 15 minutes to ensure DNS updates are respected
                    // This prevents stale connections in cloud/containerized environments
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    
                    // Enable HTTP/2 for better performance with multiple concurrent requests
                    EnableMultipleHttp2Connections = true
                })
                // Add logging handler BEFORE resilience handler so logging captures all attempts
                .AddHttpMessageHandler<LoggingDelegatingHandler>()
                .AddStandardResilienceHandler(options =>
                {
                    // Configure retry policy for transient failures
                    // Images are non-critical, so we use a simple retry strategy
                    options.Retry = new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 2,
                        Delay = TimeSpan.FromMilliseconds(500),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true // Adds randomness to prevent thundering herd
                    };
                    
                    // Disable circuit breaker for image downloads (not critical path)
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromDays(1);
                    
                    // Set reasonable timeout for image downloads
                    options.AttemptTimeout = new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(15)
                    };
                });

            // ═══════════════════════════════════════════════════════════════════
            // REGRID SCRAPER - Web Scraping Client
            // ═══════════════════════════════════════════════════════════════════
            // Purpose: Scrapes parcel data from Regrid's web interface
            // Resilience: Retry with exponential backoff, circuit breaker for cascading failures
            // Special Handling: Custom 429 (rate limit) detection in client code
            services.AddHttpClient<IRegridScraper, RegridScraper>()
                .ConfigureHttpClient(client =>
                {
                    // Overall timeout for the entire request (including retries)
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Browser-like headers to avoid bot detection
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept",
                        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                    client.DefaultRequestHeaders.Add("Referer", "https://regrid.com/");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    // Best Practice: Recycle connections every 15 minutes for DNS refresh
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    EnableMultipleHttp2Connections = true,
                    
                    // Allow cookies for session management during scraping
                    UseCookies = true,
                    CookieContainer = new CookieContainer()
                })
                // Add logging handler BEFORE resilience handler so logging captures all attempts
                .AddHttpMessageHandler<LoggingDelegatingHandler>()
                .AddStandardResilienceHandler(options =>
                {
                    // Configure retry policy with exponential backoff
                    options.Retry = new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true, // Prevents retry storms
                        Delay = TimeSpan.FromSeconds(1),
                        
                        // Custom retry logic: Don't retry on 429 (rate limit)
                        // The client has custom logic to handle rate limiting
                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                            .Handle<HttpRequestException>()
                            .Handle<TaskCanceledException>()
                            .HandleResult(response =>
                                response.StatusCode != HttpStatusCode.TooManyRequests &&
                                (int)response.StatusCode >= 500)
                    };
                    
                    // Circuit breaker prevents cascading failures
                    // If 50% of requests fail within 30 seconds, open the circuit for 15 seconds
                    options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
                    {
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        FailureRatio = 0.5,
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(15)
                    };
                    
                    // Per-attempt timeout (each retry gets 10 seconds)
                    options.AttemptTimeout = new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(10)
                    };
                    
                    // Total request timeout (all retries combined must complete within 30 seconds)
                    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                });

            // ═══════════════════════════════════════════════════════════════════
            // REAL TAX DEED CLIENT - Auction Data Scraper
            // ═══════════════════════════════════════════════════════════════════
            // Purpose: Scrapes tax deed auction listings from county RealAuction sites
            // Resilience: Aggressive retry with circuit breaker for unreliable county servers
            services.AddHttpClient<IRealTaxDeedClient, RealTaxDeedClient>()
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Essential headers to bypass bot detection on county websites
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept",
                        "application/json, text/javascript, */*; q=0.01");
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    // Best Practice: Connection lifetime management for DNS changes
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    EnableMultipleHttp2Connections = true,
                    
                    // Cookie support for session-based county websites
                    UseCookies = true,
                    CookieContainer = new CookieContainer()
                })
                // Add logging handler BEFORE resilience handler so logging captures all attempts
                .AddHttpMessageHandler<LoggingDelegatingHandler>()
                .AddStandardResilienceHandler(options =>
                {
                    // Retry policy: County servers can be unreliable, so retry aggressively
                    options.Retry = new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(1),
                        
                        // Retry on server errors (5xx) and network failures
                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                            .Handle<HttpRequestException>()
                            .Handle<TaskCanceledException>()
                            .HandleResult(response => (int)response.StatusCode >= 500)
                    };
                    
                    // Circuit breaker for county server outages
                    options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
                    {
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        FailureRatio = 0.5,
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(15)
                    };
                    
                    // Timeouts: County servers can be slow
                    options.AttemptTimeout = new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(10)
                    };
                    
                    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                });
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();
            _host.Services.GetRequiredService<IThemeService>().Initialize();


            // Initialize MainViewModel's current view AFTER all services are ready
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainViewModel.CurrentViewModel = _host.Services.GetRequiredService<MapViewModel>();

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
