# Project Structure

```
AstroValleyAssistant/
├── App.xaml / App.xaml.cs        # Application entry point; Generic Host setup and DI registration
├── AssemblyInfo.cs
├── AstroValleyAssistant.csproj
│
├── Models/                        # Plain data types; no logic
│   ├── Domain/
│   │   ├── PropertyRecord.cs      # Central domain record (C# record type, init-only)
│   │   ├── PropertyRecordMerger.cs
│   │   └── RegridMatch.cs
│   ├── Enums.cs                   # All enums: DialogOption, MenuOption, ScrapeStatus, TaxSaleType, etc.
│   ├── MarkerLocation.cs
│   ├── RegridParcelResult.cs
│   └── StateInfo.cs
│
├── Core/                          # Reusable infrastructure; no UI dependencies
│   ├── ViewModelBase.cs           # INotifyPropertyChanged + IDisposable base; use Set<T>() for properties
│   ├── ViewModelDialogBase.cs     # Base for dialog ViewModels; adds Title property
│   ├── Commands/
│   │   └── RelayCommand.cs        # RelayCommand, RelayCommand<T>, AsyncRelayCommand, AsyncRelayCommand<T>
│   ├── Behaviors/                 # XAML attached behaviors (Interaction.Behaviors)
│   ├── Converters/                # IValueConverter implementations for XAML bindings
│   ├── Data/
│   │   ├── GeographyDataService.cs
│   │   ├── RealAuctionDataService.cs
│   │   └── *.json                 # Embedded county/state data files (copied to output)
│   ├── Export/
│   │   ├── IExporter.cs           # Generic export interface
│   │   ├── ClipboardExporter.cs
│   │   ├── ExcelPropertyExporter.cs
│   │   └── HtmlMarkerMapExporter.cs
│   ├── Extensions/                # Static extension methods
│   ├── Networking/
│   │   ├── IRegridHttpClient.cs
│   │   └── RegridHttpClient.cs
│   ├── Services/                  # All services live here with matching I{Name} interfaces
│   │   ├── BrowserService.cs / IBrowserService.cs
│   │   ├── DialogService.cs / IDialogService.cs
│   │   ├── FileService.cs / IFileService.cs
│   │   ├── MarkerMapParserService.cs / IMarkerMapParserService.cs
│   │   ├── RealTaxDeedClient.cs / IRealTaxDeedClient.cs
│   │   ├── RegridScraper.cs / IRegridScraper.cs
│   │   ├── RegridService.cs / IRegridService.cs
│   │   ├── SettingsService.cs     # Implements IRegridSettings + IRealAuctionSettings
│   │   ├── ThemeService.cs / IThemeService.cs
│   │   └── UrlBuilder.cs
│   └── Utilities/
│       └── ClipboardFormatter.cs
│
├── ViewModels/                    # One ViewModel per View; inherit ViewModelBase
│   ├── MainViewModel.cs           # Shell VM; handles navigation and dialog orchestration
│   ├── RegridViewModel.cs
│   ├── RealAuctionViewModel.cs
│   ├── MapViewModel.cs
│   ├── PropertyDataViewModel.cs   # Wraps a single PropertyRecord for display
│   ├── PropertyScraperViewModelBase.cs  # Shared base for scraper page VMs
│   ├── CountyViewModel.cs / StateViewModel.cs
│   ├── SettingsViewModel.cs
│   └── Dialogs/                   # ViewModels for modal/drawer dialogs
│       ├── ImportViewModel.cs
│       ├── MarkerMapViewModel.cs
│       ├── RegridSettingsViewModel.cs
│       ├── ThemeSettingsViewModel.cs
│       └── CountyMapDialogViewModel.cs
│
├── Views/                         # XAML views; code-behind is minimal (bindings only)
│   ├── MainView.xaml              # Shell window with navigation and dialog host
│   ├── RegridView.xaml
│   ├── RealAuctionView.xaml
│   ├── MapView.xaml
│   ├── PropertyDetailsControl.xaml
│   ├── MultipleMatchesControl.xaml
│   └── Dialogs/
│       ├── ImportView.xaml
│       ├── MarkerMapView.xaml
│       ├── RegridSettingsView.xaml
│       ├── ThemeSettingsView.xaml
│       └── CountyMapView.xaml
│
├── Themes/                        # All visual styling; no C# logic except custom controls
│   ├── _Resources.xaml            # Theme-agnostic resources: sizes, icons (StreamGeometry), converters
│   ├── _Controls.xaml             # Merges all control style dictionaries
│   ├── Palettes/
│   │   ├── Light.xaml             # Light color palette
│   │   └── Dark.xaml              # Dark color palette
│   ├── Accents/
│   │   ├── Purple.xaml            # Purple accent
│   │   └── Teal.xaml              # Teal accent
│   ├── Controls/                  # Per-control style overrides
│   └── Assets/Geography/          # State outline path geometries (one XAML per state)
│
└── Core/Assets/
    ├── markermap.html             # HTML template for the map view (WebView2)
    └── markermap_interop.js       # JS bridge for WebView2 ↔ C# interop
```

## Conventions

- **New service**: create `IMyService.cs` and `MyService.cs` in `Core/Services/`, register both in `App.xaml.cs`.
- **New page**: add a ViewModel in `ViewModels/` (extend `ViewModelBase`), a View in `Views/`, and register the VM as a singleton in `App.xaml.cs`. Add a navigation case in `MainViewModel.Navigate()`.
- **New dialog**: add a ViewModel in `ViewModels/Dialogs/` (extend `ViewModelDialogBase`), a View in `Views/Dialogs/`, register as transient. Show via `IDialogService.ShowDialog(vm)`.
- **Commands**: use `RelayCommand` / `RelayCommand<T>` for sync, `AsyncRelayCommand` / `AsyncRelayCommand<T>` for async. Declare as `ICommand` properties using C# 14 field-backed properties (`=> field ??= new ...`).
- **Property change notification**: always use `Set(ref field, value)` from `ViewModelBase` — never set backing fields directly and call `OnPropertyChanged` manually.
- **Enums**: all application-wide enums go in `Models/Enums.cs`. Use `[Description]` attribute when a human-readable label is needed (e.g., for `EnumToDescriptionConverter`).
- **Theme names**: follow the `"{Palette}-{Accent}"` format (e.g., `"Light-Purple"`). Palette and accent XAML files must exist under `Themes/Palettes/` and `Themes/Accents/`.
