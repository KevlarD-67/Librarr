using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Books
{
    public interface IRefreshEditionService
    {
        bool RefreshEditionInfo(List<Edition> add, List<Edition> update, List<Tuple<Edition, Edition>> merge, List<Edition> delete, List<Edition> upToDate, List<Edition> remoteEditions, bool forceUpdateFileTags);
    }

    public class RefreshEditionService : IRefreshEditionService
    {
        private readonly IEditionService _editionService;
        private readonly IMetadataTagService _metadataTagService;
        private readonly INarratorService _narratorService;
        private readonly Logger _logger;

        public RefreshEditionService(IEditionService editionService,
            IMetadataTagService metadataTagService,
            INarratorService narratorService,
            Logger logger)
        {
            _editionService = editionService;
            _metadataTagService = metadataTagService;
            _narratorService = narratorService;
            _logger = logger;
        }

        public bool RefreshEditionInfo(List<Edition> add, List<Edition> update, List<Tuple<Edition, Edition>> merge, List<Edition> delete, List<Edition> upToDate, List<Edition> remoteEditions, bool forceUpdateFileTags)
        {
            var updateList = new List<Edition>();

            // for editions that need updating, just grab the remote edition and set db ids
            foreach (var edition in update)
            {
                var remoteEdition = remoteEditions.Single(e => e.ForeignEditionId == edition.ForeignEditionId);
                edition.UseMetadataFrom(remoteEdition);

                // make sure title is not null
                edition.Title = edition.Title ?? "Unknown";
                updateList.Add(edition);
            }

            _editionService.DeleteMany(delete.Concat(merge.Select(x => x.Item1)).ToList());
            _editionService.UpdateMany(updateList);

            // Materialize narrator strings into the normalized Narrators /
            // EditionNarrators join (migration 043). Skip editions whose
            // string is blank — they don't have audiobook narrator data
            // and the join may already be empty. Both add and updateList
            // have valid edition Ids at this point (add via InsertMany
            // upstream, updateList via UpdateMany on the line above).
            foreach (var edition in add.Concat(updateList))
            {
                if (edition.Narrators.IsNotNullOrWhiteSpace())
                {
                    var names = edition.Narrators.Split(',').Select(n => n.Trim());
                    _narratorService.SetNarratorsForEdition(edition.Id, names);
                }
            }

            var tagsToUpdate = updateList;
            if (forceUpdateFileTags)
            {
                _logger.Debug("Forcing tag update due to Author/Book/Edition updates");
                tagsToUpdate = updateList.Concat(upToDate).ToList();
            }

            _metadataTagService.SyncTags(tagsToUpdate);

            return add.Any() || delete.Any() || updateList.Any() || merge.Any();
        }
    }
}
