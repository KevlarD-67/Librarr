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

- [ ] **Dedicated low-confidence review UI** (Phase 9 polish). The
  current MetadataSwitchWizard surfaces low-confidence rows to the log
  only. Add a Settings → Metadata sub-page that reads
  `BookIdMappingRepository.GetLowConfidence(0.7)` and lets the user
  override the OL ID manually.

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

- [ ] **Self-contained Dockerfile** (Phase 9b). The Phase 9 Dockerfile
  copies `_output/`; a multi-stage version that builds inside the
  image is more reproducible.

## Soon (1.0.0 stable)

- [ ] **Audnex augmenter wired into RefreshBookService**. The proxy
  exists; the IExecute<RefreshBookCommand> handler doesn't yet call it.

- [ ] **OpenLibraryAuthorImportList + OpenLibraryTrendingImportList**.
  Phase 6 shipped only OpenLibrarySubjectImportList; the other two from
  the master plan follow the same pattern.

- [ ] **Narrator field on Edition**. Required to surface audnex narrator
  data properly. Schema migration + API change + frontend display.

- [ ] **Real-world OL JSON cassettes for the test suite**. Phase 8
  shipped hand-crafted Resource fixtures. The "golden corpus" the
  master plan calls for needs a 100+ work sampling across fiction,
  non-fiction, audiobook, foreign-language, pseudonymous, prolific
  cases.

- [ ] **Reidentify regression test**. Snapshot a 500-book
  Goodreads-ID-shaped library; run the wizard; assert match rate
  stays >= 85%.

## Later (1.1+)

- [ ] **.NET 8 LTS upgrade**. Plan in `docs/deferred-modernization.md`.
  Blocked on Servarr-forked NuGet packages.

- [ ] **Nullable enable**. Incremental rollout, one project at a time.

- [ ] **React 17 → 18 + frontend dep refresh**. Plan in
  `docs/deferred-modernization.md`. Blocks on react-dnd / react-popper
  / react-virtualized replacements.

- [ ] **Selenium → Playwright**. Quarantined since Phase 1.

- [ ] **OL bulk-data dump fallback**. archive.org publishes full OL
  dumps. For users that want fully offline metadata, surface a job
  that ingests the latest dump into a local mirror.

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
