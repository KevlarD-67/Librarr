# Librarr roadmap

Status as of the 1.0.0-beta line. Items are roughly ordered by
priority; nothing here is a hard commitment.

## Now (1.0.0-beta cycle)

- [x] **Field-tag reidentify pass** (Phase 5b). Landed in commit
  `a4acdc9`. `ReidentifyService.FileTagPass` walks every Book with files,
  reads `IMetadataTagService` tags, looks up OL by ISBN → ASIN →
  Title+Author, and overwrites any non-Manual existing mapping with a
  `BookIdMappingSource.FileTag` row at 0.97 / 0.92 / 0.78 confidence
  respectively. `ResolveOverride` pure helper extracted for testing.

- [x] **Dedicated low-confidence review UI** (Phase 9c). Landed.
  New API endpoint `/api/v1/metadata/lowconfidencemapping` (GET list,
  PUT manual override). New Settings → Metadata panel
  `LowConfidenceMappings` rendering rows with confidence < 0.70, with
  inline OL Work/Edition ID editing and "Save as Manual" button. The
  Phase 5 wizard's "done" copy now points at this panel instead of
  System → Logs. Manual rows are pinned at confidence 1.0 and are
  protected from overwriting by both reidentify pass and the file-tag
  pass (per `ReidentifyService.ResolveOverride`).

- [x] **Cover URL wiring for OpenLibrary** (Phase 4b). Landed. New
  helper `OpenLibraryCoverUrls` constructs `covers.openlibrary.org/b/id/…`
  and `…/a/id/…` URLs from the integer IDs in OL JSON. Edition mapper
  now emits `MediaCoverTypes.Cover`, author mapper emits
  `MediaCoverTypes.Poster`, work mapper backfills editions that lack
  their own cover. Sentinel negative IDs are filtered out.

- [x] **OpenLibraryDescriptionConverter coverage of edge JSON shapes**.
  Landed. Converter now handles array-of-strings (joined with newlines),
  `{text: ...}` legacy form, and nested-object `value`. Unexpected
  scalars / non-string array contents return null with a debug-level
  log. Nine-row regression fixture covers each shape.

- [x] **Self-contained Dockerfile** (Phase 9b skeleton). Landed. The
  original `Dockerfile` was renamed to `Dockerfile.prebuilt`; the new
  default `Dockerfile` is a 3-stage build (`sdk:10.0-alpine` →
  `node:24-alpine` → `aspnet:10.0-alpine` runtime). Compiles inside
  the image — no local toolchain needed. Runtime smoke (`docker build
  && docker run`) has since been done on x86_64, both locally and on a
  real server, and on **native aarch64** against the published
  `1.1.0-beta` image: healthcheck, UI, live OL search, unmapped-folder
  scan, Library Import and a full discography refresh, no errors.
  `linux/arm/v7` completed the same workload only under emulation,
  where QEMU's own Thumb translator then asserted
  (`target/arm/tcg/translate.c`) — an emulator defect, not an
  application one, and so still not a verdict either way on real
  32-bit ARM hardware.

## Soon (1.0.0 stable)

- [x] **Audnex augmenter wired into RefreshBookService**. Landed.
  `RefreshBookService.GetSkyhookData` calls
  `IAugmentAudiobookInfo.Augment` after the primary metadata source
  returns. CanAugment gates on the opt-in config flag, so disabled
  installs pay no cost. Augmenter failures swallow into Debug — the
  primary refresh path is never blocked.

- [x] **OpenLibraryAuthorImportList + OpenLibraryTrendingImportList**.
  Landed. Both follow OpenLibrarySubjectImportList's shape:
  `IHttpClient + IOpenLibraryRequestBuilder` injection, `Fetch()` calls
  one OL endpoint, validation probes with `limit=1`. Author list reads
  `/authors/{key}/works.json`; trending reads `/trending/{period}.json`
  with the period restricted to OL's documented set (now/daily/weekly/
  monthly/yearly/forever). DryIoc auto-discovers both via reflection
  on `ImportListBase` — no manual registration needed.

- [x] **Narrator field on Edition**. Landed. Migration 042 adds
  `Editions.Narrators` (nullable text, comma-separated). Edition model
  + EditionResource carry it through. Audnex augmenter now writes
  narrator names (joined on `, `). Book details header shows
  "Narrated by …" alongside page count when present. A normalized
  Narrators table is a future refactor — see the migration comment.

- [x] **Real-world OL JSON cassettes for the test suite**. 117 real
  OL captures are committed under
  `src/NzbDrone.Core.Test/Files/OpenLibrary/`; the capture recipe
  itself is automated by `scripts/capture-ol-cassettes.sh`.
  `OpenLibraryFixtureLoader` + the README in that directory document
  the corpus categories and re-capture procedure. (Earlier `[~]`
  marker was stale; finalization-pass audit confirmed the corpus
  is in tree.)

- [x] **Reidentify regression test**. `ReidentifyRegressionFixture`
  runs in the default suite (`[TestFixture]`, not `[Explicit]`). It
  seeds 10 books programmatically — 5 ISBN-13s + 5 title+author
  shapes — and drives the real `OpenLibraryProxy` against a
  cassette-backed `IHttpClient` stub, asserting the recorded
  mappings clear the 0.85 threshold. The earlier `[~]` was based on
  an outdated reading of the harness comment; a 500-book snapshot
  is documented as a future "stable gate" enhancement (see
  `docs/release-checklist.md`) but is not blocking.

## Later (1.1+)

The items below are documented in `docs/deferred-modernization.md`
with the specific reason each is deferred. They remain explicitly
deferred per the v1.0.0 release checklist (`docs/release-checklist.md`),
and none are safely-completable in an offline LLM session. See the
deferred-modernization doc for the assessment per item.

- [ ] **.NET 8 LTS upgrade**. The reason recorded here previously — that
  the Servarr-forked NuGet packages have no `net8.0` build — is wrong.
  `Servarr.FluentMigrator.Runner{,.SQLite,.Postgres}` and
  `System.Data.SQLite.Core.Servarr` all ship `netstandard2.0`
  alongside `net461`, and `netstandard2.0` is consumable from `net8.0`
  unchanged; no framework-specific build is needed. The actual cost is
  `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` meeting three
  framework versions' worth of new and changed analyzers at once,
  across a codebase that currently builds clean only because it is
  pinned to the analyzers that shipped with .NET 6. That is a triage
  job, not a `TargetFramework` edit. Worth doing — .NET 6 went out of
  support in November 2024 — but it wants its own branch and its own
  session.

- [ ] **Nullable enable**. Several-thousand-error build without
  per-file human triage; not a single-session task.

- [x] **React core 17 → 18**. Landed in `ae4261b` (Phase 10
  closeout). `react` + `react-dom` at 18.3.1, bootstrap rewritten to
  use `createRoot`. Build clean, full unit suite passes on React 18.

- [ ] **React 18 ecosystem dep refresh**. `react-dnd@14`,
  `react-virtualized@9`, and `react-popper@1` still pinned. They
  work on React 18 as-is (verified by the Phase 10 swap not
  breaking the build), but each replacement is a non-trivial diff
  with breaking API changes. Not blocking; surface for a future
  visual-regression pass once Playwright has interaction coverage.

- [x] **Selenium → Playwright**. Landed as
  `src/NzbDrone.Playwright.Test/`. Seven page-load smokes (the six
  ported from the Selenium suite, plus Library Import), each asserting
  a page-specific DOM anchor, with the base class failing any test
  that leaves an error in the UI's `#errors` panel. Opt-in behind
  `READARR_RUN_PLAYWRIGHT=1` because it needs a built backend, a built
  frontend, and a ~250 MB browser bundle on disk. Interaction and
  visual-regression coverage remain out of scope and still want the
  cassette work below.

- [x] **Playwright suite actually runs.** Eight tests green on the
  pinned 1.40.0, four consecutive clean runs, ~3s. Getting there fixed
  three things: `add_author_page` matched two elements on "Add New" and
  had presumably never passed; `library_import_page` clicked a sidebar
  child without expanding its section first; and the per-fixture browser
  lifecycle raced with `NzbDroneRunner.KillAll()`, which kills every
  Readarr by name — the browser and instance now live in `AssemblyGate`,
  one per assembly. Notes in
  [`src/NzbDrone.Playwright.Test/README.md`](../src/NzbDrone.Playwright.Test/README.md).

- [ ] **OL bulk-data dump fallback**. Fork position + trigger
  conditions to revisit are in [`docs/ol-bulk-data.md`](ol-bulk-data.md).
  Phase 12+ candidate.

## Won't (until persuaded otherwise)

- [ ] Namespace rename NzbDrone.* → Librarr.*. Cosmetic, ~2000 file
  touch. Directory.Build.props:97-99 deliberately keeps the legacy
  namespace as a heritage signal.

- [ ] rreading-glasses shim adoption. The fork explicitly rejected
  this in favor of native OL — see Phase 0 design discussion.

- [ ] Reintroducing the CLA. The Librarr fork dropped the upstream
  CLA in favor of GPL inbound = outbound (CLA.md), and there's no
  current pressure to reverse that.

---

Reorder freely. Open a PR against this file with the rationale for
any priority changes.
