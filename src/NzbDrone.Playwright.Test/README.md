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
   `_output/net6.0/`.

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

## Known blocker: the browser bundle can no longer be downloaded

**As of 2026-07-30 this suite cannot actually be run**, on any machine,
for a reason that has nothing to do with the tests. `Microsoft.Playwright`
is pinned at **1.40.0** (November 2023), whose driver asks the CDN for
`chromium-1091`. That build is no longer served — the request hangs
indefinitely rather than 404ing, which is why the symptom looks like a
slow download instead of a missing artifact.

Bumping the pin is the fix, but it is not a one-line change, because
the 1.5x packages ship a node driver newer than their own .NET pin:

| `Microsoft.Playwright` | .NET lib launches | bundled driver installs |
|---|---|---|
| 1.54.0 | `chromium_headless_shell-1181` | 1187 |
| 1.55.0 | `chromium_headless_shell-1187` | 1194 |

So `install` followed by `dotnet test` mismatches by one revision on
both. Pairing lib 1.55.0 with the 1.54.0 driver lines both up on 1187 —
but `chromium-headless-shell` specifically stalls on download where the
full `chromium` build fetches in seconds, so that pairing is unverified.
Whoever picks this up should try a current release (1.6x) first and
confirm `PlaywrightTestBase` still compiles against it; the API surface
this suite uses is small and stable.

`scripts/playwright-install.sh` has been fixed separately — it used to
look under `src/NzbDrone.Playwright.Test/bin`, which this repo never
writes to (`Directory.Build.props` redirects output to `_tests/`), and
it required PowerShell to be installed. It now finds the CLI under
`_tests/` and drives Playwright's own bundled Node directly. That fix is
necessary but not sufficient: the script runs correctly and still cannot
fetch 1.40.0's browsers.

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
