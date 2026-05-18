#!/usr/bin/env bash
# playwright-install.sh
#
# One-shot bootstrap for the Readarr.Playwright.Test browser bundle.
# Restores NuGet packages so the bundled Playwright CLI is on disk,
# then invokes it to download Chromium.
#
# Idempotent — safe to re-run. Cached at ~/.cache/ms-playwright/
# (Linux) or ~/Library/Caches/ms-playwright/ (macOS).

set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/src/NzbDrone.Playwright.Test/Readarr.Playwright.Test.csproj"

if [[ ! -f "$project" ]]; then
    echo "error: Playwright test project not found at $project" >&2
    exit 1
fi

case "$(uname -s)" in
    Linux|Darwin) ;;
    *) echo "error: this script targets Linux/macOS. On Windows, use scripts/playwright-install.ps1." >&2
       exit 1 ;;
esac

echo "Restoring Playwright test project so the CLI is on disk..."
dotnet restore "$project"

echo "Locating the Playwright entry point..."
playwright_dll=$(find "$repo_root/src/NzbDrone.Playwright.Test/bin" -name "playwright.ps1" -print -quit 2>/dev/null || true)
if [[ -z "$playwright_dll" ]]; then
    # The .ps1 wrapper isn't generated until at least one build runs.
    echo "Triggering a build so Playwright drops its CLI wrapper..."
    dotnet build "$project" -nologo --verbosity quiet
    playwright_dll=$(find "$repo_root/src/NzbDrone.Playwright.Test/bin" -name "playwright.ps1" -print -quit 2>/dev/null || true)
fi

if [[ -z "$playwright_dll" ]]; then
    echo "error: playwright.ps1 still not found under bin/. Inspect the build output." >&2
    exit 1
fi

playwright_dir=$(dirname "$playwright_dll")

echo "Installing Chromium..."
pwsh -NoProfile -ExecutionPolicy Bypass -File "$playwright_dir/playwright.ps1" install chromium

echo
echo "Done. You can now run:"
echo "  READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/"
