using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibrarySearchMapper
    {
        // Map each search doc to a slim Book record. The search response
        // doesn't include enough to populate Editions properly; downstream
        // code is expected to call GetBookInfo(work_key) for fuller data.
        public static List<Book> ReRankAndMap(OpenLibrarySearchResource resource, string queryTitle, string queryAuthor)
        {
            if (resource?.Docs == null || resource.Docs.Count == 0)
            {
                return new List<Book>();
            }

            var ranked = resource.Docs
                .Select(d => new { Doc = d, Score = Score(d, queryTitle, queryAuthor) })
                .OrderByDescending(x => x.Score)
                .Take(20);

            return ranked.Select(x => ToBook(x.Doc)).ToList();
        }

        public static Author ToAuthorSummary(OpenLibraryAuthorSearchDoc doc)
        {
            var key = ExtractKey(doc.Key);

            // OL's /search/authors.json response doesn't include the
            // photo id, but covers.openlibrary.org accepts olid-keyed
            // photos. Use that so the search-result tile shows the
            // author photo immediately, without waiting for the full
            // /authors/{key}.json hydration on add. `?default=false`
            // (set in OpenLibraryCoverUrls) means missing photos 404
            // so the frontend falls back to its own placeholder.
            var author = new Author
            {
                Metadata = new AuthorMetadata
                {
                    ForeignAuthorId = key,
                    TitleSlug = key,
                    Name = doc.Name,
                    Born = OpenLibraryDateParser.Parse(doc.BirthDate),
                    Images = OpenLibraryCoverUrls.ForAuthorByOlid(key)
                },
                CleanName = Parser.Parser.CleanAuthorName(doc.Name)
            };

            return author;
        }

        private static Book ToBook(OpenLibrarySearchDoc doc)
        {
            var key = ExtractKey(doc.Key);
            var isbn13 = doc.Isbn?.FirstOrDefault(i => i?.Length == 13);

            // OL's search response includes author_key + author_name as
            // parallel arrays (the work can have multiple co-authors).
            // The AddBook validator's `RuleFor(b => b.Author.ForeignAuthorId).NotEmpty()`
            // 400s when Author isn't populated, which silently surfaces
            // in the UI as "'Author. Foreign Author Id' must not be empty."
            // Pick the first author key/name pair; co-authors are out of
            // scope for the search-result summary (the full Author refresh
            // re-fetches via /authors/{key}.json anyway).
            var authorKey = doc.AuthorKey?.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k));
            var authorName = doc.AuthorName?.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

            var book = new Book
            {
                ForeignBookId = key,
                TitleSlug = key,
                Title = doc.Title,
                CleanTitle = Parser.Parser.CleanAuthorName(doc.Title),
                ReleaseDate = doc.FirstPublishYear.HasValue
                    ? new System.DateTime(doc.FirstPublishYear.Value, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                    : (System.DateTime?)null,
                AnyEditionOk = true,
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        ForeignEditionId = key,
                        TitleSlug = key,
                        Title = doc.Title,
                        Isbn13 = isbn13,
                        Monitored = true,

                        // Cover-resolution chain (best → fallback):
                        //   1. cover_i — deterministic primary cover id from
                        //      OL's search index. Always works when set.
                        //   2. ISBN-keyed — `/b/isbn/<isbn13>` works for many
                        //      books that have an edition cover but no
                        //      work-level cover indexed. Verified against
                        //      live OL: canonical Sanderson ISBNs all 302
                        //      to a real image even when cover_i is missing
                        //      from the search doc.
                        //   3. olid-keyed — the rare edge case where work
                        //      has `covers` set but search response omits
                        //      cover_i (search reindex race). Preserved
                        //      from the earlier fix; cheap to keep.
                        // For works with no cover anywhere on OL (e.g.
                        // small-press samplers), all three 404 and the
                        // frontend falls back to its own placeholder via
                        // the 404-quiet path in MediaCoverProxy.GetImage.
                        Images =
                            doc.CoverI.HasValue
                                ? OpenLibraryCoverUrls.ForBook(new List<int> { doc.CoverI.Value })
                                : (!string.IsNullOrWhiteSpace(isbn13)
                                    ? OpenLibraryCoverUrls.ForBookByIsbn(isbn13)
                                    : OpenLibraryCoverUrls.ForBookByOlid(key))
                    }
                }
            };

            if (authorKey != null)
            {
                var authorMeta = new AuthorMetadata
                {
                    ForeignAuthorId = authorKey,
                    TitleSlug = authorKey,
                    Name = authorName ?? authorKey,
                };

                book.Author = new Author
                {
                    Metadata = authorMeta,
                    CleanName = Parser.Parser.CleanAuthorName(authorName ?? authorKey)
                };

                book.AuthorMetadata = authorMeta;
            }

            return book;
        }

        // Token-overlap re-rank. OL's relevance ranking is keyword-soup, so
        // a cheap exact-token boost meaningfully improves first-result quality.
        // TODO Phase 4: substitute Levenshtein from Parser/Parser.cs helpers.
        private static int Score(OpenLibrarySearchDoc doc, string queryTitle, string queryAuthor)
        {
            var score = 0;

            if (queryTitle.IsNotNullOrWhiteSpace() && doc.Title.IsNotNullOrWhiteSpace())
            {
                var queryTokens = Tokens(queryTitle);
                var titleTokens = Tokens(doc.Title);
                score += queryTokens.Intersect(titleTokens).Count() * 10;

                if (string.Equals(doc.Title.Trim(), queryTitle.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    score += 50;
                }
            }

            if (queryAuthor.IsNotNullOrWhiteSpace() && doc.AuthorName != null)
            {
                var queryAuthorLower = queryAuthor.ToLowerInvariant();
                if (doc.AuthorName.Any(a => a != null && a.ToLowerInvariant().Contains(queryAuthorLower)))
                {
                    score += 30;
                }
            }

            score += System.Math.Min(doc.EditionCount, 50);

            return score;
        }

        private static IEnumerable<string> Tokens(string s)
        {
            return (s ?? string.Empty)
                .ToLowerInvariant()
                .Split(new[] { ' ', '-', ':', ',', '.', '(', ')' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2);
        }

        private static string ExtractKey(string olKey)
        {
            if (olKey.IsNullOrWhiteSpace())
            {
                return olKey;
            }

            var slash = olKey.LastIndexOf('/');
            return slash >= 0 ? olKey.Substring(slash + 1) : olKey;
        }
    }
}
