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

# Build output does NOT land under the project's own bin/ — Directory.Build.props
# redirects every test project to _tests/. Search there, and keep bin/ as a
# fallback for anyone building with those props overridden.
find_cli() {
    find "$repo_root/_tests" "$repo_root/src/NzbDrone.Playwright.Test/bin" \
         -path "*/.playwright/package/cli.js" -print -quit 2>/dev/null || true
}

echo "Locating the Playwright entry point..."
playwright_cli=$(find_cli)
if [[ -z "$playwright_cli" ]]; then
    # The bundle isn't laid down until at least one build runs.
    echo "Triggering a build so Playwright drops its CLI..."
    dotnet build "$project" -nologo --verbosity quiet
    playwright_cli=$(find_cli)
fi

if [[ -z "$playwright_cli" ]]; then
    echo "error: .playwright/package/cli.js not found under _tests/ or bin/. Inspect the build output." >&2
    exit 1
fi

# Drive the Node CLI directly rather than the playwright.ps1 wrapper: that
# wrapper needs PowerShell, which is not a reasonable thing to require on a
# Linux or macOS dev box. Playwright ships its own Node alongside the CLI, so
# prefer that and fall back to whatever node is on PATH.
bundled_node=$(find "$(dirname "$playwright_cli")/../node" -maxdepth 2 -name node -type f -print -quit 2>/dev/null || true)
node_bin="${bundled_node:-$(command -v node || true)}"

if [[ -z "$node_bin" ]]; then
    echo "error: no node binary found (neither bundled nor on PATH)." >&2
    exit 1
fi

echo "Installing Chromium..."
"$node_bin" "$playwright_cli" install chromium

echo
echo "Done. You can now run:"
echo "  READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/"
