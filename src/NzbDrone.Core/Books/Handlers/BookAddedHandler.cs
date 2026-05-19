using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public class BookAddedHandler : IHandle<BookAddedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;

        public BookAddedHandler(IManageCommandQueue commandQueueManager)
        {
            _commandQueueManager = commandQueueManager;
        }

        public void Handle(BookAddedEvent message)
        {
            if (message.DoRefresh)
            {
                // Refresh only the just-added book — not its author's full
                // works list. Upstream Readarr's model is "add a book and
                // we'll pull in the author's whole discography" (filtered
                // by the metadata profile). Librarr's intent is opt-in:
                // the user sees only books they explicitly added. The
                // author entity is created (so the book has a parent),
                // but additional books are only ingested when the user
                // explicitly clicks "Refresh Author" or adds another
                // book by title.
                _commandQueueManager.Push(new RefreshBookCommand(message.Book.Id));
            }
        }
    }
}
