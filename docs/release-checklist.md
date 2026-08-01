# Release checklist

Defines what each Librarr release tag actually requires. Two
sequential gates — beta first, then stable — plus an explicit
deferred-to-1.1+ list so a release decision doesn't accidentally
fall over an unmade tradeoff.

Cross-references:

- Deferral rationale: [`deferred-modernization.md`](deferred-modernization.md)
- OL bulk-data fork position + revisit triggers: [`ol-bulk-data.md`](ol-bulk-data.md)
- Bus factor + quarterly cadence: [`governance.md`](governance.md)
- Phase-by-phase delivery log: [`../MASTER-PLAN.md`](../MASTER-PLAN.md)

---

## v1.0.0-beta

The first publishable artefact. Goal: an artefact someone can
install and exercise end-to-end against the Open Library backend
that Phases 2–5 wired up.

### Engineering

- [x] `./test.sh Mac Unit Test` green on local Linux/macOS — verified
  during the finalization pass (3359 passed, 123 skipped, 0 failed
  across nine assemblies; `TEST_DIR=_tests/net10.0` env required, see
  test.sh for the path-resolution quirk).
- [x] `dotnet build src/Readarr.sln -c Debug` clean (0 errors, 0
  warnings).
- [x] `yarn lint` + `yarn stylelint-linux` clean.
- [x] `tests/e2e/smoke.sh` runs in CI as the blocking `e2e-smoke`
  job (`.github/workflows/build.yml:209-242`). Boots the published
  `linux-x64` backend artefact, asserts `/api/v1/health` +
  `/api/v1/system/status`, fires a `ReidentifyLibrary` command.
- [x] Playwright smoke suite, 14 tests (`src/NzbDrone.Playwright.Test/`):
  7 page-load smokes (6 from the Phase 10 port plus the
  narrator-detail page), 2 root folder form checks, 2 Add Author
  search checks and 2 seeded library round-trips. Gated by
  `READARR_RUN_PLAYWRIGHT=1`; opt-in per the suite's README.

  It could not run at all between the .NET 10 migration and
  `5447db9` — a stale driver in the shared `_tests/` output made
  every test die in `CreateAsync`, and Playwright 1.40's Chromium
  crashed on current macOS about one run in three. The gate now
  checks driver against client up front.
- [x] `docker build -f distribution/docker/Dockerfile -t librarr/librarr:test .`
  builds clean (~180 MB alpine 6.0 runtime). `docker run` boots,
  serves `/ping`, passes `tests/e2e/smoke.sh`. Eight Phase 9b
  skeleton bugs were caught and fixed in the realisation pass —
  see commit history for the per-bug breakdown.
- [x] Fresh-install metadata source defaults to OpenLibrary. The
  legacy `BookInfo` default pointed at `api.bookinfo.club` (retired
  upstream 2025-06-27); flipped in `ConfigService.cs:278` so fresh
  installs don't land on a non-functional search. Existing installs
  that explicitly set `MetadataSourceType=BookInfo` migrate via the
  Phase 5 reidentify wizard.
- [ ] **Manual operator walkthrough** (no automated coverage —
  needs a seeded library; partially verified in the realisation
  pass via the smoke container):
  1. [x] Navigate to `/narrator/999999`, confirm "Narrator not
     found." error state. (Verified live against
     `librarr/librarr:test` container.)
  2. [x] Author lookup hits OpenLibrary (Phase 3 proxy), returns
     `foreignAuthorId` like `OL1394865A`. (Verified via
     `/api/v1/author/lookup?term=Brandon%20Sanderson`.)
  3. [ ] Add an author end-to-end (`/add/new`), refresh metadata,
     confirm books + editions + covers populate within 60 s.
  4. [ ] Open a book detail page; confirm "Narrated by …" chips
     render when audiobook editions exist.
  5. [ ] Click a narrator chip; land on `/narrator/:id`; confirm
     book list renders with author links back to `/author/:slug`.

### Docs

- [x] Roadmap `[~]` items reconciled to `[x]` (cassettes + reidentify
  regression — both were already landed; the roadmap had not been
  updated post-Phase 5).
- [x] React 17 → 18 "Later" entry split into core ✅ (Phase 10) +
  ecosystem deps ❌ (deferred).
- [x] Deferred-modernization dispositions confirmed per the table
  in this checklist's "Deferred-to-1.1+" section below.
- [ ] CHANGELOG entry covering Phases 0–12 (one-line per phase
  closeout, linking to the closeout commit hash).

### Out-of-engineering (manual user-action)

These cannot be executed from an LLM session; they are gates the
human maintainer must pass through before tagging the beta.

- [ ] Fork remote configured. The local `origin` still points at
  the archived upstream:
  ```bash
  git remote rename origin upstream
  git remote add origin git@github.com:<user>/Readarr.git
  git push -u origin main
  ```
- [ ] Release secrets configured for `release.yml`. The workflow
  fires on `v*` tag push; inspect `secrets:` references inline.
- [ ] **Decide whether to code-sign the Windows build.** Optional —
  with no certificate the pipeline still produces the zips and the
  installers, just unsigned, and `distribution/windows/sign.ps1`
  says so and exits 0. Unsigned means every user meets a SmartScreen
  "unrecognised app" warning on first run, and a fresh certificate
  earns reputation slowly, so signing only stops being painful some
  weeks after the first signed release. To turn it on, add two
  repository secrets:
  - `WINDOWS_CERT_PFX` — the code-signing certificate as a base64
    PKCS#12 blob: `base64 -w0 cert.pfx` (macOS: `base64 -i cert.pfx`).
  - `WINDOWS_CERT_PASSWORD` — that .pfx's password.

  An EV certificate on a hardware token cannot be used this way; it
  needs a signing service the runner can call instead. Once both
  secrets exist nothing else changes — the `build-windows` and
  `installer` jobs pick them up, and the draft release notes switch
  from the SmartScreen warning to stating the build is signed.
- [ ] `v1.0.0-beta` tag pushed. Triggers the release pipeline
  (`.github/workflows/release.yml`):
  ```bash
  git tag -a v1.0.0-beta -m "Librarr 1.0.0 beta"
  git push origin v1.0.0-beta
  ```
  Pipeline produces multi-RID binary tarballs + a `push: false`
  Docker build. A draft GitHub release is created automatically.

---

## v1.0.0-stable (gated on beta + governance)

Promotes the beta to stable. Not actionable until the beta artefact
has been in real use, the governance commitments are met, and the
remaining engineering coverage gaps are closed.

### Engineering

- [ ] Beta has been in the wild for at least 30 days with no
  critical regressions reported in `docs/state-of-the-fork/`.
- [ ] **Integration suite runnable unattended.** The fixtures work —
  the Goodreads identifiers are gone, `EnsureAuthor` resolves an
  OpenLibrary author id, and the six fixtures that were marked
  "Waiting for metadata to be back again" each pass against live
  OpenLibrary. What does *not* work is running them in one pass:
  every fixture starts from an empty appdata, so nothing is cached
  between them, and one full run of `ApiTests` was enough to have
  the source IP refused (`Connection refused (openlibrary.org:443)`
  on 26 of 88 tests). Until that is solved they have to be run one
  fixture at a time, which is not something CI can do usefully.

  Two plausible routes, neither attempted: share one appdata (and
  therefore one warm cache) across the assembly the way
  `NzbDrone.Playwright.Test` shares its instance, or record the
  OpenLibrary responses as cassettes — `OpenLibraryCassetteFixture`
  in the unit suite already has the machinery.
- [x] **Playwright chip → page round-trip test.** Done via the
  API-based seed (`LibrarySeeder`), not a checked-in SQLite file:
  a captured database pins the schema at whatever migration count
  it was taken on and rots the next time somebody adds one of the
  48. `SeededLibraryTest` adds one small author, then clicks
  library → author and asserts on the book rows themselves, so it
  cannot pass on an empty table. Verified it fails with seeding
  disabled.

  The seed is the suite's only OpenLibrary dependency, so it
  reports Inconclusive rather than red when the network is
  missing or throttling.
- [ ] **Cross-browser Playwright.** Firefox + WebKit launchers
  are Playwright one-liners; wire when needed for theme/CSS
  regression coverage.
- [ ] **500-book reidentify regression seed.** The current 10-book
  in-memory seed is sufficient to assert the 0.85 threshold; a
  larger snapshot makes the assertion statistically meaningful.
  Lives under `tests/regression/`; capture via
  `sqlite3 readarr.db .dump` from a populated install.

### Governance

Per [`governance.md`](governance.md):

- [ ] Bus factor ≥ 2 active maintainers OR a maintenance-mode
  declaration has been posted. Today the count is one; the
  remaining maintainer has 90 days from solo-state to recruit a
  second before maintenance-mode kicks in.
- [ ] First state-of-the-fork writeup published in
  `docs/state-of-the-fork/2026-Q2.md` (due by 2026-07-14 per
  the README in that directory). Template is already there.

### Out-of-engineering

- [ ] Docker registry login + `push: true` in `release.yml:259`
  (currently `push: false` per Phase 9b TODO). Decide which
  registry (GHCR vs. Docker Hub vs. self-hosted) and configure
  the credentials.

---

## Deferred-to-1.1+ (do NOT block v1.0)

Each item has a written disposition; future contributors should
not "fix" any of these in service of shipping 1.0.

| Item | Disposition | Reference |
|---|---|---|
| .NET 8 LTS | **Defer** until Servarr-forked NuGets (`System.Data.SQLite.Core.Servarr`, `TagLibSharp-Lidarr`, `Mono.Posix.NETStandard...-servarr22`, `Servarr.FluentMigrator.*`) ship `net8.0` builds. No trigger date. | [`deferred-modernization.md`](deferred-modernization.md) |
| Nullable enable | **Defer** as a multi-PR sprint post-1.0. Per-file gradual enable is the recommended approach — global enable with `TreatWarningsAsErrors` emits thousands of CS86xx errors. | [`deferred-modernization.md`](deferred-modernization.md) |
| react-dnd / react-virtualized / react-popper | **Defer**. React core is at 18.3.1 (Phase 10, `ae4261b`); ecosystem deps still work on 18, the replacements would be breaking diffs better done after Playwright has interaction coverage. | [`deferred-modernization.md`](deferred-modernization.md) + roadmap "Later" |
| Full Selenium → Playwright parity | **Defer beyond scaffold**. Seven smoke tests (`MainPagesTest` × 6 + `NarratorPageTest` × 1) are sufficient for the beta engineering gate. Additional Selenium-parity work is post-1.0. | [`deferred-modernization.md`](deferred-modernization.md) |
| OL bulk-data dump fallback | **Defer** until any of the four trigger conditions in [`ol-bulk-data.md`](ol-bulk-data.md) fires: sustained 429s, archive.org guidance shift toward dump-based consumers, install count >100, or OL live-API availability/cost change. | [`ol-bulk-data.md`](ol-bulk-data.md) |

The "won't (until persuaded otherwise)" items from
[`roadmap.md`](roadmap.md#wont-until-persuaded-otherwise) — namespace
rename, CLA reintroduction, rreading-glasses adoption — are also
explicitly not 1.0 blockers.

---

## Verification recipe

To re-verify the engineering gates (the ones marked `[x]` above):

```bash
# Unit suite
TEST_DIR=_tests/net10.0 ./test.sh Mac Unit Test

# Full solution build
dotnet build src/Readarr.sln -c Debug

# Lint
yarn lint && yarn stylelint-linux

# Playwright smoke (requires built backend + frontend + browser bundle)
./build.sh --backend
yarn install && yarn build
./scripts/playwright-install.sh
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/
```

Dormant items intentionally not in the recipe:

- Integration suite (`READARR_RUN_INTEGRATION=1`) — would fail
  setup against retired `api.bookinfo.club`.
- Selenium suite (`READARR_RUN_AUTOMATION=1`) — quarantined since
  Phase 1, kept for git-archaeology only.
