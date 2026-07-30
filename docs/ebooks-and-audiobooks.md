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

Option 2 is the better end state; option 1 is the cheaper route to feature
parity. Either needs a decision before code — this file is deliberately not
that decision.
