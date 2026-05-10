# Project Structure

## Solution Structure (Clean Architecture)

```
AstroValleyAssistant/                    # Solution root
├── src/                                 # All projects live here
│   ├── AstroValley.Domain/              # Layer 1: Core domain (no dependencies)
│   ├── AstroValley.Application/         # Layer 2: Use cases and interfaces
│   ├── AstroValley.Infrastructure/      # Layer 3: External concerns
│   └── AstroValley.Presentation/        # Layer 4: WPF UI (STARTUP PROJECT)
│
├── .kiro/                               # Kiro configuration
│   └── steering/                        # Steering files
│       ├── tech.md
│       ├── structure.md
│       └── product.md
│
├── AstroValleyAssistant.sln            # Solution file
└── README.md
```

---

## Layer 1: Domain (`src/AstroValley.Domain`)

**Purpose**: Pure domain logic with zero external dependencies.

```
src/AstroValley.Domain/
├── AstroValley.Domain.csproj
│
├── Entities/                            # Core domain entities
│   ├── PropertyRecord.cs                # Central domain record (C# record type, init-only)
│   ├── PropertyRecordMerger.cs          # Merges PropertyRecord data
│   └── RegridMatch.cs                   # Candidate parcel match
│
├── Enums/
│   └── Enums.cs                         # All enums: DialogOption, MenuOption, ScrapeStatus, TaxSaleType, etc.
│
├── Models/                              # Data transfer objects
│   ├── AppSettings.cs
│   ├── CountyInfo.cs
│   ├── MarkerLocation.cs                # Lat/lon pin for map export
│   ├── RealAuctionCountyInfo.cs
│   ├── RegridParcelResult.cs            # Regrid API response model
│   └── StateInfo.cs
│
└── Utilities/                           # Pure domain utilities
    ├── ClipboardFormatter.cs
    ├── StringExtensions.cs
    └── UrlBuilder.cs
```

---

## Layer 2: Application (`src/AstroValley.Application`)

**Purpose**: Business logic orchestration and interface contracts.

```
src/AstroValley.Application/
├── AstroValley.Application.csproj
│
├── Interfaces/                          # All service contracts
│   ├── Data/
│   │   ├── IGeographyDataService.cs     # County/state data provider
│   │   └── IRealAuctionDataService.cs   # RealAuction county data provider
│   │
│   ├── Export/
│   │   └── IExporter.cs                 # Generic export interface: IExporter<TData, TDestination>
│   │
│   ├── Scraping/
│   │   ├── IRegridHttpClient.cs         # HTTP client for Regrid API
│   │   ├── IRegridScraper.cs            # Regrid web scraper
│   │   └── IRealTaxDeedClient.cs        # RealAuction HTTP client
│   │
│   └── Settings/
│       ├── IRegridSettings.cs           # Regrid credentials and settings
│       └── IRealAuctionSettings.cs      # RealAuction settings
│
└── Services/
    └── RegridService.cs                 # Application service for Regrid operations
```

---

## Layer 3: Infrastructure (`src/AstroValley.Infrastructure`)

**Purpose**: External integrations - HTTP, file I/O, data access.

```
src/AstroValley.Infrastructure/
├── AstroValley.Infrastructure.csproj
│
├── Data/                                # Data services and JSON files
│   ├── Counties.json                    # County data (copied to output)
│   ├── Counties_RealAuction.json        # RealAuction county data (copied to output)
│   ├── GeographyDataService.cs          # Implements IGeographyDataService
│   └── RealAuctionDataService.cs        # Implements IRealAuctionDataService
│
├── Export/                              # Export implementations
│   ├── ExcelPropertyExporter.cs         # Exports to Excel via ClosedXML
│   ├── HtmlMarkerMapExporter.cs         # Exports to HTML map
│   └── MarkerMapParserService.cs        # Parses marker map HTML
│
└── Scraping/                            # HTTP clients and scrapers
    ├── JsonSafeExtensions.cs            # JSON parsing utilities
    ├── RealTaxDeedClient.cs             # Implements IRealTaxDeedClient
    ├── RegridHttpClient.cs              # Implements IRegridHttpClient
    └── RegridScraper.cs                 # Implements IRegridScraper
```

---

## Layer 4: Presentation (`src/AstroValley.Presentation`) - **STARTUP PROJECT**

**Purpose**: WPF UI, ViewModels, Views, and composition root.

```
src/AstroValley.Presentation/
├── AstroValley.Presentation.csproj
├── App.xaml / App.xaml.cs               # Application entry point; Generic Host setup and DI registration
├── AssemblyInfo.cs
├── appsettings.user.json                # User-specific settings (gitignored)
│
├── ViewModels/                          # One ViewModel per View; uses CommunityToolkit.Mvvm
│   ├── MainViewModel.cs                 # Shell VM; handles navigation and dialog orchestration
│   ├── RegridViewModel.cs               # Regrid page VM
│   ├── RealAuctionViewModel.cs          # RealAuction page VM
│   ├── MapViewModel.cs                  # Map page VM
│   ├── PropertyDataViewModel.cs         # Wraps a single PropertyRecord for display
│   ├── PropertyScraperViewModelBase.cs  # Shared base for scraper page VMs
│   ├── CountyViewModel.cs               # County selection VM
│   ├── StateViewModel.cs                # State selection VM
│   ├── SettingsViewModel.cs             # Settings VM
│   ├── RealAuctionCalendarDataViewModel.cs
│   │
│   └── Dialogs/                         # Dialog ViewModels (transient lifetime)
│       ├── CountyMapDialogViewModel.cs
│       ├── ImportViewModel.cs
│       ├── MarkerMapViewModel.cs
│       ├── RegridSettingsViewModel.cs
│       ├── ResolveMatchesViewModel.cs   # Multiple matches resolution dialog
│       └── ThemeSettingsViewModel.cs
│
├── Views/                               # XAML views; code-behind is minimal (bindings only)
│   ├── MainView.xaml                    # Shell window with navigation and dialog host
│   ├── RegridView.xaml
│   ├── RealAuctionView.xaml
│   ├── MapView.xaml
│   ├── PropertyDetailsControl.xaml      # Reusable property details control
│   ├── MultipleMatchesBanner.xaml       # Banner for multiple matches notification
│   │
│   └── Dialogs/
│       ├── CountyMapView.xaml
│       ├── ImportView.xaml
│       ├── MarkerMapView.xaml
│       ├── RegridSettingsView.xaml
│       ├── ResolveMatchesView.xaml      # Multiple matches resolution modal
│       └── ThemeSettingsView.xaml
│
├── Themes/                              # All visual styling; no C# logic except custom controls
│   ├── _Resources.xaml                  # Theme-agnostic resources: sizes, icons (StreamGeometry), converters
│   ├── _Controls.xaml                   # Merges all control style dictionaries
│   │
│   ├── Palettes/                        # Color palettes
│   │   ├── Light.xaml
│   │   └── Dark.xaml
│   │
│   ├── Accents/                         # Accent colors
│   │   ├── Purple.xaml
│   │   └── Teal.xaml
│   │
│   ├── Controls/                        # Per-control style overrides
│   │   ├── Button.xaml
│   │   ├── ComboBox.xaml
│   │   ├── DataGrid.xaml
│   │   ├── ListBox.xaml
│   │   ├── Menu.xaml
│   │   ├── ProgressBar.xaml
│   │   ├── ScrollBar.xaml
│   │   ├── TextBlock.xaml
│   │   └── TextBox.xaml
│   │
│   └── Assets/
│       └── Geography/                   # State outline path geometries (one XAML per state)
│
├── Services/                            # Presentation-specific services
│   ├── BrowserService.cs / IBrowserService.cs
│   ├── DialogService.cs / IDialogService.cs
│   ├── FileService.cs / IFileService.cs
│   ├── ThemeService.cs / IThemeService.cs
│   ├── SettingsService.cs               # Implements IRegridSettings + IRealAuctionSettings
│   ├── CountyMapDialogFactory.cs / ICountyMapDialogFactory.cs
│
├── Behaviors/                           # XAML attached behaviors (Interaction.Behaviors)
│   ├── ComboBoxItemTemplateSelector.cs
│   ├── ListBoxSmartScrollBehavior.cs
│   └── PasswordBoxAssistant.cs
│
├── Converters/                          # IValueConverter implementations for XAML bindings
│   ├── BooleanInvertConverter.cs
│   ├── BooleanToVisibilityConverter.cs
│   ├── CenterPointConverter.cs
│   ├── DragPromptConverter.cs
│   ├── EnumToDescriptionConverter.cs
│   ├── IsNotNullOrEmptyConverter.cs
│   ├── NullToBooleanConverter.cs
│   └── NullToVisibilityConverter.cs
│
├── Export/
│   └── ClipboardExporter.cs             # Clipboard export implementation
│
└── Assets/
    ├── markermap.html                   # HTML template for the map view (WebView2)
    └── markermap_interop.js             # JS bridge for WebView2 ↔ C# interop
```

---

## Conventions

### Adding New Services

1. **Define interface** in `src/AstroValley.Application/Interfaces/{Category}/I{ServiceName}.cs`
2. **Implement** in `src/AstroValley.Infrastructure/{Category}/{ServiceName}.cs`
3. **Register** in `src/AstroValley.Presentation/App.xaml.cs`:
   ```csharp
   services.AddSingleton<IMyService, MyService>();
   ```

### Adding New Pages

1. **Create ViewModel** in `src/AstroValley.Presentation/ViewModels/{PageName}ViewModel.cs`
   - Inherit from `ObservableObject` (CommunityToolkit.Mvvm)
   - Use `[RelayCommand]` attributes for commands
2. **Create View** in `src/AstroValley.Presentation/Views/{PageName}View.xaml`
3. **Register VM as singleton** in `App.xaml.cs`:
   ```csharp
   services.AddSingleton<MyPageViewModel>();
   ```
4. **Add navigation case** in `MainViewModel.Navigate()`:
   ```csharp
   "MyPage" => _serviceProvider.GetRequiredService<MyPageViewModel>()
   ```

### Adding New Dialogs

1. **Create ViewModel** in `src/AstroValley.Presentation/ViewModels/Dialogs/{DialogName}ViewModel.cs`
   - Inherit from `DialogViewModelBase`
   - Use `TaskCompletionSource<T>` pattern for returning results
   - Implement `OnDialogClosing()` lifecycle hook if needed
2. **Create View** in `src/AstroValley.Presentation/Views/Dialogs/{DialogName}View.xaml`
3. **Register as transient** in `App.xaml.cs`:
   ```csharp
   services.AddTransient<MyDialogViewModel>();
   ```
4. **Show via IDialogService**:
   ```csharp
   var dialogVm = new MyDialogViewModel(...);
   _dialogService.ShowDialog(dialogVm);
   var result = await dialogVm.Result;
   ```

### Commands

- Use `[RelayCommand]` attribute from CommunityToolkit.Mvvm
- For async commands: `[RelayCommand]` on `async Task MethodName()`
- For CanExecute: `[RelayCommand(CanExecute = nameof(CanMethodName))]`
- Generated command name: `MethodNameCommand` (strips "Async" suffix)
- Call `{CommandName}.NotifyCanExecuteChanged()` when CanExecute conditions change

### Property Change Notification

- Inherit from `ObservableObject` (CommunityToolkit.Mvvm)
- Use C# 14 field-backed properties with `SetProperty()`:
  ```csharp
  public string MyProperty
  {
      get;
      set => SetProperty(ref field, value);
  }
  ```

### Enums

- All application-wide enums go in `src/AstroValley.Domain/Enums/Enums.cs`
- Use `[Description]` attribute for human-readable labels (used by `EnumToDescriptionConverter`)

### Theme Names

- Follow `"{Palette}-{Accent}"` format (e.g., `"Light-Purple"`)
- Palette and accent XAML files must exist under `Themes/Palettes/` and `Themes/Accents/`

### File Paths

- Use `AppContext.BaseDirectory` for runtime file resolution
- Data files in Infrastructure are copied to output directory
- Assets in Presentation are copied to output directory
