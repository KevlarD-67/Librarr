using System;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // Shape-detectors for Open Library identifiers. Hoisted out of
    // MetadataSourceFactory so LegacyMigrationService (and any future
    // consumer) can ask the same question without taking a dependency on
    // the factory.
    //
    // OL author ids are `OL\d+A` (e.g. OL26320A); work ids are `OL\d+W`
    // (e.g. OL1234W). GoodReads-shaped ids that came in from a pre-cutover
    // BookInfo DB are bare numeric strings ("3345" for Joseph Conrad), so
    // "is it shaped like an OL id?" is a strong signal that a foreign id
    // is already on the new system.
    public static class OpenLibraryIdHelper
    {
        public static bool IsAuthorId(string id)
            => id != null
               && id.StartsWith("OL", StringComparison.OrdinalIgnoreCase)
               && id.EndsWith("A", StringComparison.OrdinalIgnoreCase);

        public static bool IsWorkId(string id)
            => id != null
               && id.StartsWith("OL", StringComparison.OrdinalIgnoreCase)
               && id.EndsWith("W", StringComparison.OrdinalIgnoreCase);
    }
}
