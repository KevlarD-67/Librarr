using System;
using System.Linq;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Audnex.Resources;

namespace NzbDrone.Core.MetadataSource.Audnex
{
    // Phase 7 audiobook augmenter. Pulls narrator + duration + ASIN-keyed
    // cover from api.audnex.us (community-hosted Audible mirror). Opt-in
    // via IConfigService.AugmentAudiobookMetadata — off by default because
    // audnex.us is a community service that may go away.
    //
    // Not registered against IProvideBookInfo on purpose: this is an
    // *augmenter*, layered on top of the primary metadata source's Book.
    // Wiring into RefreshBookService is a Phase 7b task (see TODO at the
    // bottom for the merge sketch).
    public class AudnexProxy : IAugmentAudiobookInfo
    {
        private const string BaseUrl = "https://api.audnex.us";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public AudnexProxy(IHttpClient httpClient,
                           IConfigService configService,
                           Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public bool CanAugment(Book book)
        {
            if (!_configService.AugmentAudiobookMetadata)
            {
                return false;
            }

            if (book?.Editions?.Value == null)
            {
                return false;
            }

            return book.Editions.Value.Any(e => HasAsin(e) && IsAudiobookFormat(e));
        }

        public Book Augment(Book book)
        {
            if (!CanAugment(book))
            {
                return book;
            }

            var edition = book.Editions.Value.FirstOrDefault(e => HasAsin(e) && IsAudiobookFormat(e));
            if (edition == null)
            {
                return book;
            }

            AudnexBookResource resource;
            try
            {
                var userAgent = $"Librarr/{BuildInfo.Version} (+https://github.com/Librarr/Librarr)";
                var request = new HttpRequestBuilder($"{BaseUrl}/books/{edition.Asin}")
                    .Accept(HttpAccept.Json)
                    .SetHeader("User-Agent", userAgent)
                    .WithRateLimit(1.0)
                    .Build();
                request.SuppressHttpError = true;

                var response = _httpClient.Get<AudnexBookResource>(request);
                resource = response?.Resource;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Audnex augment failed for ASIN {0}; falling back to primary metadata", edition.Asin);
                return book;
            }

            if (resource == null)
            {
                return book;
            }

            // Merge: only fill blanks. Don't overwrite the primary source's
            // authoritative title / overview / publisher.
            if (edition.Overview.IsNullOrWhiteSpace() && resource.Summary.IsNotNullOrWhiteSpace())
            {
                edition.Overview = resource.Summary;
            }

            if (edition.Publisher.IsNullOrWhiteSpace() && resource.Publisher.IsNotNullOrWhiteSpace())
            {
                edition.Publisher = resource.Publisher;
            }

            if (edition.PageCount == 0 && resource.RuntimeLengthMin.HasValue)
            {
                // Reuse PageCount for runtime-minutes — there's no Duration
                // field on Edition (yet). Phase 7b can introduce one if the
                // UI ever needs to render hours/minutes.
                edition.PageCount = resource.RuntimeLengthMin.Value;
            }

            if (!edition.ReleaseDate.HasValue && resource.ReleaseDate.HasValue)
            {
                edition.ReleaseDate = resource.ReleaseDate;
            }

            // TODO Phase 7b: surface narrators in the Book / Edition model.
            // The domain doesn't currently have a Narrator concept; adding
            // one needs an Authors-style join table + an API change.
            return book;
        }

        private static bool HasAsin(Edition edition)
        {
            return edition?.Asin.IsNotNullOrWhiteSpace() == true;
        }

        private static bool IsAudiobookFormat(Edition edition)
        {
            return edition?.Format == "AudioBook" || edition?.IsEbook == true;
        }
    }
}
