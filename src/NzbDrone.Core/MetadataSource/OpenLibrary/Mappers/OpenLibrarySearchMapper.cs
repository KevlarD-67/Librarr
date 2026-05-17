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

            var author = new Author
            {
                Metadata = new AuthorMetadata
                {
                    ForeignAuthorId = key,
                    TitleSlug = key,
                    Name = doc.Name,
                    Born = OpenLibraryDateParser.Parse(doc.BirthDate),
                },
                CleanName = Parser.Parser.CleanAuthorName(doc.Name)
            };

            return author;
        }

        private static Book ToBook(OpenLibrarySearchDoc doc)
        {
            var key = ExtractKey(doc.Key);
            var isbn13 = doc.Isbn?.FirstOrDefault(i => i?.Length == 13);

            return new Book
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
                        Monitored = true
                    }
                }
            };
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
