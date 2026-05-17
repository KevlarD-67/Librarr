using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Model;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Goodreads;
using NzbDrone.Core.MetadataSource.OpenLibrary;

namespace NzbDrone.Core.MetadataSource
{
    // Dispatcher between the legacy BookInfo (Goodreads-derived) proxy and
    // the new OpenLibrary proxy, selected by IConfigService.MetadataSourceType.
    //
    // This factory is the ONLY class in the codebase that declares the
    // IProvide* / ISearchForNew* interfaces post-Phase-5 — by design.
    // BookInfoProxy and OpenLibraryProxy expose the same method shapes
    // but do not declare the interfaces themselves; that prevents
    // DryIoc's RegisterMany (Composition/Extensions.cs:31) from binding
    // two impls per interface and producing nondeterministic resolution.
    //
    // Per master plan Phase 5: switching from BookInfo → OpenLibrary on a
    // live install is gated on the reidentify wizard, which writes
    // BookIdMapping rows BEFORE flipping the config value. Consumers
    // never need to handle a "wrong proxy" state at runtime — once the
    // wizard commits, the factory routes consistently.
    public class MetadataSourceFactory
        : IProvideAuthorInfo,
          IProvideBookInfo,
          IProvideSeriesInfo,
          IProvideListInfo,
          ISearchForNewAuthor,
          ISearchForNewBook,
          ISearchForNewEntity
    {
        public const string BookInfoSource = "BookInfo";
        public const string OpenLibrarySource = "OpenLibrary";

        private readonly IConfigService _configService;
        private readonly BookInfoProxy _bookInfo;
        private readonly OpenLibraryProxy _openLibrary;
        private readonly GoodreadsProxy _goodreadsProxy;
        private readonly OpenLibrarySeriesProxy _openLibrarySeries;
        private readonly Logger _logger;

        public MetadataSourceFactory(IConfigService configService,
                                     BookInfoProxy bookInfo,
                                     OpenLibraryProxy openLibrary,
                                     GoodreadsProxy goodreadsProxy,
                                     OpenLibrarySeriesProxy openLibrarySeries,
                                     Logger logger)
        {
            _configService = configService;
            _bookInfo = bookInfo;
            _openLibrary = openLibrary;
            _goodreadsProxy = goodreadsProxy;
            _openLibrarySeries = openLibrarySeries;
            _logger = logger;
        }

        private bool IsOpenLibrary => _configService.MetadataSourceType.EqualsIgnoreCase(OpenLibrarySource);

        // IProvideAuthorInfo
        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
            => IsOpenLibrary
                ? _openLibrary.GetAuthorInfo(foreignAuthorId, useCache)
                : _bookInfo.GetAuthorInfo(foreignAuthorId, useCache);

        public HashSet<string> GetChangedAuthors(DateTime startTime)
            => IsOpenLibrary
                ? _openLibrary.GetChangedAuthors(startTime)
                : _bookInfo.GetChangedAuthors(startTime);

        // IProvideBookInfo
        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
            => IsOpenLibrary
                ? _openLibrary.GetBookInfo(id)
                : _bookInfo.GetBookInfo(id);

        // IProvideSeriesInfo
        public SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true)
            => IsOpenLibrary
                ? _openLibrarySeries.GetSeriesInfo(foreignSeriesId, useCache)
                : _goodreadsProxy.GetSeriesInfo(foreignSeriesId, useCache);

        // IProvideListInfo
        public ListInfo GetListInfo(string foreignListId, int page, bool useCache = true)
            => IsOpenLibrary
                ? _openLibrarySeries.GetListInfo(foreignListId, page, useCache)
                : _goodreadsProxy.GetListInfo(foreignListId, page, useCache);

        // ISearchForNewAuthor
        public List<Author> SearchForNewAuthor(string title)
            => IsOpenLibrary
                ? _openLibrary.SearchForNewAuthor(title)
                : _bookInfo.SearchForNewAuthor(title);

        // ISearchForNewBook
        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
            => IsOpenLibrary
                ? _openLibrary.SearchForNewBook(title, author, getAllEditions)
                : _bookInfo.SearchForNewBook(title, author, getAllEditions);

        public List<Book> SearchByIsbn(string isbn)
            => IsOpenLibrary ? _openLibrary.SearchByIsbn(isbn) : _bookInfo.SearchByIsbn(isbn);

        public List<Book> SearchByAsin(string asin)
            => IsOpenLibrary ? _openLibrary.SearchByAsin(asin) : _bookInfo.SearchByAsin(asin);

        public List<Book> SearchByForeignBookId(string foreignBookId, bool getAllEditions)
            => IsOpenLibrary
                ? _openLibrary.SearchByForeignBookId(foreignBookId, getAllEditions)
                : _bookInfo.SearchByForeignBookId(foreignBookId, getAllEditions);

        // ISearchForNewEntity
        public List<object> SearchForNewEntity(string title)
            => IsOpenLibrary
                ? _openLibrary.SearchForNewEntity(title)
                : _bookInfo.SearchForNewEntity(title);
    }
}
