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
  default `Dockerfile` is a 3-stage build (`sdk:6.0-alpine` →
  `node:20-alpine` → `aspnet:6.0-alpine` runtime). Compiles inside
  the image — no local toolchain needed. Runtime smoke (`docker build
  && docker run`) still pending and called out in the file header.

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

- [~] **Real-world OL JSON cassettes for the test suite**. Harness
  in place — `OpenLibraryFixtureLoader` + `Files/OpenLibrary/README.md`
  documents the capture recipe + corpus categories. The actual JSON
  cassettes still need to be captured live from openlibrary.org and
  committed (offline LLM session can't run those curls).

- [~] **Reidentify regression test**. Skeleton fixture
  `ReidentifyRegressionFixture` is in place, marked `[Explicit]` so
  it doesn't run in the default suite. The 6-step harness comment
  lists what's needed to make it real: a serialized 500-book library
  snapshot + OL cassettes + a cassette-backed proxy stub. Blocked
  on the cassette work above and on capturing a real seed library.

## Later (1.1+)

All five items here are documented in `docs/deferred-modernization.md`
with the specific reason each is deferred. All five remain deferred
after this session — none are safely-completable in an offline LLM
session. See that doc for the assessment per item.

- [ ] **.NET 8 LTS upgrade**. Blocked on Servarr-forked NuGet packages
  (no `net8.0` builds exist for them yet).

- [ ] **Nullable enable**. Several-thousand-error build without
  per-file human triage; not a single-session task.

- [ ] **React 17 → 18 + frontend dep refresh**. Mechanical bumps are
  cheap but `react-dnd` / `react-virtualized` / `react-popper` need
  replacements with breaking API changes — needs visual regression
  testing this session can't do.

- [ ] **Selenium → Playwright**. Quarantined since Phase 1; port
  after the cassette work below so a regression suite exists at all.

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
