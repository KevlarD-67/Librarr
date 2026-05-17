using System.IO;
using NUnit.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    // Phase 9d reidentify regression. The roadmap target is:
    //
    //   Snapshot a 500-book Goodreads-ID-shaped library; run the
    //   reidentify wizard; assert match rate stays >= 85%.
    //
    // Three pieces are missing:
    //   1. A serialized library snapshot (Books + Editions + Authors,
    //      shaped like rows pre-Phase-5). Captured from a real
    //      production install under
    //      `src/NzbDrone.Core.Test/Files/Reidentify/library.json`.
    //   2. The matching OL cassettes (Phase 8b — see
    //      `src/NzbDrone.Core.Test/Files/OpenLibrary/README.md`)
    //      so the ISBN/ASIN/Title+Author lookups deterministically
    //      resolve without network.
    //   3. A test that wires those together: build an in-memory DB,
    //      load snapshot, swap OpenLibraryProxy for a cassette-backed
    //      stub, run the command, count matched rows.
    //
    // Until all three are present, this fixture is [Explicit] so it
    // doesn't break the unit suite. The skeleton below is the
    // intended shape — fill in once the snapshot is captured.
    [TestFixture]
    [Explicit("Requires a real 500-book library snapshot and OL cassettes — see fixture comment.")]
    public class ReidentifyRegressionFixture
    {
        private const string LibrarySnapshotPath = "Files/Reidentify/library.json";
        private const double TargetMatchRate = 0.85;

        [Test]
        public void should_match_at_least_85_percent_of_seed_library()
        {
            var snapshotPath = Path.Combine(TestContext.CurrentContext.TestDirectory, LibrarySnapshotPath);
            if (!File.Exists(snapshotPath))
            {
                Assert.Ignore($"Seed library not committed yet at {snapshotPath}");
            }

            // 1. Deserialize snapshot into in-memory Book + Edition + Author rows.
            // 2. Wire IBookIdMappingRepository + IBookService + IAuthorService
            //    + IEditionService against the in-memory state.
            // 3. Replace OpenLibraryProxy with a stub that resolves
            //    SearchByIsbn / SearchByAsin / SearchForNewBook from cassettes.
            // 4. Run ReidentifyService.Execute(new ReidentifyLibraryCommand()).
            // 5. Count rows in the mapping repo with Confidence >= 0.70.
            // 6. Assert count / total >= TargetMatchRate.
            Assert.Fail("Regression harness not yet implemented; see TODO above.");
        }
    }
}
