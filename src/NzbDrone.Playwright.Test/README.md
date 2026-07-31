# Readarr.Playwright.Test

End-to-end smoke suite, replacing the gated legacy Selenium suite
(`src/NzbDrone.Automation.Test/`, retired but kept for git blame).

Scope today: six page-load smokes for Library / Calendar / Activity /
Wanted / System / Add Author. Each test mounts the page and asserts a
page-specific DOM anchor exists. Visual regression / interaction
testing is not in scope; that's gated on the OL cassette work in the
roadmap.

## Prerequisites

1. **Built backend on disk.** Run `./build.sh --backend` once.
   `NzbDroneRunner` invokes the most-recently-built output under
   `_output/net10.0/`.

2. **Built frontend on disk.** Run `yarn install && yarn build`
   once. The runner serves `_output/UI/` from disk; without the
   build step, every page test will see a blank document.

3. **Playwright browser bundle.** Run the helper once per machine:

   ```bash
   # Linux / macOS
   ./scripts/playwright-install.sh
   ```

   ```powershell
   # Windows
   .\scripts\playwright-install.ps1
   ```

   The script restores the NuGet packages and then invokes the
   bundled `playwright install` tool to download Chromium into
   `~/.cache/ms-playwright/` (Linux/macOS) or
   `%LOCALAPPDATA%\ms-playwright\` (Windows). ~250 MB on first
   run; subsequent runs are cached.

## Running

The suite is gated by `READARR_RUN_PLAYWRIGHT=1` (see
`_AssemblyGate.cs`). Without that env var, every test reports
`Skipped` — the default `dotnet test src/Readarr.sln` invocation
stays green.

```bash
# All seven smokes
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/

# Single test (mirrors the Selenium suite filter syntax)
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/ \
  --filter "FullyQualifiedName~MainPagesTest.author_page"
```

Screenshots land in the working directory as
`{test_name}_test_screenshot.png`. They're per-run artefacts —
`.gitignore` excludes them from the repo.

## First-run friction

The suite runs green on the pinned `Microsoft.Playwright` **1.40.0**;
verified 2026-07-30, eight tests, four consecutive clean runs.

The one thing that will make you think it's broken: 1.40.0 asks the CDN
for `chromium-1091`, a build from late 2023, and that download is
*extremely* slow — tens of minutes, with no progress output for long
stretches. It does complete. Don't interrupt it, and don't conclude the
artifact is gone; a partially-downloaded browser directory looks like a
present-but-broken install and produces
`Executable doesn't exist at .../chromium-1091/...` on the next run.
If that happens, delete the directory under `~/Library/Caches/ms-playwright`
(macOS) or `~/.cache/ms-playwright` (Linux) and re-run the installer.

If you do bump the pin, be aware the 1.5x packages ship a node driver
one revision ahead of their own .NET assembly, so `install` and
`dotnet test` disagree about which browser to use:

| `Microsoft.Playwright` | .NET lib launches | bundled driver installs |
|---|---|---|
| 1.54.0 | `chromium_headless_shell-1181` | 1187 |
| 1.55.0 | `chromium_headless_shell-1187` | 1194 |

Check both before assuming a bump worked.

`scripts/playwright-install.sh` used to look under
`src/NzbDrone.Playwright.Test/bin`, which this repo never writes to
(`Directory.Build.props` redirects output to `_tests/`), and it required
PowerShell. It now finds the CLI under `_tests/` and drives Playwright's
own bundled Node directly.

## One instance per assembly

The browser and the Librarr process are owned by `AssemblyGate`, not by
`PlaywrightTestBase`, and that is deliberate. `NzbDroneRunner.KillAll()`
kills every Readarr process by name rather than only its own, and every
fixture wants port 8787 — so with a per-fixture lifecycle, one fixture's
teardown shot down another fixture's instance and the suite failed
intermittently with `TargetClosedException` raised from `OneTimeSetUp`.
Booting once per assembly removes the race by construction, and takes
the suite from ~35s to ~3s.

The consequence to keep in mind when adding tests: **every test shares
one page and one database.** Tests must navigate to where they need to
be rather than assuming a starting location, and must not depend on the
library being empty.

## Why a separate project (not extending Automation.Test)

* Selenium 3.141 + ChromeDriver 91 in the legacy project literally
  won't load on a 2026-era Chrome — the package versions are pinned
  there for git-archaeology purposes, not because they work.
* Playwright's NUnit integration is its own package
  (`Microsoft.Playwright.NUnit`) and pulls a different test SDK
  chain; collocating them risks DI conflicts.
* The legacy assembly gate (`READARR_RUN_AUTOMATION=1`) lets a
  forensic-minded user still exercise the Selenium harness if they
  want; this project's gate (`READARR_RUN_PLAYWRIGHT=1`) is the
  forward path.

## What this does NOT cover yet

* Login-required flows (the smokes hit the bootstrap state where
  authentication is off by default).
* Asserting page contents — only mount-time existence of an anchor
  element. Visual diffs need a cassette / golden-image strategy
  that's still ahead of us on the roadmap.
* Cross-browser. Chromium only; Firefox + WebKit launchers are
  Playwright one-liners but not wired in until they're needed.
