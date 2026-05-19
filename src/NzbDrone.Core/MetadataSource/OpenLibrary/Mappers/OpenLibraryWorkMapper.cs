using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryWorkMapper
    {
        // Returns the assembled Book plus any AuthorMetadata entries inferred
        // from the work + editions. AuthorMetadata is intentionally slim here;
        // callers that need full author data fetch /authors/{key}.json separately.
        public static (Book Book, List<AuthorMetadata> Authors) ToBook(
            OpenLibraryWorkResource work,
            OpenLibraryEditionListResource editions)
        {
            var workKey = ExtractKey(work.Key);

            var primaryEdition = SelectPrimaryEdition(editions);
            var editionList = (editions?.Entries ?? new List<OpenLibraryEditionResource>())
                .Select(OpenLibraryEditionMapper.ToEdition)
                .ToList();

            // Mark the primary edition Monitored=true, all others false.
            if (editionList.Any())
            {
                var primaryKey = primaryEdition != null ? ExtractKey(primaryEdition.Key) : editionList.First().ForeignEditionId;
                foreach (var e in editionList)
                {
                    e.Monitored = e.ForeignEditionId == primaryKey;
                }

                // Work-level covers backfill any edition lacking its own.
                // Most editions have their own cover ID — this only fires
                // when OL returns sparse edition records (e.g. older
                // imports that were never updated with an edition cover).
                var workCovers = OpenLibraryCoverUrls.ForBook(work.Covers);
                if (workCovers.Count > 0)
                {
                    foreach (var e in editionList)
                    {
                        if (e.Images.Count == 0)
                        {
                            e.Images = workCovers;
                        }
                    }
                }
            }

            var book = new Book
            {
                ForeignBookId = workKey,
                ForeignEditionId = editionList.FirstOrDefault(e => e.Monitored)?.ForeignEditionId,
                TitleSlug = workKey,
                Title = work.Title,
                CleanTitle = Parser.Parser.CleanAuthorName(work.Title),
                ReleaseDate = OpenLibraryDateParser.Parse(work.FirstPublishDate) ?? primaryEdition?.PublishDate.AsNullableDate(),
                Genres = work.Subjects?.Take(10).ToList() ?? new List<string>(),
                AnyEditionOk = true,
                Editions = editionList
            };

            // Each author link is just "/authors/OL...A" — we don't have
            // full author records yet. Surface stub AuthorMetadata so the
            // Book.AuthorMetadataId join can resolve once /authors/{key}.json
            // is fetched on the per-author refresh path.
            var authors = (work.Authors ?? new List<OpenLibraryAuthorLink>())
                .Select(a => a.Author?.Key)
                .Where(k => k.IsNotNullOrWhiteSpace())
                .Select(k => new AuthorMetadata { ForeignAuthorId = ExtractKey(k), TitleSlug = ExtractKey(k) })
                .ToList();

            return (book, authors);
        }

        private static OpenLibraryEditionResource SelectPrimaryEdition(OpenLibraryEditionListResource editions)
        {
            if (editions?.Entries == null || editions.Entries.Count == 0)
            {
                return null;
            }

            // Preference order: English + ISBN-13 + cover → English + cover → any
            // with cover → English + ISBN-13 → English → has ISBN-13 → first.
            // Pulling cover-bearing editions ahead of the language/ISBN tiers
            // dramatically improves the % of library books that surface with
            // a tile image. The ISBN tier still matters because OL's
            // /b/isbn/<n>-L.jpg endpoint succeeds for many editions whose JSON
            // omits the `covers` field — see the Images-with-ISBN fallback in
            // OpenLibraryEditionMapper.ToEdition.
            return editions.Entries.FirstOrDefault(e => IsEnglishWithIsbn13(e) && HasCover(e))
                ?? editions.Entries.FirstOrDefault(e => IsEnglish(e) && HasCover(e))
                ?? editions.Entries.FirstOrDefault(HasCover)
                ?? editions.Entries.FirstOrDefault(IsEnglishWithIsbn13)
                ?? editions.Entries.FirstOrDefault(IsEnglish)
                ?? editions.Entries.FirstOrDefault(e => e.Isbn13?.Any() == true)
                ?? editions.Entries[0];
        }

        private static bool IsEnglish(OpenLibraryEditionResource e) =>
            e.Languages?.Any(l => l.Key != null && l.Key.EndsWith("/eng", System.StringComparison.OrdinalIgnoreCase)) == true;

        private static bool IsEnglishWithIsbn13(OpenLibraryEditionResource e) =>
            IsEnglish(e) && e.Isbn13?.Any() == true;

        // Strictly: only true when OL's JSON says the edition has a real
        // cover_i image. The ISBN fallback path (b/isbn/<n>-L.jpg) is
        // surprisingly often 404 — OL only resolves that endpoint when
        // someone has uploaded the ISBN→cover mapping — so trusting
        // "has ISBN-13" as a cover proxy meant SelectPrimaryEdition kept
        // picking coverless editions over siblings that had real cover_i
        // entries. Concrete repro: work OL16796166W (Casual Vacancy)
        // → mapper picked OL34507766M (Covers=null, isbn 9781306761017
        // → 404) instead of OL28783445M (cover_i=10716309).
        private static bool HasCover(OpenLibraryEditionResource e) =>
            e.Covers?.Any(c => c > 0) == true;

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

    internal static class OpenLibraryDateExtensions
    {
        public static System.DateTime? AsNullableDate(this string raw) => OpenLibraryDateParser.Parse(raw);
    }
}
