using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // Phase 3 MVP. Implements the same method shapes as the IProvide* /
    // ISearchForNew* interfaces but DOES NOT declare them, deliberately —
    // declaring them would cause DryIoc's RegisterMany auto-discovery
    // (Composition/Extensions.cs:31) to bind a second implementation against
    // each interface alongside BookInfoProxy, yielding nondeterministic
    // last-registered-wins resolution.
    //
    // The MetadataSourceFactory that swaps between proxies based on
    // IConfigService.MetadataSourceType lands in Phase 5 alongside the
    // reidentify wizard. Until then this class is only resolvable as the
    // concrete type (WithAutoConcreteTypeResolution makes that work) and is
    // exercised by tests and the standalone health-check path.
    //
    // See MASTER-PLAN.md Phase 3 for the full design and METADATA-MIGRATION.md
    // §7 for the field-by-field OL→Readarr mapping table.
    public class OpenLibraryProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly IOpenLibraryRequestBuilder _requestBuilder;
        private readonly Logger _logger;

        public OpenLibraryProxy(IHttpClient httpClient,
                                IOpenLibraryRequestBuilder requestBuilder,
                                Logger logger)
        {
            _httpClient = httpClient;
            _requestBuilder = requestBuilder;
            _logger = logger;
        }

        // Matches IProvideAuthorInfo.GetAuthorInfo. Two HTTP calls: author
        // detail + first page of works.
        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
        {
            var authorReq = _requestBuilder.For($"authors/{foreignAuthorId}.json").Build();
            var worksReq = _requestBuilder.For($"authors/{foreignAuthorId}/works.json?limit=200").Build();

            var authorResp = _httpClient.Get<OpenLibraryAuthorResource>(authorReq);
            var worksResp = _httpClient.Get<OpenLibraryAuthorWorksResource>(worksReq);

            if (authorResp?.Resource == null)
            {
                throw new OpenLibraryException("OL author not found: {0}", foreignAuthorId);
            }

            return OpenLibraryAuthorMapper.ToAuthor(authorResp.Resource, worksResp?.Resource);
        }

        // Matches IProvideAuthorInfo.GetChangedAuthors. OL has no
        // "changed-since" endpoint; the per-author refresh schedule already
        // covers freshness. Return empty to suppress the delta-refresh path.
        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            return new HashSet<string>();
        }

        // Matches IProvideBookInfo.GetBookInfo. `id` is an OL work key
        // (e.g., "OL14931151W"). Returns (workId, book, authors).
        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            var workReq = _requestBuilder.For($"works/{id}.json").Build();
            var work = _httpClient.Get<OpenLibraryWorkResource>(workReq)?.Resource;
            if (work == null)
            {
                throw new OpenLibraryException("OL work not found: {0}", id);
            }

            var editionsReq = _requestBuilder.For($"works/{id}/editions.json?limit=50").Build();
            var editions = _httpClient.Get<OpenLibraryEditionListResource>(editionsReq)?.Resource;

            var (book, authors) = OpenLibraryWorkMapper.ToBook(work, editions);

            return Tuple.Create(id, book, authors);
        }

        // Matches ISearchForNewAuthor.SearchForNewAuthor.
        public List<Author> SearchForNewAuthor(string title)
        {
            var req = _requestBuilder.For($"search/authors.json?q={Uri.EscapeDataString(title)}").Build();
            var resp = _httpClient.Get<OpenLibraryAuthorSearchResource>(req);

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
        }

        // Matches ISearchForNewBook.SearchForNewBook.
        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var qs = $"?title={Uri.EscapeDataString(title)}";
            if (!string.IsNullOrWhiteSpace(author))
            {
                qs += $"&author={Uri.EscapeDataString(author)}";
            }

            qs += "&limit=20&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count";

            var resp = _httpClient.Get<OpenLibrarySearchResource>(_requestBuilder.For($"search.json{qs}").Build());
            return OpenLibrarySearchMapper.ReRankAndMap(resp?.Resource, title, author);
        }

        // Matches ISearchForNewBook.SearchByIsbn.
        // /isbn/{isbn}.json redirects (302) to /books/OL...M.json — the http
        // client follows that automatically when AllowAutoRedirect is true.
        public List<Book> SearchByIsbn(string isbn)
        {
            var request = _requestBuilder.For($"isbn/{isbn}.json").Build();
            request.AllowAutoRedirect = true;

            var resp = _httpClient.Get<OpenLibraryEditionResource>(request);
            if (resp?.Resource == null)
            {
                return new List<Book>();
            }

            return new List<Book> { OpenLibraryEditionMapper.ToBook(resp.Resource) };
        }

        // Matches ISearchForNewBook.SearchByAsin.
        // OL has no dedicated ASIN endpoint; use the search index via the
        // identifier qualifier.
        public List<Book> SearchByAsin(string asin)
        {
            var req = _requestBuilder.For($"search.json?q=identifier%3A{Uri.EscapeDataString(asin)}&limit=5").Build();
            var resp = _httpClient.Get<OpenLibrarySearchResource>(req);
            return OpenLibrarySearchMapper.ReRankAndMap(resp?.Resource, asin, null);
        }

        // Matches ISearchForNewBook.SearchByForeignBookId (post-Phase-2 rename).
        // Caller may pass an OL work key or an OL edition key. Differentiate
        // by the trailing letter: W = work, M = manifest (edition).
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
                var resp = _httpClient.Get<OpenLibraryEditionResource>(_requestBuilder.For($"books/{foreignBookId}.json").Build());
                if (resp?.Resource == null)
                {
                    return new List<Book>();
                }

                return new List<Book> { OpenLibraryEditionMapper.ToBook(resp.Resource) };
            }

            return new List<Book>();
        }

        // Matches ISearchForNewEntity.SearchForNewEntity.
        // The SPA quick-search blends authors and books in the same drop-down.
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
    }
}
