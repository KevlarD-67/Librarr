# Deferred modernization work (Phase 10 follow-ups)

The Phase 10 commit landed a subset of the master plan's modernization
items. The rest is documented here, with the reasoning for each
deferral, so a future session (or a future maintainer) can pick them up
without re-discovering the same constraints.

## .NET 6 → .NET 8 LTS

**Status:** deferred.

**Why not now:**

* `Directory.Packages.props` pins several Servarr-forked NuGet packages
  with `net6.0`-specific builds: `System.Data.SQLite.Core.Servarr`,
  `TagLibSharp-Lidarr`, `Mono.Posix.NETStandard...-servarr22`,
  `Servarr.FluentMigrator.*`. Bumping the TFM without rebuilding those
  forks against .NET 8 will fail at restore.
* The custom Dapper ORM under `NzbDrone.Core/Datastore/` makes light use
  of reflection helpers that have changed behavior under .NET 8's
  trim-friendly defaults. A real upgrade needs a regression pass against
  the live integration suite.

**What's needed:**

1. Audit `Directory.Packages.props` — pick a successor for each
   Servarr-forked package (newer fork OR upstream OR replacement OR
   maintain our own).
2. Bump `<TargetFrameworks>net6.0</TargetFrameworks>` →
   `<TargetFrameworks>net8.0</TargetFrameworks>` in
   `src/Directory.Build.props`.
3. Update `dotnetVersion` in CI (`.github/workflows/build.yml` +
   `azure-pipelines.yml`) from `6.0.x` to `8.0.x`.
4. Re-run the full integration suite. Expect surprises in
   `Datastore/Migration/` (FluentMigrator behavior under net8) and
   `NzbDrone.Common/Reflection/` (trim-related warnings).

## `<Nullable>enable</Nullable>` codebase-wide

**Status:** deferred.

**Why not now:**

* The inherited codebase has zero nullable annotations. Enabling
  globally with `TreatWarningsAsErrors=true` (already set in
  `Directory.Build.props:4`) would emit thousands of CS86xx errors at
  build time. Cleaning those up is genuinely person-weeks of work, not
  an LLM-session task.
* Even per-file `#nullable enable` opt-in needs human review to decide
  which references should be nullable — automated guessing is wrong
  often enough to be net-negative.

**What's needed:** roll it out incrementally per project, starting with
the lowest-coupling ones (`NzbDrone.Common.Test`, `Readarr.Http`).

## React 17 → React 18

**Status:** deferred.

**Why not now:**

* The package.json pin is `react@17.0.2`. A bump to 18.x triggers
  re-resolution of every dependent React lib in the tree
  (`react-dnd@14`, `react-virtualized@9`, `react-popper@1` — already
  flagged as peer-dep-incompatible with React 17). Some of those
  packages don't have React-18-compatible versions and would need
  replacing.
* The bootstrap entry-point at `frontend/src/bootstrap.tsx` would need
  to migrate from `ReactDOM.render` to `createRoot`. That part is
  trivial; the dependency story is what blocks.

**What's needed:**

1. Audit each `react-*` dep for React 18 compatibility.
2. Replace `react-dnd@14` with `react-dnd@16` (breaking API changes —
   the existing drag handlers need a rewrite).
3. Replace `react-popper@1` with `@popperjs/react@2` or floating-ui.
4. Replace `react-virtualized` with `react-window` or `@tanstack/virtual`.
5. Bump `react` and `react-dom` to `18.x`.
6. Update `bootstrap.tsx` to use `createRoot`.
7. Test under React 18's strict-mode double-render in dev — class
   components with side effects in `componentDidMount` may break.

## Selenium → Playwright

**Status:** deferred.

**Why not now:** Phase 1 already quarantined the existing Selenium 3
suite with `[Explicit]`. The port itself is a separate effort that
deserves its own session — Playwright .NET has a different API surface
(`IPage`, `IBrowserContext`) and the existing page-object pattern
(`src/NzbDrone.Automation.Test/PageModel/`) would need a rewrite.

**What's needed:**

1. Add `Microsoft.Playwright` (~80MB nupkg, downloads browser binaries
   at install time) to `Directory.Packages.props` for the test project.
2. Rewrite `AutomationTest.cs` setup using `IBrowserContext` instead of
   `ChromeDriver`.
3. Port `PageModel/PageBase.cs` and friends.
4. Remove the `[Explicit]` attribute once the suite passes again.

## Namespace cleanup (NzbDrone.* → Librarr.*)

**Status:** explicitly NOT recommended.

**Why not:**

* `src/Directory.Build.props:97-99` deliberately remaps `Readarr.*`
  csproj names back to `NzbDrone.*` namespaces. This is intentional —
  the codebase is partly upstream Servarr (Sonarr) heritage and the
  namespaces serve as a historical signal for which subsystems came
  from where.
* A full rename is ~2000 file touches. The risk/reward is bad: every
  `using` and every fully-qualified reference must change, and the
  payoff is purely cosmetic (the assembly names already say `Readarr`).
* If you really want this, do it with `dotnet format` or a Roslyn
  rewriter, NOT by hand or `sed`. And run the full integration suite
  after.

**Recommendation:** leave `NzbDrone.*` namespaces alone. The
`Directory.Build.props` remap stays.

## SBOM step

**Status:** ✅ done in Phase 10. See `.github/workflows/build.yml` `sbom`
job. Artifacts attached to each successful build (90-day retention).

## OL bulk-data dump fallback

**Status:** deferred to 1.1+. The standalone Phase 11 writeup at
[`docs/ol-bulk-data.md`](ol-bulk-data.md) is now the authoritative source —
it covers the technical sketch that used to live here plus the trigger
conditions that would flip the decision.

## Reidentify regression test (Soon → blocked on cassettes)

**Status:** harness scaffolded in `ReidentifyRegressionFixture`
(`[Explicit]`); needs cassettes + a real 500-book library snapshot.

**Why not now in this session:** capturing the snapshot requires a
running Readarr install with a real library and live OL HTTP. An
offline session can scaffold the test shape (done) but can't
populate the seed data.

## React 17 → 18 (Later bucket item, reaffirmed)

**Status:** still deferred. Same reasoning as above — `react-dnd@14`,
`react-virtualized@9`, and `react-popper@1` all need replacements
before the React bump is safe, and each of those replacements is a
non-trivial diff. An LLM session can do the mechanical part (bump
versions, run the codemods) but the visual regression check needs a
human at a browser, which this session cannot provide.

## .NET 8 LTS + Nullable enable (Later bucket items, reaffirmed)

**Status:** still deferred. The Servarr-forked NuGet packages
(`System.Data.SQLite.Core.Servarr`, `TagLibSharp-Lidarr`,
`Mono.Posix.NETStandard...-servarr22`, `Servarr.FluentMigrator.*`)
are pinned to `net6.0`-specific builds; bumping the TFM fails at
restore. Without a co-bump of those forks (or successor packages),
the .NET 8 work cannot start. Same blocker for Nullable: the
codebase has zero annotations, so flipping the switch produces a
several-thousand-error build that needs human triage.

## Selenium → Playwright (Later bucket item, reaffirmed)

**Status:** still deferred. The Selenium suite is already
quarantined (`[Explicit]` per Phase 1), so this is a port rather
than a critical-path item. A future session can port it without
blocking anything else — recommended priority: after the cassette
work above so a real regression suite exists at all.
