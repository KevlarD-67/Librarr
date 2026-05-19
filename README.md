# Librarr

> **1.0.0-beta — 2026-05-19.** Forked from the archived
> [Readarr/Readarr](https://github.com/Readarr/Readarr) project (last
> upstream commit `0b79d300`, 2025-06-27). Rebuilds Readarr on top of
> Open Library as the primary metadata source.
>
> See [`CHANGELOG.md`](CHANGELOG.md) for full release notes,
> [`MASTER-PLAN.md`](MASTER-PLAN.md) for the strategic roadmap, and
> [`ARCHITECTURE.md`](ARCHITECTURE.md) § "Librarr fork additions" for
> a map of what changed in the fork.

Librarr is an ebook and audiobook collection manager for Usenet and
BitTorrent users. It monitors RSS feeds for new books from your favorite
authors and will grab, sort, and rename them. Like its predecessor, only
one type of a given book is supported per instance — run two instances
if you want both an audiobook and ebook of the same title.

## Heritage

Librarr inherits its codebase from Readarr, which was itself forked from
Sonarr in the Servarr family (Sonarr / Radarr / Lidarr / Readarr).
Internally many namespaces and assemblies still carry the `Readarr` and
`NzbDrone` names — see [`CLAUDE.md`](CLAUDE.md) and
[`ARCHITECTURE.md`](ARCHITECTURE.md) for the full identity map.

## Migrating from Readarr

Librarr ships a hands-off first-boot migration for existing Readarr
libraries. Point the container at your old Readarr `config/` directory
(it already contains your authors, books, and download history) and
start it — `LegacyMigrationService`
(`src/NzbDrone.Core/Books/Services/LegacyMigrationService.cs`) takes
over from there:

1. On `ApplicationStartedEvent` it scans the imported DB for legacy
   GoodReads-shaped IDs.
2. If it finds any, it flips `MonitorNewItems` to `None` per-author so
   the OpenLibrary refresh path doesn't grab unwanted new editions
   mid-migration.
3. It enqueues `ReidentifyLibraryCommand` at high priority. The
   reidentify pipeline walks every book, matches it against
   OpenLibrary using ISBN / ASIN / title-author confidence scoring,
   and writes results into the `BookIdMapping` bridge table
   (migration `041_book_id_mapping.cs`).
4. A frontend banner
   (`frontend/src/App/LegacyMigrationBanner.js`) reports progress
   while it runs, then auto-hides when done. The companion health
   check (`LegacyMigrationCheck`) surfaces problems if the marker
   never sets.
5. A persisted marker (`LegacyMigrationCompleted` in `config.xml`)
   prevents re-runs on subsequent restarts.

If you already manually reidentified your library before upgrading, the
migration detects the pre-populated `BookIdMapping` table and skips
straight to setting the marker.

## Major Features

* Watches for better quality of the ebooks and audiobooks you have and
  does automatic upgrades (e.g., from PDF to AZW3).
* Cross-platform: Windows, Linux, macOS, Raspberry Pi.
* Automatically detects new books.
* Scans your existing library and downloads missing books.
* Failed-download handling: will try another release if one fails.
* Manual search to pick any release or see why one was skipped.
* Profiles for fine-grained quality / format preferences.
* Configurable book renaming.
* Supports SABnzbd, NZBGet, qBittorrent, Deluge, rTorrent, Transmission,
  uTorrent, and other download clients.
* Calibre integration (add to library, conversion) — requires Calibre
  Content Server.

## What changed vs Readarr

| Area | Readarr | Librarr |
|---|---|---|
| Primary metadata source | BookInfo (Goodreads-derived, unusable) | Open Library (native) |
| Series metadata | Goodreads | Wikidata SPARQL |
| Audiobook supplement | none | audnex.us (opt-in) |
| CI | Azure Pipelines | GitHub Actions |
| Sentry / telemetry | servarr.com | none (until the fork stands up its own) |
| CLA | Required (assigns rights to Servarr) | None — GPL v3 inbound = outbound |
| Status | Archived 2025-06-27 | Active fork |

## Status

**1.0.0-beta — engineering gate cleared.** The OpenLibrary metadata
proxy, BookIdMapping bridge, reidentify pipeline, first-boot
migration, and downstream import-loop fixes are all shipped. See
[`CHANGELOG.md`](CHANGELOG.md) for the per-cycle breakdown.

Caveats:

- Field-validated on a single deployment so far. Field reports
  welcome.
- The fork does not publish docker images to any registry yet — build
  locally from [`distribution/docker/`](distribution/docker/).
- Several known follow-ups remain (duplicate-book-record dedupe,
  broader indexer coverage). Track progress in
  [`MASTER-PLAN.md`](MASTER-PLAN.md).

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md). No CLA — contributions are
accepted under GPL v3 (inbound = outbound).

## License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2017-2025 readarr.com
* Copyright 2026-present Librarr Project
