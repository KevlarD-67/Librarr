using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    // Migration-043 service tests. Three axes:
    //   1. Idempotent overwrite — DeleteByEditionId always fires before insert.
    //   2. Dedup by CleanName (case-insensitive) — duplicate aliases collapse.
    //   3. Existing Narrator rows are reused (no duplicate inserts).
    [TestFixture]
    public class NarratorServiceFixture : CoreTest<NarratorService>
    {
        // StyleCop SA1107 forbids the obvious `n => { n.Id = …; return n; }`
        // lambda body on one line. Pre-baking it as a delegate keeps each
        // Setup readable without splitting the brace block per call site.
        private static System.Func<Narrator, Narrator> InsertWithId(int id) =>
            n =>
            {
                n.Id = id;
                return n;
            };

        [Test]
        public void SetNarratorsForEdition_should_skip_when_edition_id_is_zero()
        {
            Subject.SetNarratorsForEdition(0, new[] { "George Guidall" });

            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.DeleteByEditionId(It.IsAny<int>()), Times.Never);
            Mocker.GetMock<INarratorRepository>()
                .Verify(r => r.Insert(It.IsAny<Narrator>()), Times.Never);
        }

        [Test]
        public void SetNarratorsForEdition_should_always_clear_join_rows_first()
        {
            // Even when the incoming name list is empty, the wipe still
            // happens — that's how the "no narrators on this edition any
            // more" case propagates.
            Subject.SetNarratorsForEdition(42, new List<string>());

            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.DeleteByEditionId(42), Times.Once);
            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.IsAny<IList<EditionNarrator>>()), Times.Never);
        }

        [Test]
        public void SetNarratorsForEdition_should_insert_new_narrator_when_clean_name_missing()
        {
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.FindByCleanName(It.IsAny<string>()))
                .Returns((Narrator)null);
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.Insert(It.IsAny<Narrator>()))
                .Returns<Narrator>(InsertWithId(100));

            Subject.SetNarratorsForEdition(7, new[] { "George Guidall" });

            Mocker.GetMock<INarratorRepository>()
                .Verify(r => r.Insert(It.Is<Narrator>(n => n.Name == "George Guidall")), Times.Once);
            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.Is<IList<EditionNarrator>>(rows =>
                    rows.Count == 1 &&
                    rows[0].EditionId == 7 &&
                    rows[0].NarratorId == 100 &&
                    rows[0].Order == 0)), Times.Once);
        }

        [Test]
        public void SetNarratorsForEdition_should_reuse_existing_narrator_when_clean_name_matches()
        {
            // Existing narrator in the DB; new edition references it.
            // Should not call Insert on the narrator repo.
            var existing = new Narrator { Id = 5, Name = "George Guidall", CleanName = "georgeguidall" };
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.FindByCleanName(It.IsAny<string>()))
                .Returns(existing);

            Subject.SetNarratorsForEdition(8, new[] { "George Guidall" });

            Mocker.GetMock<INarratorRepository>()
                .Verify(r => r.Insert(It.IsAny<Narrator>()), Times.Never);
            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.Is<IList<EditionNarrator>>(rows =>
                    rows.Count == 1 && rows[0].NarratorId == 5)), Times.Once);
        }

        [Test]
        public void SetNarratorsForEdition_should_dedup_by_clean_name_case_insensitive()
        {
            // "George Guidall" and "george guidall" should collapse to one row.
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.FindByCleanName(It.IsAny<string>()))
                .Returns((Narrator)null);
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.Insert(It.IsAny<Narrator>()))
                .Returns<Narrator>(InsertWithId(100));

            Subject.SetNarratorsForEdition(9, new[] { "George Guidall", "george guidall" });

            // Exactly one Narrator row, exactly one join row.
            Mocker.GetMock<INarratorRepository>()
                .Verify(r => r.Insert(It.IsAny<Narrator>()), Times.Once);
            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.Is<IList<EditionNarrator>>(rows => rows.Count == 1)), Times.Once);
        }

        [Test]
        public void SetNarratorsForEdition_should_skip_null_and_blank_names()
        {
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.FindByCleanName(It.IsAny<string>()))
                .Returns((Narrator)null);
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.Insert(It.IsAny<Narrator>()))
                .Returns<Narrator>(InsertWithId(100));

            Subject.SetNarratorsForEdition(10, new[] { "Real Person", null, "  ", string.Empty });

            Mocker.GetMock<INarratorRepository>()
                .Verify(r => r.Insert(It.Is<Narrator>(n => n.Name == "Real Person")), Times.Once);
            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.Is<IList<EditionNarrator>>(rows => rows.Count == 1)), Times.Once);
        }

        [Test]
        public void SetNarratorsForEdition_should_preserve_order_for_multi_cast()
        {
            // Dual-cast title — the Order column matters for credited billing.
            // Both Insert calls return id=100; the test asserts on Order, not
            // NarratorId, so distinct ids aren't required here.
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.FindByCleanName(It.IsAny<string>()))
                .Returns((Narrator)null);
            Mocker.GetMock<INarratorRepository>()
                .Setup(r => r.Insert(It.IsAny<Narrator>()))
                .Returns<Narrator>(InsertWithId(100));

            Subject.SetNarratorsForEdition(11, new[] { "George Guidall", "Frank Muller" });

            Mocker.GetMock<IEditionNarratorRepository>()
                .Verify(r => r.InsertMany(It.Is<IList<EditionNarrator>>(rows =>
                    rows.Count == 2 &&
                    rows[0].Order == 0 &&
                    rows[1].Order == 1)), Times.Once);
        }
    }
}
