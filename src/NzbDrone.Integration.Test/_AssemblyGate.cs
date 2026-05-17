using System;
using NUnit.Framework;

namespace NzbDrone.Integration.Test
{
    // Assembly-wide gate for the integration suite. Every fixture here boots
    // a real Readarr instance and calls /author/lookup during setup, which
    // hits api.bookinfo.club — retired upstream on 2025-06-27. Until the
    // suite is repointed at OpenLibrary or stubbed against recorded
    // cassettes, every test fails its setup. Run manually with
    // READARR_RUN_INTEGRATION=1 against a populated metadata source.
    [SetUpFixture]
    public class AssemblyGate
    {
        [OneTimeSetUp]
        public void GateAssembly()
        {
            if (Environment.GetEnvironmentVariable("READARR_RUN_INTEGRATION") != "1")
            {
                Assert.Ignore(
                    "Integration.Test depends on a reachable metadata source (api.bookinfo.club, now retired). " +
                    "Set READARR_RUN_INTEGRATION=1 to run against a populated upstream once OpenLibrary cutover lands.");
            }
        }
    }
}
