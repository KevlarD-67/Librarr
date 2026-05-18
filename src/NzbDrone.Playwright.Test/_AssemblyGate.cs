using System;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // Assembly-wide gate for the Playwright smoke suite. Every fixture here
    // boots a real Librarr instance via NzbDroneRunner and drives a headless
    // Chromium through Playwright. The default `dotnet test` invocation in
    // a dev sandbox doesn't have a built binary on hand and may not have
    // Playwright's browser bundle installed, so we don't fail it by
    // default — opt in by setting READARR_RUN_PLAYWRIGHT=1.
    //
    // First-run setup: see src/NzbDrone.Playwright.Test/README.md for the
    // `playwright install` step (downloads ~250 MB of browser binaries
    // into ~/.cache/ms-playwright on Linux/macOS or
    // %LOCALAPPDATA%\ms-playwright on Windows).
    [SetUpFixture]
    public class AssemblyGate
    {
        [OneTimeSetUp]
        public void GateAssembly()
        {
            if (Environment.GetEnvironmentVariable("READARR_RUN_PLAYWRIGHT") != "1")
            {
                Assert.Ignore(
                    "Playwright smoke suite is opt-in; set READARR_RUN_PLAYWRIGHT=1 to run. " +
                    "Requires a built backend on disk and `playwright install` to have run once — see README.md.");
            }
        }
    }
}
