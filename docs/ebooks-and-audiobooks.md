# Ebooks and audiobooks in one instance

**Short answer:** one instance handles both formats, but not for the same
author at the same time. If you want ebooks *and* audiobooks of the same
authors, run two instances.

This page exists because the constraint is real but the usual one-line summary
of it — "single format per instance" — is not accurate, and the inaccuracy
sends people looking in the wrong place when it bites them.

## What is actually true

Both format families live in the same instance. `Quality` defines PDF, MOBI,
EPUB and AZW3 (ids 1–4) alongside MP3, FLAC and M4B (ids 10–13), and a quality
profile can contain any mix of them. Root folders each carry their own
`DefaultQualityProfileId` and `DefaultMetadataProfileId`, so an `/ebooks` root
folder and an `/audiobooks` root folder can default to entirely different
profiles. `BookFile` is keyed on `EditionId` and carries `Part`/`PartCount`, so
multi-file audiobooks are representable.

So nothing in the schema forces one format per instance.

## What actually constrains you

**An author has exactly one quality profile, and every download decision reads
it from the author.**

`Author` has a single `QualityProfileId` and a single `RootFolderPath`. Every
specification in the decision engine resolves the profile the same way —
`subject.Author.QualityProfile` — including `QualityAllowedByProfileSpecification`,
`CutoffSpecification`, `UpgradeAllowedSpecification`, `UpgradeDiskSpecification`
and `CustomFormatAllowedByProfileSpecification`, plus the ranking in
`DownloadDecisionComparer`.

That single profile is one ordered ranking. Put EPUB and M4B in it together and
you have not asked for both — you have told Librarr that M4B and EPUB are
alternatives for the same slot, ranked against each other. Whichever ranks
higher wins, and the upgrade path will treat the other as something to replace.
There is no way to express "keep one EPUB *and* one M4B of this book".

The practical consequence is per-author, not per-instance: an author lives in
one root folder under one profile. You can have an ebook author and an
audiobook author in the same instance quite happily. You cannot have the same
author both ways.

## Recommended setups

**Both formats, same authors** — two instances, separate config volumes and
ports, one root folder each. This is the established Servarr pattern and what
most people mean by "run separate containers". Point each at its own download
client category.

**Both formats, different authors** — one instance is fine. Create two root
folders with different default quality profiles, and add each author into the
one that matches the format you want for them.

**One format** — one instance, nothing special to do.

## Lifting the constraint

Worth doing, and it would be a genuine differentiator: the closest comparable
project handles both formats in one instance and is closed-source. Two credible
designs, neither small:

1. **Per-format profiles on the author.** Replace the single `QualityProfileId`
   with a profile per format family, and have the specifications select by the
   parsed release's format. Touches every specification listed above plus the
   comparer, the author schema and the author UI. Conceptually simple, wide
   blast radius, and the upgrade/cutoff logic needs to become per-format too.

2. **Treat format as part of what is monitored.** Monitor "Elantris (ebook)"
   and "Elantris (audiobook)" as distinct monitored things over the same book,
   each with its own profile and file. Closer to how users describe what they
   want, and it makes "I have the ebook, now get me the audiobook" expressible.
   Larger change: monitoring, wanted/missing, and the import path all currently
   assume one monitored slot per edition.

## Decision: option 1 first (2026-07-30)

Option 2 remains the better end state. It is not, however, an alternative to
option 1 — it *contains* it. Monitoring "Elantris (ebook)" and "Elantris
(audiobook)" separately requires each monitored format to carry its own
profile, which is exactly what option 1 builds. Doing option 1 first is not a
detour to be undone later.

The deciding argument is what each change breaks. Option 1's blast radius is
wide but mechanical: it is a change to how a profile is resolved, and the
compiler plus the specification fixtures find every site. Option 2 changes what
a monitored thing *is*, and monitoring, wanted/missing and the import path all
encode "one monitored slot per edition" as an unstated assumption — semantic
breakage that nothing in the toolchain catches. On a fork field-validated on a
single deployment with no frontend test suite at all, take the change the
toolchain can verify.

### What makes this tractable

Three things that are already true, and are why this is a smaller job than the
prose above implies:

* **Storage already allows both.** `BookFile` carries `EditionId` *and*
  `Part`, and `Book` lazy-loads a *list* of files. Nothing in the schema says
  one file per book. The constraint is entirely that a single ordered profile
  makes an EPUB and an M4B compete for one slot.
* **Format is already derivable.** `Quality` ids split cleanly — 0-4 text
  (Unknown, PDF, MOBI, EPUB, AZW3), 10-13 audio (MP3, FLAC, M4B,
  UnknownAudio), with 5-9 left as a deliberate gap. A `Format` property on
  `Quality` is a pure function over the existing ids; no migration, no data
  backfill.
* **Every read site already knows the format.** All sixteen reads are
  `subject.Author.QualityProfile.Value` where `subject` is a `RemoteBook`
  carrying `ParsedBookInfo.Quality`, or an import item carrying
  `Item.Quality`. None of them has to be taught something new — they have the
  quality in hand at the point they resolve the profile.

### Work breakdown

1. **`Quality.Format`** — derived `{ Text, Audio }` from the id ranges, plus a
   fixture pinning every existing quality to its family so a future quality
   added in the 5-9 gap fails loudly rather than silently classifying as text.
2. **`Author.AudiobookQualityProfileId`** — one nullable column, one
   FluentMigrator migration. **Null means "single-format author"** and
   resolves to `QualityProfileId` for every format, so existing installs
   behave exactly as they do today and the migration needs no backfill.
3. **The resolution seam** — replace the sixteen `Author.QualityProfile.Value`
   reads with one call that takes the quality. This is the whole change; steps
   1, 2 and 4 exist to serve it. Do it as a single mechanical commit so the
   diff is reviewable as "did every site get the same treatment".
4. **Cutoff and upgrade** — fall out of step 3 rather than needing their own
   work, since each format resolves its own profile and therefore its own
   cutoff. Worth its own fixture proving an M4B import does not replace an
   existing EPUB.
5. **UI** — an optional second profile picker on the author edit form and in
   root-folder defaults. Kept last deliberately: everything above is testable
   without it.

Sequencing note: steps 1-3 are worthless individually and valuable together,
so they want to land as one reviewed branch, not trickled onto `main`.
