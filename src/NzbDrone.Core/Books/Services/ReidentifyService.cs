using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.OpenLibrary;

namespace NzbDrone.Core.Books
{
    public interface IReidentifyService
    {
    }

    public class ReidentifyService : IReidentifyService, IExecute<ReidentifyLibraryCommand>
    {
        // High-confidence cut. Anything below this surfaces in the Phase 5
        // wizard's manual-review step.
        private const double HighConfidence = 0.85;
        private const double MediumConfidence = 0.70;

        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly IBookIdMappingRepository _mappingRepo;
        private readonly OpenLibraryProxy _openLibrary;
        private readonly Logger _logger;

        public ReidentifyService(IAuthorService authorService,
                                 IBookService bookService,
                                 IEditionService editionService,
                                 IBookIdMappingRepository mappingRepo,
                                 OpenLibraryProxy openLibrary,
                                 Logger logger)
        {
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _mappingRepo = mappingRepo;
            _openLibrary = openLibrary;
            _logger = logger;
        }

        public void Execute(ReidentifyLibraryCommand command)
        {
            _logger.ProgressInfo("Starting library reidentification onto Open Library");

            var authors = _authorService.GetAllAuthors();
            _logger.Info("Reidentifying {0} authors", authors.Count);
            foreach (var author in authors)
            {
                try
                {
                    MapAuthor(author);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to reidentify author {0}", author);
                }
            }

            var books = _bookService.GetAllBooks();
            _logger.Info("Reidentifying {0} books", books.Count);
            foreach (var book in books)
            {
                try
                {
                    MapBook(book);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to reidentify book {0}", book);
                }
            }

            // TODO Phase 5b: re-run identification pipeline against
            // BookFile parsed tags (MediaFiles/BookImport/Identification/
            // CandidateService). File tags should TRUMP server lookups
            // when both disagree — that's the authoritative path.
            _logger.ProgressInfo("Library reidentification finished (file-tag pass deferred)");
        }

        private void MapAuthor(Author author)
        {
            var goodreadsId = author.ForeignAuthorId;
            if (goodreadsId.IsNullOrWhiteSpace())
            {
                return;
            }

            if (_mappingRepo.FindByGoodreadsId(goodreadsId) != null)
            {
                return;
            }

            var name = author.Name;
            if (name.IsNullOrWhiteSpace())
            {
                return;
            }

            var candidates = _openLibrary.SearchForNewAuthor(name);
            if (candidates.Count == 0)
            {
                _logger.Debug("No OL author candidates for '{0}'", name);
                return;
            }

            var best = PickBestAuthor(candidates, author);
            if (best == null)
            {
                return;
            }

            var confidence = ScoreAuthorMatch(best, author);

            _mappingRepo.Insert(new BookIdMapping
            {
                GoodreadsId = goodreadsId,
                OpenLibraryWorkId = null,
                OpenLibraryEditionId = best.ForeignAuthorId,
                Confidence = confidence,
                Source = BookIdMappingSource.TitleAuthor,
                CreatedUtc = DateTime.UtcNow
            });
        }

        private void MapBook(Book book)
        {
            if (book.ForeignBookId.IsNullOrWhiteSpace())
            {
                return;
            }

            if (_mappingRepo.FindByGoodreadsId(book.ForeignBookId) != null)
            {
                return;
            }

            // ISBN-13 → ASIN → title+author fallback chain. The fallback
            // path matches MASTER-PLAN.md Phase 5.
            var editions = _editionService.GetEditionsByBook(book.Id);
            var isbn13 = editions.Select(e => e.Isbn13).FirstOrDefault(i => i.IsNotNullOrWhiteSpace());
            if (isbn13.IsNotNullOrWhiteSpace())
            {
                var hits = _openLibrary.SearchByIsbn(isbn13);
                if (TryWriteMapping(book, hits, BookIdMappingSource.Isbn, 0.95))
                {
                    return;
                }
            }

            var asin = editions.Select(e => e.Asin).FirstOrDefault(a => a.IsNotNullOrWhiteSpace());
            if (asin.IsNotNullOrWhiteSpace())
            {
                var hits = _openLibrary.SearchByAsin(asin);
                if (TryWriteMapping(book, hits, BookIdMappingSource.Asin, 0.88))
                {
                    return;
                }
            }

            var authorName = book.Author?.Value?.Name;
            var titleHits = _openLibrary.SearchForNewBook(book.Title, authorName, false);
            TryWriteMapping(book, titleHits, BookIdMappingSource.TitleAuthor, ScoreTitleMatch(titleHits, book));
        }

        private bool TryWriteMapping(Book book, List<Book> hits, string source, double confidence)
        {
            var hit = hits?.FirstOrDefault();
            if (hit == null || hit.ForeignBookId.IsNullOrWhiteSpace())
            {
                return false;
            }

            var primaryEdition = hit.Editions?.Value?.FirstOrDefault(e => e.Monitored)
                                 ?? hit.Editions?.Value?.FirstOrDefault();

            _mappingRepo.Insert(new BookIdMapping
            {
                GoodreadsId = book.ForeignBookId,
                OpenLibraryWorkId = hit.ForeignBookId,
                OpenLibraryEditionId = primaryEdition?.ForeignEditionId,
                Confidence = confidence,
                Source = source,
                CreatedUtc = DateTime.UtcNow
            });

            if (confidence < MediumConfidence)
            {
                _logger.Info("Low-confidence mapping for book '{0}' (confidence {1:F2}) — needs manual review", book.Title, confidence);
            }

            return true;
        }

        private static Author PickBestAuthor(List<Author> candidates, Author target)
        {
            // Prefer name-equality match; otherwise first result.
            // TODO Phase 5b: weight by birth/death year overlap when known.
            return candidates.FirstOrDefault(c => string.Equals(c.Name, target.Name, StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault();
        }

        private static double ScoreAuthorMatch(Author candidate, Author target)
        {
            // Exact-name → 0.9, anything else → 0.5 (manual review threshold).
            // The Phase 5 wizard's MediumConfidence cutoff is 0.7.
            if (string.Equals(candidate.Name, target.Name, StringComparison.OrdinalIgnoreCase))
            {
                return 0.90;
            }

            return 0.50;
        }

        private static double ScoreTitleMatch(List<Book> hits, Book target)
        {
            var first = hits?.FirstOrDefault();
            if (first == null)
            {
                return 0;
            }

            if (string.Equals(first.Title?.Trim(), target.Title?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return HighConfidence;
            }

            return 0.55;
        }
    }
}
