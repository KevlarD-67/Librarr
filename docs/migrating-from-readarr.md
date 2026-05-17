# Migrating from Readarr to Librarr

This guide walks an existing Readarr user through the upgrade to Librarr
1.0.0-beta.1, the fork that rebuilds the metadata layer on Open Library.
If you've never run Readarr, you don't need this guide — install Librarr
fresh per the README.

## TL;DR

1. **Back up** `Readarr/config.xml` and the `Readarr.db` SQLite file.
2. **Install Librarr** alongside Readarr (the installer uses a fresh
   GUID, so the two coexist).
3. **Point Librarr at your Readarr data folder** — same root, same
   library file layout.
4. **Open Settings → Metadata → Switch Metadata Source** and click
   *Start reidentify*. The wizard remaps every Goodreads ID to its OL
   equivalent and stores the mapping in the new `BookIdMapping` table.
5. **Review System → Logs** for `Low-confidence mapping for book` lines
   — those are titles that need manual confirmation. They stay
   visible/usable in Librarr; reidentify just flagged them as uncertain.
6. **Uninstall Readarr** once you're satisfied the migration carried
   over correctly.

## What changes

| Area | Readarr | Librarr |
|---|---|---|
| Primary metadata source | BookInfo (Goodreads-derived) | Open Library |
| Series metadata | Goodreads listopia | Wikidata SPARQL (P179 / P1545) |
| Audiobook enrichment | None | audnex.us (opt-in) |
| Import lists | Goodreads bookshelves / listopia | OL subject tags + author works |
| Foreign IDs in DB | Goodreads numeric IDs | OL keys (`OL12345W` / `OL12345M`) |
| Files / monitoring / history | Preserved as-is during migration | Preserved as-is during migration |
| Quality profiles | Preserved | Preserved |
| Download client + indexer config | Preserved | Preserved |

The reidentify wizard rewrites the metadata layer only. Everything else
in your library — paths, downloaded files, monitoring flags, profile
assignments, history rows — stays bit-identical.

## What can go wrong

* **Low-confidence matches.** ~5–15% of typical libraries land at
  `<0.7` confidence and need manual confirmation. The wizard surfaces
  them in System → Logs (Phase 9 will move them into a dedicated review
  screen).
* **Books with no OL presence.** Niche self-published or out-of-print
  works may not exist in Open Library. The original Goodreads-ID row in
  `BookIdMapping` stays unmapped (`OpenLibraryWorkId` is NULL). You can
  either add them manually (Settings → Metadata → Add by OL ID) or
  leave them as-is — the existing books are not deleted.
* **Audiobook narrators.** Librarr's domain model doesn't yet have a
  Narrator concept (Phase 7b TODO). The audnex augmenter pulls narrator
  data but stores it only in the edition overview text until the
  domain expands.
* **Switching back.** Setting `MetadataSourceType = "BookInfo"` in
  Settings → Metadata reverses the routing (the original Goodreads-ID
  rows are still in `Books.ForeignBookId`). The `BookIdMapping` rows
  stay intact so a second forward-migration is fast.

## Sanity-check checklist

After running the wizard:

- [ ] `select count(*) from BookIdMapping where OpenLibraryWorkId is not null;`
      reports >= 85% of your total `Books` row count.
- [ ] Top 50 most-monitored authors all show a populated overview /
      birth-date / cover in the UI.
- [ ] A random sample of 20 books across the library show the same
      monitoring flags as before the migration.
- [ ] `select count(*) from BookFiles where BookId is null;` is 0
      (no file orphaned by the reidentify pass).

If any of the four fails, **don't uninstall Readarr yet.** Open an
issue on the Librarr GitHub with the log slice and the wizard's
exit summary. The `BookIdMapping` table is the audit trail — every
row records the confidence + source method for forensics.

## Rolling back

If the migration goes badly:

1. Stop Librarr.
2. In Settings → Metadata, set `MetadataSourceType = "BookInfo"`.
3. Start Readarr again. Both apps see the same DB; switching the
   metadata-source pointer reverses the routing.
4. Optionally, run the FluentMigrator down-migration for 041:
   `dotnet ef database update 040 --project src/NzbDrone.Core` (or
   manually `drop table BookIdMapping;` if you don't have the EF
   tooling installed).

Librarr 1.0.0-beta.1 keeps BookInfoProxy in the binary; the rollback
path remains supported until Phase 10 deletes the legacy proxy.
