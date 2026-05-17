using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // Phase 4b: cover URL builder. OL's bibliographic JSON returns
    // bare integer cover IDs; the actual image URL is at
    // covers.openlibrary.org/{b,a}/id/{id}-L.jpg. Negative IDs are OL
    // sentinels meaning "no cover" and must be dropped.
    [TestFixture]
    public class OpenLibraryCoverUrlsFixture
    {
        [Test]
        public void ForBook_should_emit_large_book_cover_url_per_id()
        {
            var covers = OpenLibraryCoverUrls.ForBook(new List<int> { 12345 });

            covers.Should().HaveCount(1);
            covers[0].Url.Should().Be("https://covers.openlibrary.org/b/id/12345-L.jpg");
            covers[0].CoverType.Should().Be(MediaCoverTypes.Cover);
        }

        [Test]
        public void ForAuthor_should_emit_author_photo_url_per_id()
        {
            var covers = OpenLibraryCoverUrls.ForAuthor(new List<int> { 7777 });

            covers.Should().HaveCount(1);
            covers[0].Url.Should().Be("https://covers.openlibrary.org/a/id/7777-L.jpg");
            covers[0].CoverType.Should().Be(MediaCoverTypes.Poster);
        }

        [Test]
        public void ForBook_should_drop_negative_sentinel_ids()
        {
            var covers = OpenLibraryCoverUrls.ForBook(new List<int> { -1, 100, -5 });

            covers.Should().HaveCount(1);
            covers[0].Url.Should().EndWith("/100-L.jpg");
        }

        [Test]
        public void ForBook_should_deduplicate_ids()
        {
            var covers = OpenLibraryCoverUrls.ForBook(new List<int> { 42, 42, 42 });

            covers.Should().HaveCount(1);
        }

        [Test]
        public void ForBook_should_return_empty_list_when_input_is_null()
        {
            var covers = OpenLibraryCoverUrls.ForBook(null);

            covers.Should().BeEmpty();
        }

        [Test]
        public void ForAuthor_should_return_empty_list_when_input_is_null()
        {
            var covers = OpenLibraryCoverUrls.ForAuthor(null);

            covers.Should().BeEmpty();
        }
    }
}
