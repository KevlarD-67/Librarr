using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaCover;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    // Phase 4b. OL's bibliographic JSON returns cover/photo IDs as
    // integers; the actual image is at
    //   https://covers.openlibrary.org/b/id/{id}-L.jpg     (book/edition)
    //   https://covers.openlibrary.org/a/id/{id}-L.jpg     (author photo)
    // OL uses -L (large), -M (medium), -S (small). We store the -L URL.
    //
    // Negative cover IDs in OL responses are sentinels meaning "no
    // cover" — they must be filtered out before constructing URLs.
    internal static class OpenLibraryCoverUrls
    {
        private const string BookCoverFormat = "https://covers.openlibrary.org/b/id/{0}-L.jpg";
        private const string AuthorPhotoFormat = "https://covers.openlibrary.org/a/id/{0}-L.jpg";

        public static List<MediaCover.MediaCover> ForBook(List<int> coverIds)
        {
            return BuildCovers(coverIds, BookCoverFormat, MediaCoverTypes.Cover);
        }

        public static List<MediaCover.MediaCover> ForAuthor(List<int> photoIds)
        {
            return BuildCovers(photoIds, AuthorPhotoFormat, MediaCoverTypes.Poster);
        }

        private static List<MediaCover.MediaCover> BuildCovers(List<int> ids, string urlFormat, MediaCoverTypes type)
        {
            if (ids == null)
            {
                return new List<MediaCover.MediaCover>();
            }

            return ids
                .Where(id => id > 0)
                .Distinct()
                .Select(id => new MediaCover.MediaCover
                {
                    Url = string.Format(urlFormat, id),
                    CoverType = type
                })
                .ToList();
        }
    }
}
