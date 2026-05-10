# Tech Stack

## Platform & Runtime
- **Target framework**: `net10.0-windows`
- **Language**: C# 14 (`<LangVersion>14.0</LangVersion>`)
- **UI framework**: WPF (Windows Presentation Foundation)
- **Nullable reference types**: enabled
- **Implicit usings**: enabled

## Architecture: Clean Architecture (4-Layer Solution)

The solution follows Clean Architecture principles with clear separation of concerns:

### Layer 1: Domain (`src/AstroValley.Domain`)
- **Purpose**: Core business entities, enums, and domain models
- **Dependencies**: NONE (pure C# - no external packages or project references)
- **Contains**:
  - `Entities/`: Core domain entities (`PropertyRecord`, `RegridMatch`, `PropertyRecordMerger`)
  - `Enums/`: All application enums (`ScrapeStatus`, `TaxSaleType`, `DialogOption`, etc.)
  - `Models/`: Data transfer objects (`StateInfo`, `CountyInfo`, `RegridParcelResult`, `MarkerLocation`)
  - `Utilities/`: Pure domain utilities (`ClipboardFormatter`, `StringExtensions`, `UrlBuilder`)

### Layer 2: Application (`src/AstroValley.Application`)
- **Purpose**: Use cases, business logic orchestration, and interface definitions
- **Dependencies**: Domain only
- **Contains**:
  - `Interfaces/Data/`: Data service contracts (`IGeographyDataService`, `IRealAuctionDataService`)
  - `Interfaces/Export/`: Export service contracts (`IExporter<TData, TDestination>`)
  - `Interfaces/Scraping/`: Scraping service contracts (`IRegridScraper`, `IRealTaxDeedClient`, `IRegridHttpClient`)
  - `Interfaces/Settings/`: Settings contracts (`IRegridSettings`, `IRealAuctionSettings`)
  - `Services/`: Application services (`RegridService`)

### Layer 3: Infrastructure (`src/AstroValley.Infrastructure`)
- **Purpose**: External concerns - data access, HTTP clients, file I/O, third-party integrations
- **Dependencies**: Application, Domain
- **Contains**:
  - `Data/`: JSON data files and data services (`GeographyDataService`, `RealAuctionDataService`)
  - `Scraping/`: HTTP clients and web scrapers (`RegridScraper`, `RealTaxDeedClient`, `RegridHttpClient`)
  - `Export/`: Export implementations (`ExcelPropertyExporter`, `HtmlMarkerMapExporter`, `MarkerMapParserService`)

### Layer 4: Presentation (`src/AstroValley.Presentation`) - **STARTUP PROJECT**
- **Purpose**: WPF UI, ViewModels, Views, and composition root
- **Dependencies**: Application, Infrastructure, Domain
- **Contains**:
  - `App.xaml.cs`: Application entry point, Generic Host setup, DI registration
  - `ViewModels/`: MVVM ViewModels (page-level and dialog ViewModels)
  - `Views/`: XAML views and user controls
  - `Themes/`: All visual styling (palettes, accents, control styles)
  - `Services/`: Presentation-specific services (`DialogService`, `BrowserService`, `ThemeService`, `SettingsService`)
  - `Behaviors/`: XAML attached behaviors
  - `Converters/`: IValueConverter implementations
  - `Assets/`: Static assets (HTML templates, JavaScript files)

### Dependency Flow
```
Presentation ──→ Application ──→ Domain
     ↓
Infrastructure ──→ Application
```

## Key Libraries

| Package | Layer | Purpose |
|---|---|---|
| `Microsoft.Extensions.Hosting` | Presentation | Generic Host for app lifecycle and DI |
| `Microsoft.Extensions.DependencyInjection` | Presentation | Dependency injection container |
| `Microsoft.Extensions.Http.Resilience` | Infrastructure | Resilient `HttpClient` pipelines |
| `Microsoft.Web.WebView2` | Presentation | Embedded Chromium browser (map view) |
| `Microsoft.Xaml.Behaviors.Wpf` | Presentation | XAML behaviors |
| `CommunityToolkit.Mvvm` | Presentation | MVVM helpers (`[RelayCommand]`, `ObservableObject`) |
| `ClosedXML` | Infrastructure | Excel export |
| `HtmlAgilityPack` | Infrastructure | HTML scraping/parsing |

## Architecture Patterns

- **MVVM** – Strict separation of Views, ViewModels, and Models. No business logic in code-behind.
- **Generic Host** – `App.xaml.cs` bootstraps `IHost` for DI, configuration, and lifecycle management.
- **Interface-first services** – Every service has a matching `I{Name}` interface registered in DI.
- **Typed `HttpClient`** – HTTP clients are registered as typed clients via `AddHttpClient<TInterface, TImpl>()`.
- **CommunityToolkit.Mvvm** – Uses `[RelayCommand]` attributes for command generation, `ObservableObject` for INPC.
- **Generic export abstraction** – `IExporter<TData, TDestination>` for pluggable export implementations.
- **TaskCompletionSource pattern** – Dialog ViewModels return results via `Task<T>` for clean async/await.

## DI Lifetime Conventions

- **Singleton**: Page-level ViewModels (state persists across navigation), all services, `MainView`, `MainViewModel`
- **Transient**: Dialog ViewModels, exporters
- `SettingsService` is registered as singleton and aliased to both `IRegridSettings` and `IRealAuctionSettings`

## Settings

User settings are stored via `Properties.Settings.Default` (`.settings` file). `SettingsService` wraps all reads/writes and exposes `Save()`.

## Build & Run

```bash
# Restore dependencies (from solution root)
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build src/AstroValley.Presentation/AstroValley.Presentation.csproj

# Build Release
dotnet build -c Release

# Run (from solution root)
dotnet run --project src/AstroValley.Presentation/AstroValley.Presentation.csproj
```

Build output goes to `src/{ProjectName}/bin/Debug/net10.0-windows/` or `src/{ProjectName}/bin/Release/net10.0-windows/`.

**Startup Project**: `src/AstroValley.Presentation`

There is no automated test project in this repository.
