# Product: Astro Valley Assistant

A Windows desktop application for researching tax-defaulted properties in the U.S. It helps users gather, enrich, and analyze property data from auction sources and parcel data providers.

## Core Workflows

- **Regrid Lookup** – Scrape parcel data from Regrid by parcel ID or address. Supports clipboard paste, file import, and batch scraping with status tracking per record.
- **RealAuction** – Pull tax deed auction listings from RealAuction county auction pages, including opening bids, assessed values, and auction dates.
- **Map View** – Visualize property locations on an interactive map using a WebView2-hosted HTML/JS map with marker pins.
- **Marker Map** – Export a set of pinned locations to a standalone HTML map file.
- **Import/Export** – Import property lists from files; export results to clipboard (tab-delimited) or Excel.

## Key Domain Concepts

- `PropertyRecord` – The central domain object combining auction data and Regrid parcel data.
- `RegridMatch` – A candidate parcel result when a lookup returns multiple matches.
- `ScrapeStatus` – Tracks per-record scrape state: Pending, Loading, Success, NotFound, MultipleMatches, Error, RateLimited.
- `MarkerLocation` – A lat/lon pin used for map export.

## Target Users

Individual researchers and investors analyzing tax-defaulted property auctions. This is a hobby/personal-use project, not a commercial product.
