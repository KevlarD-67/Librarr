using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface IBookIdMappingRepository : IBasicRepository<BookIdMapping>
    {
        BookIdMapping FindByGoodreadsId(string goodreadsId);
        List<BookIdMapping> FindByGoodreadsIds(List<string> goodreadsIds);
        List<BookIdMapping> GetLowConfidence(double threshold);
    }

    public class BookIdMappingRepository : BasicRepository<BookIdMapping>, IBookIdMappingRepository
    {
        public BookIdMappingRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public BookIdMapping FindByGoodreadsId(string goodreadsId)
        {
            return Query(x => x.GoodreadsId == goodreadsId).SingleOrDefault();
        }

        public List<BookIdMapping> FindByGoodreadsIds(List<string> goodreadsIds)
        {
            return Query(x => goodreadsIds.Contains(x.GoodreadsId));
        }

        public List<BookIdMapping> GetLowConfidence(double threshold)
        {
            // Surfaced by the Phase 5 wizard's "needs manual review" step.
            return Query(x => x.Confidence < threshold);
        }
    }
}
