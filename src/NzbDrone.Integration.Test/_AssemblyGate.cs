using System;
using NUnit.Framework;

namespace NzbDrone.Integration.Test
{
    // Assembly-wide gate for the integration suite. Every fixture here boots a
    // real Readarr instance and calls /author/lookup during setup.
    //
    // That used to hit api.bookinfo.club, retired upstream on 2025-06-27, and
    // this gate was written on the assumption that every test therefore failed
    // its setup. That is no longer true: the OpenLibrary cutover landed, and
    // AuthorEditorFixture has been confirmed passing against live OL. Several
    // fixtures still carry a stale
    // [Ignore("Waiting for metadata to be back again")] from that era — see
    // AuthorFixture, AuthorLookupFixture, CalendarFixture, BlocklistFixture.
    //
    // The gate stays because the reason it is useful survives the fix: these
    // tests need the network and a couple of minutes, so they should not run
    // on a bare `dotnet test`. Opt in with READARR_RUN_INTEGRATION=1.
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
