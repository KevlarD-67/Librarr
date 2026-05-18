using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // Phase 3 MVP + Phase 4 hardening. Implements the same method shapes as
    // the IProvide* / ISearchForNew* interfaces but DOES NOT declare them
    // (RegisterMany would bind a second impl per interface alongside
    // BookInfoProxy — see Phase 5 MetadataSourceFactory).
    //
    // Phase 4 added per-resource LazyCache wrapping with the TTLs from
    // MASTER-PLAN.md §4 (authors 24h, works 7d, editions 30d, search 1h)
    // and a Send<T> helper that retries 429 / 5xx with exponential
    // back-off + jitter (no Polly: Polly 8's generic pipeline doesn't
    // compose cleanly with HttpResponse<T> covariance — see commit msg).
    public class OpenLibraryProxy
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

        private readonly IHttpClient _httpClient;
        private readonly IOpenLibraryRequestBuilder _requestBuilder;
        private readonly Logger _logger;
        private readonly CachingService _cache;

        public OpenLibraryProxy(IHttpClient httpClient,
                                IOpenLibraryRequestBuilder requestBuilder,
                                Logger logger)
        {
            _httpClient = httpClient;
            _requestBuilder = requestBuilder;
            _logger = logger;

            _cache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())));
            _cache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = 3600 };
        }

        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
        {
            return Cached(useCache, $"oa_{foreignAuthorId}", TimeSpan.FromHours(24), () =>
            {
                var authorReq = _requestBuilder.For($"authors/{foreignAuthorId}.json").Build();
                var worksReq = _requestBuilder.For($"authors/{foreignAuthorId}/works.json?limit=200").Build();

                var authorResp = Send<OpenLibraryAuthorResource>(authorReq);
                var worksResp = Send<OpenLibraryAuthorWorksResource>(worksReq);

                if (authorResp?.Resource == null)
                {
                    throw new OpenLibraryException("OL author not found: {0}", foreignAuthorId);
                }

                return OpenLibraryAuthorMapper.ToAuthor(authorResp.Resource, worksResp?.Resource);
            });
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            // OL has no changed-since API. The per-author refresh schedule
            // covers freshness. Suppress the delta-refresh path.
            _logger.Debug("OL GetChangedAuthors called (startTime={0}); OL has no delta API, returning empty.", startTime);
            return new HashSet<string>();
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            return Cached(true, $"ow_{id}", TimeSpan.FromDays(7), () =>
            {
                var workReq = _requestBuilder.For($"works/{id}.json").Build();
                var work = Send<OpenLibraryWorkResource>(workReq)?.Resource;
                if (work == null)
                {
                    throw new OpenLibraryException("OL work not found: {0}", id);
                }

                var editionsReq = _requestBuilder.For($"works/{id}/editions.json?limit=50").Build();
                var editions = Send<OpenLibraryEditionListResource>(editionsReq)?.Resource;

                var (book, authors) = OpenLibraryWorkMapper.ToBook(work, editions);

                return Tuple.Create(id, book, authors);
            });
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            return Cached(true, $"osa_{title}", TimeSpan.FromHours(1), () =>
            {
                var req = _requestBuilder.For($"search/authors.json?q={Uri.EscapeDataString(title)}").Build();
                var resp = Send<OpenLibraryAuthorSearchResource>(req);

                var result = new List<Author>();
                if (resp?.Resource?.Docs == null)
                {
                    return result;
                }

                foreach (var doc in resp.Resource.Docs)
                {
                    result.Add(OpenLibrarySearchMapper.ToAuthorSummary(doc));
                }

                return result;
            });
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var cacheKey = $"os_{title}|{author}|{getAllEditions}";

            return Cached(true, cacheKey, TimeSpan.FromHours(1), () =>
            {
                string qs;
                if (string.IsNullOrWhiteSpace(author))
                {
                    // Global search-bar path: the user typed something that
                    // might be a title, an author, or a series. OL's `q=`
                    // does proper relevance scoring across all indexed
                    // fields and ranks canonical works first. `?title=`
                    // over-matches on compilation/omnibus works whose
                    // titles contain the author name (e.g. "Brandon
                    // Sanderson Sampler"), which mostly lack covers.
                    qs = $"?q={Uri.EscapeDataString(title)}";
                }
                else
                {
                    // Targeted add-book / reidentify path: caller has
                    // disambiguated by author already, exact title+author
                    // is the right shape.
                    qs = $"?title={Uri.EscapeDataString(title)}&author={Uri.EscapeDataString(author)}";
                }

                qs += "&limit=20&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count";

                var resp = Send<OpenLibrarySearchResource>(_requestBuilder.For($"search.json{qs}").Build());
                return OpenLibrarySearchMapper.ReRankAndMap(resp?.Resource, title, author);
            });
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return Cached(true, $"oisbn_{isbn}", TimeSpan.FromDays(30), () =>
            {
                var request = _requestBuilder.For($"isbn/{isbn}.json").Build();
                request.AllowAutoRedirect = true;

                var resp = Send<OpenLibraryEditionResource>(request);
                if (resp?.Resource == null)
                {
                    return new List<Book>();
                }

                return new List<Book> { OpenLibraryEditionMapper.ToBook(resp.Resource) };
            });
        }

        public List<Book> SearchByAsin(string asin)
        {
            return Cached(true, $"oasin_{asin}", TimeSpan.FromDays(30), () =>
            {
                var req = _requestBuilder.For($"search.json?q=identifier%3A{Uri.EscapeDataString(asin)}&limit=5").Build();
                var resp = Send<OpenLibrarySearchResource>(req);
                return OpenLibrarySearchMapper.ReRankAndMap(resp?.Resource, asin, null);
            });
        }

        public List<Book> SearchByForeignBookId(string foreignBookId, bool getAllEditions)
        {
            if (string.IsNullOrWhiteSpace(foreignBookId))
            {
                return new List<Book>();
            }

            if (foreignBookId.EndsWith("W", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var (_, book, _) = GetBookInfo(foreignBookId);
                    return new List<Book> { book };
                }
                catch (OpenLibraryException)
                {
                    return new List<Book>();
                }
            }

            if (foreignBookId.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                return Cached(true, $"oe_{foreignBookId}", TimeSpan.FromDays(30), () =>
                {
                    var resp = Send<OpenLibraryEditionResource>(_requestBuilder.For($"books/{foreignBookId}.json").Build());
                    if (resp?.Resource == null)
                    {
                        return new List<Book>();
                    }

                    return new List<Book> { OpenLibraryEditionMapper.ToBook(resp.Resource) };
                });
            }

            return new List<Book>();
        }

        public List<object> SearchForNewEntity(string title)
        {
            var result = new List<object>();

            foreach (var author in SearchForNewAuthor(title))
            {
                result.Add(author);
                if (result.Count >= 20)
                {
                    break;
                }
            }

            foreach (var book in SearchForNewBook(title, null))
            {
                result.Add(book);
                if (result.Count >= 40)
                {
                    break;
                }
            }

            return result;
        }

        private T Cached<T>(bool useCache, string cacheKey, TimeSpan ttl, Func<T> factory)
        {
            if (!useCache)
            {
                return factory();
            }

            return _cache.GetOrAdd(cacheKey, () => factory(), DateTimeOffset.UtcNow.Add(ttl));
        }

        // Inline retry loop for OL transient failures. 429 + 5xx → wait
        // (2s, 4s, 8s) + jitter, max 3 retries. Honors the Retry-After
        // header when OL provides one (common on 429).
        //
        // Not using Polly: Polly 8's ResiliencePipeline<HttpResponse> doesn't
        // compose cleanly with the generic HttpResponse<T> here (covariance
        // round-trip via cast works at runtime but is ugly and brittle when
        // the upstream IHttpClient signature evolves). Worth a re-look once
        // more OL endpoints land — until then, the inline loop is plenty.
        private HttpResponse<T> Send<T>(HttpRequest request)
            where T : new()
        {
            var delay = InitialRetryDelay;
            HttpResponse<T> response = null;

            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    response = _httpClient.Get<T>(request);
                }
                catch (HttpException ex) when (ex.Response != null && IsRetryable(ex.Response) && attempt < MaxRetries)
                {
                    _logger.Warn(ex, "OL request {0} threw on attempt {1}; retrying after {2}s", request.Url, attempt + 1, delay.TotalSeconds);
                    Wait(ex.Response, delay);
                    delay = NextDelay(delay);
                    continue;
                }

                if (response != null && IsRetryable(response) && attempt < MaxRetries)
                {
                    _logger.Warn("OL request {0} returned {1} on attempt {2}; retrying after {3}s", request.Url, response.StatusCode, attempt + 1, delay.TotalSeconds);
                    Wait(response, delay);
                    delay = NextDelay(delay);
                    continue;
                }

                return response;
            }

            return response;
        }

        private static bool IsRetryable(HttpResponse response)
        {
            if (response == null)
            {
                return false;
            }

            return response.StatusCode == HttpStatusCode.TooManyRequests || response.HasHttpServerError;
        }

        private static void Wait(HttpResponse response, TimeSpan fallback)
        {
            var retryAfter = response?.Headers?.GetSingleValue("Retry-After");
            if (!string.IsNullOrEmpty(retryAfter) && int.TryParse(retryAfter, out var seconds))
            {
                Thread.Sleep(TimeSpan.FromSeconds(seconds));
                return;
            }

            Thread.Sleep(fallback);
        }

        private static TimeSpan NextDelay(TimeSpan current)
        {
            // Exponential * (0.8–1.2) jitter. Random instance avoided to keep
            // the helper deterministic-ish in tests.
            var ms = (long)(current.TotalMilliseconds * 2);
            var jittered = ms + ((ms / 5) * ((DateTime.UtcNow.Ticks % 3) - 1));
            return TimeSpan.FromMilliseconds(Math.Max(jittered, 1));
        }
    }
}
