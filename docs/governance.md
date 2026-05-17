# Librarr governance

The fork that survives a maintainer rotation has its operating model
written down before it's needed. This document is that.

## Decision-making

* **Code changes** land via pull request. One non-author approval is
  required for any change that touches more than docs or per-folder
  README files. Two approvals required for changes to:
  - `src/NzbDrone.Core/Datastore/Migration/` (DB schema)
  - `src/NzbDrone.Core/MetadataSource/` (the metadata seam)
  - `src/NzbDrone.Core/Configuration/` (config schema)
  - `MASTER-PLAN.md`, `ARCHITECTURE.md`, `CLAUDE.md` (architectural docs)

* **Roadmap changes** (additions, reorderings) land via PR against
  `docs/roadmap.md` with the rationale in the PR body. No special
  approval bar.

* **Release tagging** is the responsibility of the release manager (see
  Roles below). Tag format: `v1.0.0-beta.N` for beta, `v1.0.0` for
  stable, then SemVer from there.

* **Security disclosures** go through GitHub private security
  advisories (`SECURITY.md`). The first responder on the on-call
  rotation triages; a fix goes out within 7 days of confirmation for
  any vulnerability rated High or Critical.

## Roles

Per-role headcount minimums are aspirational targets, not hard gates.
When fewer people hold a role than the target, the role's
responsibilities fall back to the most-recently-active maintainer.

| Role | Minimum | Responsibilities |
|---|---|---|
| Maintainer | 2 active | Merge rights on `develop`. Quarterly review of open PRs. |
| Release manager | 1 active | Tags releases, drafts changelogs, runs the signing dance. Rotates quarterly. |
| Security responder | 1 active | First-touch on GitHub security advisories. Coordinates disclosure. |
| Metadata steward | 1 active | Watches OL upstream API changes. Maintains the SPARQL series queries. |

## Bus factor

The fork's goal is **two active maintainers minimum** at all times. If
the active count drops to one, the remaining maintainer's responsibility
is to recruit a second within 90 days — through community outreach, an
"is anyone interested in helping" GitHub issue, or by handing the keys
to an interested party.

If no second maintainer materializes in 90 days, the existing maintainer
should declare a maintenance-mode pause: only security patches accepted,
no feature work, until the bus factor recovers.

## Funding

Librarr ships under GPL v3 with no commercial offering. Donations route
via the channels declared in `.github/FUNDING.yml` (Open Collective
slot intentionally empty pending a Librarr collective).

Costs that funding should cover:

1. CI runtime (GitHub Actions free tier covers most use cases; only
   matters at scale).
2. Code-signing certificates (Authenticode for Windows binaries, Apple
   Developer for the macOS .app — both annual).
3. Docker Hub Org subscription (optional; only matters if image
   download volume exceeds the free tier).
4. Sentry-equivalent error-tracking subscription (also optional;
   self-hosted Sentry is on the table).
5. Domain registration (if/when the fork picks one — current setup
   uses github.com/Librarr/Librarr as the canonical URL).

Out of scope for funding:

* Salary for maintainers. Librarr is a volunteer project.

## Public sync

* **README badge for build status** — once the GHA workflow is verified
  green for two consecutive weeks.
* **Quarterly state-of-the-fork** post in `docs/state-of-the-fork/`
  with: open PR count, closed-PR-this-quarter count, recent
  contributors, roadmap delta, retro on what didn't ship.

## Acknowledging upstream

Librarr's codebase derives from `Readarr/Readarr` (forked from
`Sonarr/Sonarr` in turn). The retirement notice in upstream README
suggested forks were welcome, and the Servarr team has not raised
trademark objections to the Librarr rename. If they do, the maintainers
will rename again rather than litigate.

The `MASTER-PLAN.md` revival plan was authored by the Librarr fork's
initial maintainer. The `Bookarr` placeholder name that appears in
some Phase 0/Phase 5 code samples is a known carry-over and gets
swapped for `Librarr` opportunistically.
