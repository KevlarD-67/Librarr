using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using Readarr.Http.REST;
using Swashbuckle.AspNetCore.Annotations;

namespace Readarr.Api.V1.Books
{
    public class EditionResource : RestResource
    {
        public int BookId { get; set; }
        public string ForeignEditionId { get; set; }
        public string TitleSlug { get; set; }
        public string Isbn13 { get; set; }
        public string Asin { get; set; }
        public string Title { get; set; }
        public string Language { get; set; }
        public string Overview { get; set; }
        public string Format { get; set; }
        public bool IsEbook { get; set; }
        public string Disambiguation { get; set; }
        public string Publisher { get; set; }
        public int PageCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Narrators { get; set; }
        public List<MediaCover> Images { get; set; }
        public List<Links> Links { get; set; }
        public Ratings Ratings { get; set; }
        public bool Monitored { get; set; }
        public bool ManualAdd { get; set; }
        public string RemoteCover { get; set; }

        //Hiding this so people don't think its usable (only used to set the initial state)
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [SwaggerIgnore]
        public bool Grabbed { get; set; }
    }

    public static class EditionResourceMapper
    {
        public static EditionResource ToResource(this Edition model)
        {
            if (model == null)
            {
                return null;
            }

            return new EditionResource
            {
                Id = model.Id,
                BookId = model.BookId,
                ForeignEditionId = model.ForeignEditionId,
                TitleSlug = model.TitleSlug,
                Isbn13 = model.Isbn13,
                Asin = model.Asin,
                Title = model.Title,
                Language = model.Language,
                Overview = model.Overview,
                Format = model.Format,
                IsEbook = model.IsEbook,
                Disambiguation = model.Disambiguation,
                Publisher = model.Publisher,
                PageCount = model.PageCount,
                ReleaseDate = model.ReleaseDate,
                Narrators = ProjectNarrators(model),
                Images = model.Images,
                Links = model.Links,
                Ratings = model.Ratings,
                Monitored = model.Monitored,
                ManualAdd = model.ManualAdd
            };
        }

        // Source narrators from the normalized Narrators/EditionNarrators
        // tables when the lazy-load resolves with data; fall back to the
        // legacy comma-separated column for editions where the join hasn't
        // been populated yet (e.g. records from before migration 043
        // synced for the first time). Frontend continues to receive a
        // comma-separated string — see BookDetailsHeader.js.
        private static string ProjectNarrators(Edition model)
        {
            var fromJoin = model.NarratorList?.Value;
            if (fromJoin != null && fromJoin.Count > 0)
            {
                return string.Join(", ", fromJoin.Select(n => n.Name));
            }

            return model.Narrators;
        }

        public static Edition ToModel(this EditionResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new Edition
            {
                Id = resource.Id,
                BookId = resource.BookId,
                ForeignEditionId = resource.ForeignEditionId,
                TitleSlug = resource.TitleSlug,
                Isbn13 = resource.Isbn13,
                Asin = resource.Asin,
                Title = resource.Title,
                Language = resource.Language,
                Overview = resource.Overview,
                Format = resource.Format,
                IsEbook = resource.IsEbook,
                Disambiguation = resource.Disambiguation,
                Publisher = resource.Publisher,
                PageCount = resource.PageCount,
                ReleaseDate = resource.ReleaseDate,
                Narrators = resource.Narrators,
                Images = resource.Images,
                Links = resource.Links,
                Ratings = resource.Ratings,
                Monitored = resource.Monitored,
                ManualAdd = resource.ManualAdd
            };
        }

        public static List<EditionResource> ToResource(this IEnumerable<Edition> models)
        {
            return models?.Select(ToResource).ToList();
        }

        public static List<Edition> ToModel(this IEnumerable<EditionResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
