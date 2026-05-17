using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryAuthorMapper
    {
        public static Author ToAuthor(OpenLibraryAuthorResource resource, OpenLibraryAuthorWorksResource works)
        {
            var metadata = ToMetadata(resource);

            var author = new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                Monitored = true,
            };

            // Works are surfaced as the author's book list. Each Phase-3 work
            // returns a slim book with no edition data — Phase 4 hardening
            // will hydrate editions per work, ideally lazily.
            // TODO Phase 4: backfill editions from /works/{key}/editions.json.
            return author;
        }

        public static AuthorMetadata ToMetadata(OpenLibraryAuthorResource resource)
        {
            var olKey = ExtractKey(resource.Key);

            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = olKey,
                TitleSlug = olKey,
                Name = (resource.PersonalName ?? resource.Name).CleanSpaces(),
                Overview = resource.Bio,
                Born = OpenLibraryDateParser.Parse(resource.BirthDate),
                Died = OpenLibraryDateParser.Parse(resource.DeathDate),
                Status = resource.DeathDate.IsNotNullOrWhiteSpace() ? AuthorStatusType.Ended : AuthorStatusType.Continuing
            };

            metadata.SortName = metadata.Name?.ToLowerInvariant();
            metadata.NameLastFirst = metadata.Name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst?.ToLowerInvariant();

            if (resource.AlternateNames != null)
            {
                metadata.Aliases.AddRange(resource.AlternateNames);
            }

            metadata.Images = OpenLibraryCoverUrls.ForAuthor(resource.Photos);

            // TODO Phase 4: links → Links list (homepage, wikipedia).
            return metadata;
        }

        private static string ExtractKey(string olKey)
        {
            // "/authors/OL5749351A" → "OL5749351A"; pass-through if already bare.
            if (olKey.IsNullOrWhiteSpace())
            {
                return olKey;
            }

            var slash = olKey.LastIndexOf('/');
            return slash >= 0 ? olKey.Substring(slash + 1) : olKey;
        }
    }
}
