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

            return new Edition
            {
                ForeignEditionId = olKey,
                TitleSlug = olKey,
                Isbn13 = isbn13,
                Asin = asin,
                Title = resource.Title,
                Overview = resource.Description,
                Format = format,
                IsEbook = IsEbookFormat(format),
                Disambiguation = resource.Subtitle,
                Publisher = resource.Publishers?.FirstOrDefault(),
                PageCount = resource.NumberOfPages ?? 0,
                ReleaseDate = OpenLibraryDateParser.Parse(resource.PublishDate),
                Images = OpenLibraryCoverUrls.ForBook(resource.Covers),
                Monitored = true
            };
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
