using System.Collections;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.MetadataSource.OpenLibrary.Fixtures;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // SearchByIsbn against the real /isbn/{isbn}.json payloads committed under
    // Files/OpenLibrary/, driven through the proxy rather than the mappers.
    // The cassette corpus already proves these payloads *deserialize and map*
    // (OpenLibraryCassetteFixture); this fixture pins what the ISBN route
    // promises on top of that, against JSON OL actually served:
    //
    //   1. Edition identity survives. /isbn/ resolves one ISBN to one edition,
    //      and the candidate carries that edition's own key — the property
    //      ReidentifyService's 0.95-confidence mapping rests on, and the reason
    //      this route exists instead of a search.json query (which returns
    //      work-level docs and whichever ISBN happens to be listed first).
    //      See the review on PR #4.
    //
    //      The *ISBN* does not always survive with it, which is the more
    //      interesting half. ToEdition reads `isbn_13` only, and two of the five
    //      captures — Dune and Sapiens — carry no `isbn_13` at all: OL answered
    //      with an edition record listing `isbn_10` alone, in both cases the
    //      checksum-equivalent of the ISBN-13 that was queried (0441172717 is
    //      9780441172719; 0062316095 is 9780062316097). So Edition.Isbn13 is
    //      null and the candidate reaches the matcher with no ISBN.
    //
    //      That is not cosmetic. DistanceCalculator:86-94 then takes the
    //      `isbn_missing` branch (weight 0.1) instead of `isbn` at distance 0
    //      (weight 10.0), and losing a 10.0-weight bucket from the denominator
    //      is what lets the author penalty dominate. Measured through
    //      ToBook -> ToCandidates' backfill -> BookDistance, holding the payload
    //      fixed and varying only Isbn13:
    //
    //        Foundation, authorless, isbn_13 present  0.1469   accept
    //        Foundation, authorless, isbn_13 cleared  0.2857   REJECT (gate 0.20)
    //        Foundation, named,      isbn_13 present  0.0047
    //        Foundation, named,      isbn_13 cleared  0.0179
    //
    //      Harmless once the author name resolves; decisive when it does not.
    //      The Isbn13-is-null expectations below therefore pin current
    //      behaviour, not desired behaviour — deriving ISBN-13 from `isbn_10`
    //      is a checksum, not a round trip.
    //
    //   2. Where the edition JSON carries an author key, the candidate reaches
    //      the caller with a resolved author *name* (issue #7: an authorless
    //      candidate burns 0.1875 of the 0.20 distance budget before anything
    //      else is scored).
    //
    //   3. Where it does not — and two of the five captured payloads don't,
    //      because OL frequently hangs authors on the work rather than the
    //      edition — the candidate is authorless and no author lookup is
    //      attempted. That is the documented boundary of the #7 fix, pinned
    //      here with real payloads so a future work-level fallback has a
    //      test to flip.
    //
    //      Sapiens is where (1) and (3) compound: no author key to resolve AND
    //      no isbn_13, measured at 0.5082 against the 0.20 gate. The #7 fix
    //      cannot reach it — there is no key to look a name up by — so it is
    //      the ISBN half that would have to carry it.
    //
    // The expectations are hardcoded rather than re-read from the JSON on
    // purpose: re-deriving them from the cassette would make the test a
    // tautology. If a re-captured cassette changes shape, the table is the
    // thing to update, consciously.
    [TestFixture]
    public class OpenLibraryIsbnSearchCassetteFixture : CoreTest<OpenLibraryProxy>
    {
        private int _authorCalls;

        [SetUp]
        public void Setup()
        {
            _authorCalls = 0;

            // The request builder is pure URL construction — use the real one so
            // the URLs the proxy asks for are the URLs under test.
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryEditionResource>(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest r) => new HttpResponse<OpenLibraryEditionResource>(
                    Response(r, OpenLibraryFixtureLoader.LoadJson(CassetteForEditionUrl(r.Url.ToString())), HttpStatusCode.OK)));

            // Author lookups are served from the real author cassettes, keyed
            // off the requested OLID. A key we have no cassette for is a 404 —
            // not a 5xx, which would drag the proxy's 2s/4s/8s retry backoff
            // into the test run.
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryAuthorResource>(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest r) =>
                {
                    _authorCalls++;

                    var cassette = CassetteForAuthorUrl(r.Url.ToString());

                    return cassette == null
                        ? new HttpResponse<OpenLibraryAuthorResource>(Response(r, null, HttpStatusCode.NotFound))
                        : new HttpResponse<OpenLibraryAuthorResource>(Response(r, OpenLibraryFixtureLoader.LoadJson(cassette), HttpStatusCode.OK));
                });
        }

        // ── the corpus, as captured ─────────────────────────────────
        // fileName, queried ISBN, edition key, work key, Isbn13 as OL returned it
        public static IEnumerable IdentityCases()
        {
            yield return new TestCaseData("isbn_1984_9780451524935.json", "9780451524935", "OL34854896M", "OL1168083W", "9780451524935").SetName("{m}(1984)");

            // Dune and Sapiens are the isbn_10-only captures: OL answered with
            // an edition carrying no `isbn_13`, so ToEdition — which reads
            // `isbn_13` alone — yields a null Isbn13 even though the queried
            // ISBN is present in the payload as its ISBN-10 equivalent. The
            // expected null is current behaviour, not desired behaviour; see
            // the measured cost in the fixture header.
            yield return new TestCaseData("isbn_dune_9780441172719.json", "9780441172719", "OL22597282M", "OL893415W", null).SetName("{m}(dune)");
            yield return new TestCaseData("isbn_foundation_9780553293357.json", "9780553293357", "OL7825249M", "OL46125W", "9780553293357").SetName("{m}(foundation)");
            yield return new TestCaseData("isbn_hobbit_9780261103573.json", "9780261103573", "OL10236417M", "OL27513W", "9780261103573").SetName("{m}(fellowship)");
            yield return new TestCaseData("isbn_sapiens_9780062316097.json", "9780062316097", "OL27000666M", "OL17075811W", null).SetName("{m}(sapiens)");
        }

        [TestCaseSource(nameof(IdentityCases))]
        public void Isbn_cassette_should_map_to_the_edition_ol_resolved(string fileName, string isbn, string editionKey, string workKey, string isbn13)
        {
            var books = Subject.SearchByIsbn(isbn);

            books.Should().HaveCount(1, "/isbn/ resolves one ISBN to one edition");

            var book = books[0];
            book.ForeignEditionId.Should().Be(editionKey);
            book.ForeignBookId.Should().Be(workKey, "the slim book wraps the edition's work");

            var edition = book.Editions.Value.Should().ContainSingle().Subject;
            edition.ForeignEditionId.Should().Be(editionKey);
            edition.Isbn13.Should().Be(isbn13);
        }

        public static IEnumerable AuthorBearingCases()
        {
            yield return new TestCaseData("9780441172719", "OL79034A", "Frank Herbert").SetName("{m}(dune)");
            yield return new TestCaseData("9780553293357", "OL34221A", "Isaac Asimov").SetName("{m}(foundation)");
            yield return new TestCaseData("9780261103573", "OL26320A", "J.R.R. Tolkien").SetName("{m}(fellowship)");
        }

        [TestCaseSource(nameof(AuthorBearingCases))]
        public void Isbn_cassette_with_an_author_key_should_reach_the_caller_named(string isbn, string authorId, string authorName)
        {
            var books = Subject.SearchByIsbn(isbn);

            var metadata = books[0].AuthorMetadata.Value;
            metadata.ForeignAuthorId.Should().Be(authorId);
            metadata.Name.Should().Be(authorName, "an authorless candidate spends 0.1875 of the 0.20 distance budget on nothing");

            books[0].Author.Value.CleanName.Should().NotBeNullOrEmpty("DistanceCalculator compares clean names");
            _authorCalls.Should().Be(1);
        }

        // 1984 and Sapiens really came back from OL with no edition-level
        // `authors` — the author hangs on the work. The #7 fix deliberately
        // does not chase that (it would be a second lookup per candidate for
        // a case the edition data can't see), so these candidates stay
        // authorless and score accordingly. If a work-level fallback ever
        // lands, these are the cases that should start carrying a name.
        [TestCase("9780451524935", TestName = "{m}(1984)")]
        [TestCase("9780062316097", TestName = "{m}(sapiens)")]
        public void Isbn_cassette_without_edition_level_authors_should_skip_the_author_lookup(string isbn)
        {
            var books = Subject.SearchByIsbn(isbn);

            books.Should().HaveCount(1, "no author is still a usable candidate");
            books[0].AuthorMetadata.Should().BeNull("OL's edition JSON carried no author key to resolve");
            _authorCalls.Should().Be(0, "there is nothing to look up");
        }

        // ── helpers ─────────────────────────────────────────────────
        private static HttpResponse Response(HttpRequest request, string json, HttpStatusCode status)
        {
            return new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes(json ?? string.Empty), status);
        }

        private static string CassetteForEditionUrl(string url)
        {
            foreach (TestCaseData c in IdentityCases())
            {
                var fileName = (string)c.Arguments[0];
                var isbn = (string)c.Arguments[1];

                if (url.Contains($"isbn/{isbn}.json"))
                {
                    return fileName;
                }
            }

            throw new AssertionException($"Unexpected edition request: {url}");
        }

        private static string CassetteForAuthorUrl(string url)
        {
            if (url.Contains("authors/OL79034A.json"))
            {
                return "author_frank_herbert.json";
            }

            if (url.Contains("authors/OL34221A.json"))
            {
                return "author_asimov.json";
            }

            if (url.Contains("authors/OL26320A.json"))
            {
                return "author_tolkien.json";
            }

            return null;
        }
    }
}
