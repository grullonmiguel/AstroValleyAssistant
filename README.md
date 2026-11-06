# Astro Valley Assistant

A Windows desktop app (WPF / C#) that helps researchers analyze tax-defaulted properties in the U.S.

## Features

- **Regrid lookup** – Look up parcel data by parcel ID or address. Import lists from the clipboard or a file, scrape in batches, track status per record, and resolve multiple-match results.
- **Real Auction** – Browse county RealAuction tax deed sites in an embedded WebView2 window and pull listings (opening bids, assessed values, auction dates).
- **Maps** – View property locations as pins on an interactive map, by county or from a marker list, and export them to a standalone HTML map.
- **Import / export** – Copy results to the clipboard (tab-delimited) or export to Excel.
- **Themes** – Configurable light/dark theme and accent settings.

## Tech stack

- .NET 10, C# 14, WPF
- MVVM with CommunityToolkit.Mvvm
- Generic Host, dependency injection, and `HttpClient` with resilience handlers (`Microsoft.Extensions.*`)
- WebView2 for the map and RealAuction navigation
- ClosedXML for Excel export

## Project layout

The solution follows Clean Architecture (`src/`):

| Project | Role |
|---|---|
| `AstroValley.Domain` | Entities, enums, models, pure utilities |
| `AstroValley.Application` | Service interfaces and application services |
| `AstroValley.Infrastructure` | HTTP clients, scrapers, data and export implementations |
| `AstroValley.Presentation` | WPF UI, view models, composition root (startup project) |

See [SOLUTION.md](SOLUTION.md) and `CLAUDE.md` for more detail.

## Getting started

Requirements: Windows, the .NET 10 SDK, and the WebView2 runtime.

```bash
dotnet build AstroValleyAssistant.sln
dotnet run --project src/AstroValley.Presentation
```

Regrid credentials and other user settings are entered in the app and stored in `appsettings.user.json` next to the executable. Don't commit that file.

## About

This is a hobby project and is not intended to be sold. You are free to use it as-is. No warranty is provided, and I am not responsible for any outcomes resulting from its use.

## Future plans

- Installer (.msi/.exe) for download from GitHub.
- More tools for property analysis and data import/export.
