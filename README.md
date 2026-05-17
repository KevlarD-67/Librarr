# Librarr

> **Status: pre-alpha.** Forked from the archived
> [Readarr/Readarr](https://github.com/Readarr/Readarr) project (last
> upstream commit `0b79d300`, 2025-06-27). Goal: rebuild Readarr on top
> of Open Library as the primary metadata source. See
> [`MASTER-PLAN.md`](MASTER-PLAN.md) for the 12-phase revival plan and
> [`METADATA-MIGRATION.md`](METADATA-MIGRATION.md) for the technical
> migration sketch.

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

The metadata seam refactor, OpenLibrary proxy, and ID-bridge migration
are in flight. **Do not use this fork on a production library yet** —
the reidentify wizard for migrating existing Goodreads-ID libraries
isn't shipped. Track progress in [`MASTER-PLAN.md`](MASTER-PLAN.md).

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md). No CLA — contributions are
accepted under GPL v3 (inbound = outbound).

## License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2017-2025 readarr.com
* Copyright 2026-present Librarr Project
