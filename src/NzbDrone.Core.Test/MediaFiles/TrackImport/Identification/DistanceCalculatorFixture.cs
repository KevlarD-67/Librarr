using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles.BookImport.Identification;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.BookImport.Identification
{
    [TestFixture]
    public class DistanceCalculatorFixture : TestBase
    {
        [Test]
        public void should_reverse_single_reversed_author()
        {
            var input = new List<string> { "Last, First" };
            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().Contain("First Last");
        }

        [Test]
        public void should_reverse_two_reversed_author()
        {
            var input = new List<string>
            {
                "Last, First",
                "Last2, First2"
            };

            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().HaveCount(4);
            authors.Should().Contain("First Last");
            authors.Should().Contain("First2 Last2");
            authors.Should().Contain("Last, First");
            authors.Should().Contain("Last2, First2");
        }

        [Test]
        public void should_not_reverse_single_author()
        {
            var input = new List<string> { "First Last" };
            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().HaveCount(1);
            authors.Should().Contain("First Last");
        }

        [TestCase("First1 Last1, First2 Last2", "First1 Last1", "First2 Last2")]
        [TestCase("First1 Last1; First2 Last2", "First1 Last1", "First2 Last2")]
        [TestCase("First1 Last1 & First2 Last2", "First1 Last1", "First2 Last2")]
        [TestCase("First1 Last1 / First2 Last2", "First1 Last1", "First2 Last2")]
        [TestCase("First1 Last1 and First2 Last2", "First1 Last1", "First2 Last2")]
        public void should_split_concatenated_author(string inputString, string first, string second)
        {
            var input = new List<string> { inputString };
            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().Contain(inputString);
            authors.Should().Contain(first);
            authors.Should().Contain(second);
            authors.Should().HaveCount(3);
        }

        [Test]
        public void should_split_concatenated_with_trailing_and()
        {
            var inputString = "First Last, First2 Last2 & First3 Last3";
            var input = new List<string> { inputString };
            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().Contain(inputString);
            authors.Should().Contain("First Last");
            authors.Should().Contain("First2 Last2");
            authors.Should().Contain("First3 Last3");
            authors.Should().HaveCount(4);
        }

        [Test]
        public void should_not_split_if_multiple_input()
        {
            var input = new List<string>
            {
                "First Last",
                "Second Third, Fourth Fifth"
            };

            var authors = DistanceCalculator.GetAuthorVariants(input);

            authors.Should().HaveCount(2);
            authors.Should().Contain("First Last");
            authors.Should().Contain("Second Third, Fourth Fifth");
        }

        // Issue #7. An absent author name is scored as maximum author-distance
        // rather than "unknown", so on an otherwise-perfect ISBN match it costs
        // 0.1875 of the 0.20 budget in CloseAlbumMatchSpecification — leaving no
        // room for any other imperfection. OpenLibraryProxy now resolves the
        // name for ISBN-sourced candidates; this pins what that is worth, so the
        // margin cannot quietly come back.
        [Test]
        public void absent_author_name_should_cost_most_of_the_accept_budget()
        {
            const string Isbn = "9780345391803";

            double Score(string editionAuthorName)
            {
                var edition = new Edition
                {
                    Title = "Neuromancer",
                    Isbn13 = Isbn,
                    Book = new Book
                    {
                        Title = "Neuromancer",
                        AuthorMetadata = new AuthorMetadata { Name = editionAuthorName }
                    }
                };

                var localBooks = new List<LocalBook>
                {
                    new LocalBook
                    {
                        Path = "/books/neuromancer.epub",
                        FileTrackInfo = new ParsedTrackInfo
                        {
                            Authors = new List<string> { "William Gibson" },
                            BookTitle = "Neuromancer",
                            Isbn = Isbn
                        }
                    }
                };

                return DistanceCalculator.BookDistance(localBooks, edition).NormalizedDistance();
            }

            Score("William Gibson").Should().Be(0.0);
            Score(string.Empty).Should().BeApproximately(0.1875, 0.0001);
        }

        // A candidate mapped straight from a metadata source has not been
        // through a DB lazy load, so Book.AuthorMetadata can be null. Scoring
        // one must not take the whole import run down.
        [Test]
        public void should_score_candidate_with_no_author_metadata()
        {
            var edition = new Edition
            {
                Title = "Neuromancer",
                Book = new Book
                {
                    Title = "Neuromancer",
                    AuthorMetadata = null
                }
            };

            var localBooks = new List<LocalBook>
            {
                new LocalBook
                {
                    Path = "/books/neuromancer.epub",
                    FileTrackInfo = new ParsedTrackInfo
                    {
                        Authors = new List<string> { "William Gibson" },
                        BookTitle = "Neuromancer"
                    }
                }
            };

            var distance = DistanceCalculator.BookDistance(localBooks, edition);

            // An absent author is maximum author-distance, not a crash.
            distance.NormalizedDistance().Should().BeGreaterThan(0.0);
        }

        [Test]
        public void should_score_candidate_whose_book_is_not_attached()
        {
            var edition = new Edition
            {
                Title = "Neuromancer",
                Book = null
            };

            var localBooks = new List<LocalBook>
            {
                new LocalBook
                {
                    Path = "/books/neuromancer.epub",
                    FileTrackInfo = new ParsedTrackInfo
                    {
                        Authors = new List<string> { "William Gibson" },
                        BookTitle = "Neuromancer"
                    }
                }
            };

            var distance = DistanceCalculator.BookDistance(localBooks, edition);

            distance.NormalizedDistance().Should().BeGreaterThan(0.0);
        }
    }
}
