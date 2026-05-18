using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Books;

namespace NzbDrone.Api.Test
{
    // Phase-2 narrator read-path tests (post-43-and-43-wiring). The
    // mapper now prefers the normalized Narrators / EditionNarrators
    // join over the legacy Editions.Narrators string column. Three
    // axes:
    //   1. Join populated → resource serializes from it
    //   2. Join empty / null → fall back to the legacy string column
    //   3. The Order column matters when more than one narrator
    [TestFixture]
    public class EditionResourceMapperFixture : TestBase
    {
        [Test]
        public void Should_project_narrators_string_from_join_when_populated()
        {
            var edition = new Edition
            {
                Id = 7,
                Narrators = "Stale Cache From Column", // would be wrong; join wins
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>
                {
                    new Narrator { Name = "George Guidall" },
                    new Narrator { Name = "Frank Muller" }
                })
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().Be("George Guidall, Frank Muller");
        }

        [Test]
        public void Should_fall_back_to_string_column_when_join_is_empty()
        {
            var edition = new Edition
            {
                Id = 8,
                Narrators = "Pre-Migration Names",
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>())
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().Be("Pre-Migration Names");
        }

        [Test]
        public void Should_fall_back_to_string_column_when_lazy_load_is_null()
        {
            var edition = new Edition
            {
                Id = 9,
                Narrators = "Legacy Only",
                NarratorList = null
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().Be("Legacy Only");
        }

        [Test]
        public void Should_return_null_when_both_join_and_column_are_empty()
        {
            var edition = new Edition
            {
                Id = 10,
                Narrators = null,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>())
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().BeNull();
        }
    }
}
