using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Books;

namespace NzbDrone.Api.Test
{
    // Post-44 narrator read-path tests. The legacy Editions.Narrators
    // string column is gone; the mapper sources the API string solely
    // from the normalized Narrators / EditionNarrators join.
    [TestFixture]
    public class EditionResourceMapperFixture : TestBase
    {
        [Test]
        public void Should_project_narrators_string_from_join_when_populated()
        {
            var edition = new Edition
            {
                Id = 7,
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
        public void Should_return_null_when_join_is_empty()
        {
            var edition = new Edition
            {
                Id = 8,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>())
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().BeNull();
        }

        [Test]
        public void Should_return_null_when_lazy_load_is_null()
        {
            // Edge case from tests / unit setup where NarratorList isn't
            // initialized at all; mapper must not throw.
            var edition = new Edition
            {
                Id = 9,
                NarratorList = null
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().BeNull();
        }
    }
}
