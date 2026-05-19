using System;
using System.Collections.Generic;
using System.Linq;
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
            // Cache the raw HTTP resources only; run the mapper on every
            // call so each caller gets a fresh Author + slim Book list.
            // LazyCache hands back the same object reference on hit, and
            // downstream code (BookService.AddBook, BasicRepository.Insert)
            // mutates Book/Edition fields in place — so a cached *mapped*
            // payload leaks Id/BookId/Monitored mutations across calls.
            var resources = Cached(useCache, $"oa_{foreignAuthorId}", TimeSpan.FromHours(24), () =>
            {
                var authorReq = _requestBuilder.For($"authors/{foreignAuthorId}.json").Build();

                // OL accepts limit=1000 in a single response (verified:
                // Le Guin's 252 works returned 251 entries at limit=1000
                // vs 200 at limit=200, clipping The Dispossessed off
                // the end and out of the author's discography in the
                // UI entirely). All prolific authors known today stay
                // well under 1000 (Asimov ~500, King ~600, Le Guin 252).
                // When a future author exceeds this, the symptom is a
                // clipped list (size > entries.Count) and the fix is
                // to escalate to a paginated loop here — easy migration.
                var worksReq = _requestBuilder.For($"authors/{foreignAuthorId}/works.json?limit=1000").Build();

                var authorResp = Send<OpenLibraryAuthorResource>(authorReq);
                var worksResp = Send<OpenLibraryAuthorWorksResource>(worksReq);

                if (authorResp?.Resource == null)
                {
                    throw new OpenLibraryException("OL author not found: {0}", foreignAuthorId);
                }

                return (Author: authorResp.Resource, Works: worksResp?.Resource);
            });

            return OpenLibraryAuthorMapper.ToAuthor(resources.Author, resources.Works);
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
            // Cache the raw HTTP resources only; run the mapper on every
            // call so each caller gets a fresh Book + Editions list.
            // LazyCache hands back the same object reference on hit, and
            // BookService.AddBook + BasicRepository.Insert mutate the
            // edition objects in place (Id via reflection, BookId via
            // ForEach, Monitored toggle) — so a cached *mapped* payload
            // leaks those mutations across calls. After a first add,
            // editions in cache carry Ids that no longer correspond to
            // any DB row, and the retry path's SetMonitored assertion
            // fires with Count(Monitored)==0.
            var resources = Cached(true, $"ow_{id}", TimeSpan.FromDays(7), () =>
            {
                var workReq = _requestBuilder.For($"works/{id}.json").Build();
                var work = Send<OpenLibraryWorkResource>(workReq)?.Resource;
                if (work == null)
                {
                    throw new OpenLibraryException("OL work not found: {0}", id);
                }

                // 50 → 200 so works with long edition lists carry more
                // cover-bearing candidates into SelectPrimaryEdition's
                // tiered preference (English+ISBN13+cover →
                // English+cover → any cover → ...). Latent before now —
                // Rowling's books all had covers in the first 50 — but
                // surfaced by the Le Guin investigation (LHoD has 91
                // editions; cover candidates can plausibly cluster in
                // the tail of works with audiobook/foreign reprints).
                var editionsReq = _requestBuilder.For($"works/{id}/editions.json?limit=200").Build();
                var editionsRes = Send<OpenLibraryEditionListResource>(editionsReq)?.Resource;

                return (Work: work, Editions: editionsRes);
            });

            var (book, authors) = OpenLibraryWorkMapper.ToBook(resources.Work, resources.Editions);

            // AddBookService.AddSkyhookData:130 expects Item1 to be
            // the **author** foreign id so it can locate the matching
            // AuthorMetadata in Item3 via
            //   tuple.Item3.FirstOrDefault(x => x.ForeignAuthorId == tuple.Item1)
            // The BookInfoProxy returned `authorId` here; the mapper
            // was incorrectly returning the work id, which never
            // matched anything in `authors` (those carry author OLIDs
            // ending in A, work id ends in W). Result: AuthorMetadata
            // null, then NRE on `.Value.ForeignAuthorId` access in
            // AddBookService:58.
            var primaryAuthorId = authors.FirstOrDefault()?.ForeignAuthorId ?? id;
            return Tuple.Create(primaryAuthorId, book, authors);
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            // OL's `/search/*.json` returns 422 UnprocessableEntity for
            // single-char queries (e.g. when the user is mid-typing and
            // the frontend's onChange fires a search on each keystroke).
            // Short-circuit before the HTTP call to avoid both the
            // wasted round-trip and the [Fatal] error pipeline log.
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 2)
            {
                return new List<Author>();
            }

            return Cached(true, $"osa_{title}", TimeSpan.FromHours(1), () =>
            {
                var req = _requestBuilder.For($"search/authors.json?q={Uri.EscapeDataString(title)}").Build();
                req.SuppressHttpError = false;
                req.SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.UnprocessableEntity };
                req.LogHttpError = false;

                HttpResponse<OpenLibraryAuthorSearchResource> resp;
                try
                {
                    resp = Send<OpenLibraryAuthorSearchResource>(req);
                }
                catch (HttpException ex) when (ex.Response?.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    return new List<Author>();
                }

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
            // OL's /search.json 422s on single-char queries — short-circuit
            // before the HTTP call to avoid both the wasted round-trip and
            // the [Fatal] error pipeline log when the frontend fires search
            // on every keystroke.
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 2)
            {
                return new List<Book>();
            }

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

                var req = _requestBuilder.For($"search.json{qs}").Build();
                req.SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.UnprocessableEntity };
                req.LogHttpError = false;

                HttpResponse<OpenLibrarySearchResource> resp;
                try
                {
                    resp = Send<OpenLibrarySearchResource>(req);
                }
                catch (HttpException ex) when (ex.Response?.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    return new List<Book>();
                }

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
            // Typed-prefix search shortcuts mirroring the BookInfoProxy
            // syntax (`isbn:` / `asin:` / `author:` / `work:` /
            // `edition:`), updated for OL's identifier shape:
            //   author:OL1394865A  → /authors/{key}.json single result
            //   work:OL26421189W   → /works/{key}.json   single result
            //   edition:OL49282196M → /books/{key}.json  single result
            //   isbn:067003469X    → /isbn/{value}.json  ISBN lookup
            //   asin:B00JCDK5ME    → search.json?q=identifier:{asin}
            //
            // Unknown prefixes (and prefix-less queries) fall through to
            // the existing author + book merged search.
            var prefixed = TryPrefixedSearch(title);
            if (prefixed != null)
            {
                return prefixed;
            }

            // Two layers of dedup for authors:
            //   seenAuthorIds   — drops duplicate OLIDs (cheap)
            //   authorByCleanName — drops duplicate *people*. OL frequently
            //     has multiple author OLIDs for the same person spelled
            //     three different ways ("J. K. Rowling", "J.K. Rowling",
            //     "J.k. Rowling" → OL23919A, OL16230142A, OL16034707A).
            //     The book-search-synthesized candidate is preferred when
            //     a duplicate is detected, because OL's book index links
            //     to the *canonical* author OLID (the one that actually has
            //     works attached) rather than the stub records that pollute
            //     /search/authors.json. CleanName normalization (no spaces,
            //     no dots, lowercase) is what Parser.CleanAuthorName already
            //     produces, so reuse it.
            var result = new List<object>();
            var seenAuthorIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var authorByCleanName = new Dictionary<string, Author>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var author in SearchForNewAuthor(title))
            {
                if (author.Metadata?.Value?.ForeignAuthorId != null)
                {
                    seenAuthorIds.Add(author.Metadata.Value.ForeignAuthorId);
                }

                var cleanName = author.CleanName;
                if (!string.IsNullOrWhiteSpace(cleanName))
                {
                    // First write wins for /search/authors.json hits — they
                    // arrive in OL's own ranking order. We may overwrite
                    // with a book-synthesized hit later.
                    if (!authorByCleanName.ContainsKey(cleanName))
                    {
                        authorByCleanName[cleanName] = author;
                    }
                }
                else
                {
                    // No clean name (shouldn't happen, but be safe) —
                    // surface as-is.
                    result.Add(author);
                }
            }

            var books = SearchForNewBook(title, null);

            // Synthesize Author tiles from the books' author metadata.
            // OL's book index links to the canonical author OLID for each
            // work, so a synthesized author is *always* the preferred
            // entry when its CleanName collides with a stub from
            // /search/authors.json — overwrite. Don't gate on
            // seenAuthorIds here: a canonical OLID can legitimately
            // appear in BOTH /search/authors.json and the book index
            // (it just happens to have a J.K. Rowling stub spelled
            // three different ways in the author search), and we want
            // the book-derived entry to win the name-collision.
            foreach (var book in books)
            {
                var meta = book?.AuthorMetadata?.Value;
                if (meta == null || string.IsNullOrWhiteSpace(meta.ForeignAuthorId))
                {
                    continue;
                }

                var cleanName = Parser.Parser.CleanAuthorName(meta.Name);

                // Already synthesized for this exact OLID under this
                // CleanName — skip the duplicate (every other book by
                // the same author would otherwise re-run the work).
                if (!string.IsNullOrWhiteSpace(cleanName)
                    && authorByCleanName.TryGetValue(cleanName, out var existing)
                    && existing.Metadata?.Value?.ForeignAuthorId == meta.ForeignAuthorId)
                {
                    continue;
                }

                var synthesizedAuthor = new Author
                {
                    Metadata = new AuthorMetadata
                    {
                        ForeignAuthorId = meta.ForeignAuthorId,
                        TitleSlug = meta.TitleSlug ?? meta.ForeignAuthorId,
                        Name = meta.Name,
                        Images = OpenLibraryCoverUrls.ForAuthorByOlid(meta.ForeignAuthorId)
                    },
                    CleanName = cleanName
                };

                if (!string.IsNullOrWhiteSpace(cleanName))
                {
                    authorByCleanName[cleanName] = synthesizedAuthor;
                }
                else
                {
                    result.Add(synthesizedAuthor);
                }

                seenAuthorIds.Add(meta.ForeignAuthorId);
            }

            foreach (var author in authorByCleanName.Values)
            {
                result.Add(author);
                if (result.Count >= 20)
                {
                    break;
                }
            }

            foreach (var book in books)
            {
                result.Add(book);
                if (result.Count >= 40)
                {
                    break;
                }
            }

            return result;
        }

        private List<object> TryPrefixedSearch(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || !title.Contains(':'))
            {
                return null;
            }

            // Lower-case the prefix only — OL identifiers (OL...W, OL...M,
            // OL...A) preserve their original casing on the right-hand
            // side. ISBNs / ASINs are case-insensitive in practice, but
            // we pass them through unchanged so the proxy methods see
            // exactly what the user typed.
            var split = title.Split(new[] { ':' }, 2);
            if (split.Length != 2)
            {
                return null;
            }

            var prefix = split[0].Trim().ToLowerInvariant();
            var slug = split[1].Trim();

            if (string.IsNullOrWhiteSpace(slug) || slug.Any(char.IsWhiteSpace))
            {
                return null;
            }

            switch (prefix)
            {
                case "isbn":
                    return SearchByIsbn(slug).Cast<object>().ToList();
                case "asin":
                    return SearchByAsin(slug).Cast<object>().ToList();
                case "work":
                case "edition":
                    // SearchByForeignBookId routes by the suffix letter
                    // (W → work, M → edition), so both prefixes share it.
                    return SearchByForeignBookId(slug, true).Cast<object>().ToList();
                case "author":
                    try
                    {
                        var author = GetAuthorInfo(slug);
                        return author != null ? new List<object> { author } : new List<object>();
                    }
                    catch (OpenLibraryException)
                    {
                        return new List<object>();
                    }

                default:
                    return null;
            }
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
                catch (Exception ex) when (IsTransientNetworkError(ex) && attempt < MaxRetries)
                {
                    // HTTP/2 stream resets, mid-response IOException, request
                    // timeouts. The original HttpException catch above only
                    // covers status-coded failures (429/5xx) — torn streams
                    // never reach the response phase, so they escape as raw
                    // HttpRequestException/IOException and previously
                    // aborted entire author refreshes mid-flight. Retry with
                    // the same backoff schedule as the status-coded path.
                    _logger.Warn(ex, "OL request {0} hit transient network error on attempt {1}; retrying after {2}s", request.Url, attempt + 1, delay.TotalSeconds);
                    Thread.Sleep(delay);
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

        private static bool IsTransientNetworkError(Exception ex)
        {
            return ex is System.Net.Http.HttpRequestException
                || ex is System.IO.IOException
                || ex is System.Threading.Tasks.TaskCanceledException;
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
