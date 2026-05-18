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
# All six smokes
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/

# Single test (mirrors the Selenium suite filter syntax)
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/ \
  --filter "FullyQualifiedName~MainPagesTest.author_page"
```

Screenshots land in the working directory as
`{test_name}_test_screenshot.png`. They're per-run artefacts —
`.gitignore` excludes them from the repo.

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
