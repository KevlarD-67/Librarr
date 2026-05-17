using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public interface IOpenLibraryRequestBuilder
    {
        HttpRequestBuilder For(string path);
    }

    public class OpenLibraryRequestBuilder : IOpenLibraryRequestBuilder
    {
        private const string BaseUrl = "https://openlibrary.org/";

        public HttpRequestBuilder For(string path)
        {
            // OL's documented politeness rule: identify the consumer and
            // throttle. See https://openlibrary.org/developers/api#politeness
            var userAgent = $"Librarr/{BuildInfo.Version} (+https://github.com/Librarr/Librarr)";

            return new HttpRequestBuilder(BaseUrl + path.TrimStart('/'))
                .Accept(HttpAccept.Json)
                .SetHeader("User-Agent", userAgent)

                // WithRateLimit(seconds) — 0.6s between calls ≈ 100 req/min.
                // OL's actual limit is generous (no public number, but they've
                // tolerated higher rates from Open Library Bot). Stay conservative.
                .WithRateLimit(0.6);
        }
    }
}
