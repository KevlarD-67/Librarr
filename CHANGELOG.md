# Changelog

All notable changes to Librarr are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and this project loosely follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

## [1.1.0-beta] — 2026-07-30

First release since `1.0.0-beta`, and the first to publish images for
`linux/arm64` and `linux/arm/v7` — ARM users no longer have to build
locally.

### Added

- **Library Import wizard** (`/add/import`, `frontend/src/LibraryImport/`).
  Walks the folders inside a root folder that no author occupies and pairs
  each one with an Open Library author, then adds them in a single
  request. The author is bound to the folder that already exists on disk
  rather than one derived from the naming format, so existing files are
  adopted instead of orphaned. Backed by
  `RootFolderService.GetUnmappedFolders()` (surfaced as
  `RootFolderResource.unmappedFolders`) and `POST /api/v1/author/import`.
  Readarr never shipped this page — it was dropped when Readarr forked
  from Lidarr, leaving `UnmappedFolder.cs` and `ImportArtistDefaults.cs`
  as dead code.
- **Rescan button on each root folder** (Settings → Media Management).
  Triggers `RescanFoldersCommand` for that folder alone, so an existing
  collection can be picked up without waiting for the scheduled task.
- **Multi-arch docker images.** `linux/amd64`, `linux/arm64` and
  `linux/arm/v7`, published as one manifest list to GHCR and Docker Hub.
  Both `Dockerfile` build stages now pin `--platform=$BUILDPLATFORM` and
  resolve the .NET RID from `TARGETARCH`/`TARGETVARIANT`, so the
  cross-compile happens natively instead of under QEMU emulation.

### Fixed

- **Author search returned OpenLibrary's ranking unchanged, which is wrong
  for folder names.** `/author/lookup` passed OL's `/search/authors.json`
  order straight through, and the Library Import wizard auto-selects the
  first result — so whatever OL ranked first is what got imported. Searching
  `Tolkien, J.R.R.` (an ordinary Calibre folder convention) put a 1-work
  archaeology report ahead of the real 355-work J.R.R. Tolkien, purely
  because the report's title contains `(Tolkien, J`. Results are now
  re-ranked on name match, with OL's work count as a bounded tiebreak so a
  prolific unrelated author can never displace an exact match. Nothing is
  filtered out — a stub record is demoted, never hidden, because OL's index
  lags and a genuinely new author can legitimately report zero works.
  *(`OpenLibrarySearchMapper.ReRankAndMapAuthors`.)*
- **Same-named authors were impossible to tell apart.** OL routinely returns
  several records per author that are identical in every field Librarr
  mapped — three "Stephen King" records at 606, 48 and 7 works, the last of
  whom wrote *Principles of Macroeconomics*. The work count and best-known
  title were already in OL's response and already parsed; they were simply
  discarded by the mapper. They now reach the UI, so the wizard's dropdown
  reads `Stephen King — 606 work(s), Carrie` instead of three identical
  rows. Carried as transient fields — never persisted, and excluded from
  entity equality so they can't make an author look permanently dirty to
  `AuthorMetadataRepository.UpsertMany`.
- **`scripts/playwright-install.sh` could never work in this repo.** It
  looked for the Playwright CLI under `src/NzbDrone.Playwright.Test/bin`,
  which nothing writes to — `Directory.Build.props` redirects all test
  output to `_tests/` — and it shelled out to `pwsh`, requiring PowerShell
  on a Linux or macOS dev box. It now locates the CLI under `_tests/` and
  drives Playwright's own bundled Node directly.
- **Interactive search returned HTTP 500 for any book with no monitored
  edition.** `ReleaseSearchService` used `SingleOrDefault` on the
  monitored-edition set, which throws on both zero matches and more than
  one. Measured at 42% of books in a freshly added library. Now prefers a
  monitored edition, falls back to any edition, and finally to the book
  title. *(`src/NzbDrone.Core/IndexerSearch/ReleaseSearchService.cs`.)*
- **Cover fetches could be rejected by Open Library.** The covers API
  meters requests keyed by ISBN but explicitly exempts CoverID and OLID
  lookups, and a library refresh issues one ISBN-keyed request per
  edition lacking a work-level cover. Only that form is now throttled —
  throttling the exempt majority would slow every refresh for nothing.
- **`HttpClient.DownloadFileAsync` accepted a `userAgent` and discarded
  it.** Callers that asked to identify themselves were silently sending
  the default. Now applied, along with the request rate limit.
- **Docker images were never version-stamped**, so `RuntimeInfo`
  classified every containerised install as a non-production build.

### Changed

- **Librarr now identifies itself honestly to Open Library, Wikidata and
  Audnex** — a real User-Agent carrying the app name, version and a
  contact URL, per each service's stated policy
  (`MetadataUserAgent.cs`). The spoofed browser strings in
  `GoodreadsProxy` are deliberately left alone and documented as such;
  that proxy exists only for the legacy rollback path.
- `.gitignore` tightened to cover local docker volumes, smoke-test
  output, coverage results, test logs and `.env` files.

### Documentation

- `docs/ebooks-and-audiobooks.md` — what one instance can and cannot do
  with both formats. The usual one-line summary ("single format per
  instance") is not accurate: the real constraint is per-author, because
  an author carries exactly one quality profile and that profile is a
  single ordered ranking.
- `distribution/docker/README.md` — corrected the claim that images are
  not published anywhere, and documented the versioning and toolchain
  caveats of `Dockerfile.prebuilt`.
- `docs/roadmap.md` — two entries were recording things that aren't true.
  The .NET 8 upgrade was listed as blocked on the Servarr-forked NuGet
  packages having no `net8.0` build; they all ship `netstandard2.0`, which
  `net8.0` consumes unchanged. The real cost is triaging three frameworks'
  worth of analyzer changes under `TreatWarningsAsErrors`. Selenium →
  Playwright was listed as quarantined, but the suite has shipped.
- `src/NzbDrone.Playwright.Test/README.md` — documented why the suite
  cannot currently be executed (the pinned 1.40.0 browser build is no
  longer served) and what a version bump has to contend with.

## [1.0.0-beta] — 2026-05-19

First public release of Librarr — the engineering-gate-cleared
continuation of the archived
[Readarr/Readarr](https://github.com/Readarr/Readarr) project on
OpenLibrary metadata. Forked from upstream `0b79d300` (2025-06-27,
"Retirement announcement"); see
[`MASTER-PLAN.md`](MASTER-PLAN.md) for the strategic blueprint and
[`ARCHITECTURE.md`](ARCHITECTURE.md) § "Librarr fork additions" for the
fork's code-level inventory.

### Added

- **Native OpenLibrary metadata source.** `OpenLibraryProxy` plus
  author / book / edition search services and mappers replace the
  retired BookInfo / GoodReads-derived path entirely
  (`src/NzbDrone.Core/MetadataSource/OpenLibrary/`).
- **`BookIdMapping` bridge table** (migration `041_book_id_mapping.cs`)
  records confidence-scored GoodReads → OpenLibrary ID mappings for
  every legacy book in the library. Backed by
  `BookIdMappingRepository.cs`. *(Cycles 4, 5.)*
- **`ReidentifyLibraryCommand` + `ReidentifyService`** walks the
  library, matches every existing book against OpenLibrary using
  ISBN / ASIN / title-author confidence scoring, and writes mappings
  into the bridge table.
- **First-boot legacy migration.** `LegacyMigrationService` runs on
  `ApplicationStartedEvent`: detects GoodReads-shaped IDs, flips
  `MonitorNewItems` to `None` per-author for the duration, enqueues
  `ReidentifyLibraryCommand` at high priority, and sets a persisted
  marker on completion so it never re-runs. Companion
  `LegacyMigrationCheck` surfaces stuck state via the health system.
  *(Cycle 6.)*
- **Frontend migration banner** (`frontend/src/App/LegacyMigrationBanner.{js,css,Connector.js}`),
  wired into `Page.js`. Polls health + active commands every 15 s
  and reports migration progress; auto-hides when the marker sets.
  *(Cycle 6.)*
- **Pickable cover modal** with canonical OpenLibrary cover as the
  default. Backed by `Book.PreferredCoverUrl` column (migration
  `045_book_preferred_cover_url.cs`). Includes a bench harness in
  `scripts/bench_le_guin.py` for evaluating cover-pick quality.
  *(Cycle 1.)*
- **Edition-language mapping.** OpenLibrary's two-letter and verbose
  language identifiers now hydrate `Edition.Language`. Bench harness
  reports mean coverage of language metadata up from 0 / 240 to
  240 / 240 (+9.6 percentage points overall). *(Cycle 2.)*
- **Edition-richness tiebreaker.** When OpenLibrary returns multiple
  candidate editions for a work, the picker now prefers richness
  (covers, descriptions, ISBN/OCLC presence, language, number of
  pages). +33 books picked up at least one previously-blank field
  across seven categories. *(Cycle 3.)*
- **Narrator surface for audiobooks.** Normalized narrators schema
  (`043_normalized_narrators.cs`), `NarratorService` wired into
  `RefreshEditionService`, narrator-chips frontend, dedicated
  per-narrator detail page, REST surface in `Readarr.Api.V1.Narrators`.
- **`.azw` file recognition** (Kindle KF7 / older Mobipocket).
  Now mapped to `Quality.MOBI` in `MediaFiles/MediaFileExtensions.cs`.
  *(Cycle 7c.)*
- **`CHANGELOG.md`** (this file). Future cycles will append their
  entries to `[Unreleased]` above.
- **`distribution/docker/README.md`** — local docker quickstart.

### Changed

- **Default `MetadataSourceType` is now OpenLibrary** in fresh
  installs. `bookinfo.club` is retired and no longer reachable.
- **Search prefix syntax** routes to OpenLibrary by identifier shape
  (drops the GoodReads-specific prefixes).
- **Refresh path** stops cascade-adding the entire author's
  discography on Add Book, and stops wiping real metadata on retry.
  Add Book now refreshes just the book; Add Author refreshes the
  full discography but defaults to unmonitored.
- **Books library** keeps to explicit adds only; the author page
  exposes the full discography for browse / pick.
- **CI** moved from Azure Pipelines to GitHub Actions.
- **Selenium → Playwright** scaffolding for end-to-end tests.
- **React 17 → 18** (minimal swap; class-component dominant code
  unchanged).
- **Identity rebrand.** User-facing strings, README, and packaging
  artifacts now say "Librarr". Internal `Readarr.*` csproj names and
  `NzbDrone.*` namespaces are deliberately preserved — see
  [`CLAUDE.md`](CLAUDE.md) "Identity quirk".
- **CI version** bumped to `1.0.0-beta` in
  `azure-pipelines.yml:22`.

### Fixed

- **NZB grabs were 100 % failing on NZBgeek / NzbPlanet etc.** Caused
  by the dev-mode redirect-rejection guardrail in `HttpClient.cs:101`
  firing for every CDN redirect because locally-built docker images
  don't run as Azure `officialBuild`. Fix: opt the NZB-grab request
  into `AllowAutoRedirect = true` in
  `Download/UsenetClientBase.cs`, matching the explicit pattern used
  in `OpenLibraryProxy.cs` and elsewhere. *(Cycle 7a.)*
- **Silent import failures.** Rejected `ImportDecision`s were
  swallowed at debug level and never surfaced to the user. Fix:
  `ImportApprovedBooks.cs` now materializes every rejection as a
  visible `ImportResult` entry, logs a Warn line with the rejection
  reasons, and publishes `TrackImportFailedEvent`. Real downloads
  also light up a `BookImportIncomplete` row in Activity history via
  the existing `CompletedDownloadService.Process` chain. *(Cycle 7d.)*
- **`Add Book` NREs** on the search → add flow.
- **Single-character search queries** no longer hammer OpenLibrary
  and produce 422-noise warnings.
- **Search dedupes author tiles** by normalized name and prefers
  book-derived OLIDs over fuzzy guesses.
- **`Add Author with Monitor=None`** actually means None (used to
  silently slip into "All").
- **Refresh path** hardened against transient failures and
  zero-edition aborts.

### Migration notes

- Existing Readarr libraries: mount your old config directory at
  `/config`, start the container, and the first-boot
  `LegacyMigrationService` handles the rest. See
  ["Migrating from Readarr"](README.md#migrating-from-readarr) in the
  README, or
  `src/NzbDrone.Core/Books/Services/LegacyMigrationService.cs` for
  the source of truth.
- `MetadataSourceType` flips to OpenLibrary by default. If you have
  the old `bookinfo.club` value persisted, it is silently ignored —
  the endpoint is gone.
- The `BookIdMapping` table is additive and uses migration `041`.
  Library DB schema migrations also include `042-045` for
  edition narrators, narrator normalization, dropping the legacy
  `Editions.Narrators` column, and `Book.PreferredCoverUrl`.

### Out of scope (deferred)

- Duplicate-book-record dedupe — ~128 author-title clusters
  introduced by the Cycle 5 OL refresh. Needs a normalized-title
  dedupe pass; tracked for a future cycle.
- Public docker registry publish.
- GitHub remote push + GitHub Release artifact.
- Internal `Readarr.*` csproj rename / `NzbDrone.*` namespace
  rebrand (deliberately preserved).

[Unreleased]: https://github.com/Rorqualx/Librarr/compare/v1.1.0-beta...HEAD
[1.1.0-beta]: https://github.com/Rorqualx/Librarr/compare/v1.0.0-beta...v1.1.0-beta
[1.0.0-beta]: https://github.com/Rorqualx/Librarr/releases/tag/v1.0.0-beta
