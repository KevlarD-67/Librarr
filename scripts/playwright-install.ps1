<#
.SYNOPSIS
    One-shot bootstrap for the Readarr.Playwright.Test browser bundle.

.DESCRIPTION
    Restores NuGet packages so the bundled Playwright CLI is on disk,
    then invokes it to download Chromium. Idempotent — safe to re-run.
    Cached at %LOCALAPPDATA%\ms-playwright on Windows.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project  = Join-Path $repoRoot 'src\NzbDrone.Playwright.Test\Readarr.Playwright.Test.csproj'

if (-not (Test-Path -LiteralPath $project)) {
    Write-Error "Playwright test project not found at $project"
    exit 1
}

Write-Host 'Restoring Playwright test project so the CLI is on disk...'
dotnet restore $project

$binDir = Join-Path $repoRoot 'src\NzbDrone.Playwright.Test\bin'
$playwright = Get-ChildItem -LiteralPath $binDir -Filter 'playwright.ps1' -Recurse -File -ErrorAction SilentlyContinue |
              Select-Object -First 1

if (-not $playwright) {
    # The .ps1 wrapper isn't generated until at least one build runs.
    Write-Host 'Triggering a build so Playwright drops its CLI wrapper...'
    dotnet build $project -nologo --verbosity quiet
    $playwright = Get-ChildItem -LiteralPath $binDir -Filter 'playwright.ps1' -Recurse -File -ErrorAction SilentlyContinue |
                  Select-Object -First 1
}

if (-not $playwright) {
    Write-Error 'playwright.ps1 still not found under bin/. Inspect the build output.'
    exit 1
}

Write-Host 'Installing Chromium...'
& $playwright.FullName install chromium

Write-Host ''
Write-Host 'Done. You can now run:'
Write-Host '  $env:READARR_RUN_PLAYWRIGHT = "1"; dotnet test src\NzbDrone.Playwright.Test\'
