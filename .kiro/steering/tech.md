# Tech Stack

## Platform & Runtime
- **Target framework**: `net10.0-windows`
- **Language**: C# 14 (`<LangVersion>14.0</LangVersion>`)
- **UI framework**: WPF (Windows Presentation Foundation)
- **Nullable reference types**: enabled
- **Implicit usings**: enabled

## Target Architecture: Clean Architecture
- **Goal**: Decompose the single project into a 4-layer solution.
- **Layers**:
  1. **Domain**: Core entities and business logic. Zero dependencies on external libraries or other projects.
  2. **Application**: Use cases, interfaces for infrastructure, and MediatR handlers.
  3. **Infrastructure**: Implementations of Application interfaces (Database, API Clients like `RegridScraper`).
  4. **Presentation (WPF)**: Views, ViewModels, and `App.xaml.cs` (Composition Root).
- **Dependency Flow**: Presentation -> Application -> Domain; Infrastructure -> Application.

## Key Libraries
| Package | Purpose |
|---|---|
| `Microsoft.Extensions.Hosting` | Generic Host for app lifecycle and DI |
| `Microsoft.Extensions.DependencyInjection` | Dependency injection container |
| `Microsoft.Extensions.Http.Resilience` | Resilient `HttpClient` pipelines |
| `Microsoft.Web.WebView2` | Embedded Chromium browser (map view) |
| `Microsoft.Xaml.Behaviors.Wpf` | XAML behaviors (e.g., scroll, template selectors) |
| `ClosedXML` | Excel export |
| `HtmlAgilityPack` | HTML scraping/parsing |

## Architecture Patterns
- **MVVM** – strict separation of Views, ViewModels, and Models. No business logic in code-behind.
- **Generic Host** – `App.xaml.cs` bootstraps `IHost` for DI, configuration, and lifecycle management.
- **Interface-first services** – every service has a matching `I{Name}` interface registered in DI.
- **Typed `HttpClient`** – HTTP clients (`RealTaxDeedClient`, `RegridScraper`) are registered as typed clients via `AddHttpClient<TInterface, TImpl>()`.
- **`IExporter<TData, TDestination>`** – generic export abstraction; implementations include clipboard and HTML map exporters.

## DI Lifetime Conventions
- **Singleton**: page-level ViewModels (state must persist across navigation), all services, `MainView`, `MainViewModel`.
- **Transient**: dialog ViewModels, exporters.
- `SettingsService` is registered as a singleton and aliased to both `IRegridSettings` and `IRealAuctionSettings`.

## Settings
User settings are stored via `Properties.Settings.Default` (`.settings` file). `SettingsService` wraps all reads/writes and exposes `Save()`.

## Build & Run

```bash
# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build

# Build (Release)
dotnet build -c Release

# Run
dotnet run
```

Build output goes to `bin/Debug/net10.0-windows/` or `bin/Release/net10.0-windows/`.

There is no automated test project in this repository.
