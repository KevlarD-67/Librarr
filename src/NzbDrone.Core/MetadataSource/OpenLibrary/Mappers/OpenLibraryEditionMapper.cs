using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryEditionMapper
    {
        public static Edition ToEdition(OpenLibraryEditionResource resource)
        {
            var olKey = ExtractKey(resource.Key);
            var isbn13 = resource.Isbn13?.FirstOrDefault();
            var asin = resource.Identifiers?.Amazon?.FirstOrDefault();
            var format = NormalizeFormat(resource.PhysicalFormat);

            // Cover-resolution chain (best → fallback):
            //   1. resource.Covers — work-specific cover_i list returned by OL
            //      for editions that have an indexed primary cover.
            //   2. ISBN-keyed — many editions ship without a `covers` field
            //      yet have an /b/isbn/<n>-L.jpg image. Verified live for the
            //      majority of Sparknotes / Pottermore editions whose JSON
            //      omits `covers`. `?default=false` (set in
            //      OpenLibraryCoverUrls) makes missing covers 404 so the
            //      MediaCoverProxy 404-quiet path takes over.
            //   3. olid-keyed — final fallback for editions that have no
            //      ISBN either; cheap to keep, harmless when it 404s.
            // When none of the three actually produces an image, the
            // frontend renders its built-in book placeholder.
            var images = OpenLibraryCoverUrls.ForBook(resource.Covers);
            if (images.Count == 0 && isbn13.IsNotNullOrWhiteSpace())
            {
                images = OpenLibraryCoverUrls.ForBookByIsbn(isbn13);
            }

            if (images.Count == 0 && olKey.IsNotNullOrWhiteSpace())
            {
                images = OpenLibraryCoverUrls.ForBookByOlid(olKey);
            }

            return new Edition
            {
                ForeignEditionId = olKey,
                TitleSlug = olKey,
                Isbn13 = isbn13,
                Asin = asin,
                Title = resource.Title,
                Language = ExtractLanguageCode(resource.Languages),
                Overview = resource.Description,
                Format = format,
                IsEbook = IsEbookFormat(format),
                Disambiguation = resource.Subtitle,
                Publisher = resource.Publishers?.FirstOrDefault(),
                PageCount = resource.NumberOfPages ?? 0,
                ReleaseDate = OpenLibraryDateParser.Parse(resource.PublishDate),
                Images = images,
                Monitored = true
            };
        }

        // OL's Languages list carries entries like {"key": "/languages/eng"}.
        // Extract the 3-letter ISO 639-2 code from the last segment of the
        // first entry. Cycle 2 of the Le Guin completeness loop: Edition
        // already had a Language column + the EditionResource already
        // surfaced it both ways, but the mapper here was the gap — so
        // 0/249 books had a populated language despite OL having data on
        // 244/249. SelectPrimaryEdition in OpenLibraryWorkMapper already
        // reads the same Languages list for its English-preference tiers,
        // proving the data shape is reliable.
        private static string ExtractLanguageCode(System.Collections.Generic.List<Resources.OpenLibraryKey> languages)
        {
            var key = languages?.FirstOrDefault()?.Key;
            if (key.IsNullOrWhiteSpace())
            {
                return null;
            }

            var slash = key.LastIndexOf('/');
            return slash >= 0 ? key.Substring(slash + 1) : key;
        }

        public static Book ToBook(OpenLibraryEditionResource resource)
        {
            // When an external ID lookup (ISBN, ASIN, edition OLID) hits OL,
            // we only know the edition. Reconstruct a slim book wrapper so
            // downstream code can still navigate Book → Editions.
            var workKey = resource.Works?.FirstOrDefault()?.Key;
            var foreignBookId = workKey.IsNotNullOrWhiteSpace() ? ExtractKey(workKey) : ExtractKey(resource.Key);

            var edition = ToEdition(resource);

            var book = new Book
            {
                ForeignBookId = foreignBookId,
                ForeignEditionId = edition.ForeignEditionId,
                TitleSlug = foreignBookId,
                Title = resource.Title,
                CleanTitle = Parser.Parser.CleanAuthorName(resource.Title),
                ReleaseDate = edition.ReleaseDate,
                AnyEditionOk = true,
                Editions = new List<Edition> { edition }
            };

            // TODO Phase 4: also fetch the work for richer metadata when callers
            // can absorb a second HTTP call.
            return book;
        }

        private static string NormalizeFormat(string raw)
        {
            if (raw.IsNullOrWhiteSpace())
            {
                return null;
            }

            var lower = raw.ToLowerInvariant();
            if (lower.Contains("ebook") || lower.Contains("electronic") || lower.Contains("epub") || lower.Contains("kindle"))
            {
                return "EBook";
            }

            if (lower.Contains("audio"))
            {
                return "AudioBook";
            }

            if (lower.Contains("hardcover") || lower.Contains("hardback"))
            {
                return "Hardcover";
            }

            if (lower.Contains("paperback") || lower.Contains("softcover"))
            {
                return "Paperback";
            }

            return raw;
        }

        private static bool IsEbookFormat(string normalizedFormat)
        {
            return normalizedFormat == "EBook" || normalizedFormat == "AudioBook";
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
