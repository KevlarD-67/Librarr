# State of the fork — quarterly writeups

`docs/governance.md` (Public sync section) commits to a quarterly
"state of the fork" post. This directory is where those posts land.

A writeup serves two purposes:

1. External: tells readers what Librarr's last quarter looked like
   without making them read commit logs.
2. Internal: forces a 90-day retrospective on what shipped, what
   slipped, and where the bus-factor stands.

## Filename convention

`YYYY-Q1.md`, `YYYY-Q2.md`, `YYYY-Q3.md`, `YYYY-Q4.md`.

Quarters are calendar quarters (Q1 = Jan–Mar, etc.), not fiscal.

## Cadence

Each writeup is published **within 14 days of the quarter's end** — so
Q1 by 2026-04-14, Q2 by 2026-07-14, and so on.

If a quarter is skipped, `docs/governance.md` "Bus factor" says the
remaining maintainer has 90 days to recruit a second one. A missed
writeup is a signal that countdown may be running — though as of
2026-08-02 that section names its own start date and deadline, so the
writeup is no longer the only way to notice.

### Partial quarters (decided 2026-08-02)

**The first writeup owed is 2026-Q2, not 2026-Q1.** Librarr's first
commit is 2026-05-16 and the repository was created 2026-05-19, so Q1
2026 predates the project entirely. No Q1 post is owed and none is
missing.

Q2 is therefore a 45-day quarter for this project, and a thin one.
Three options were weighed when it came up 19 days late:

1. **Publish it thin** — chosen. A short honest post costs an hour and
   keeps the cadence's record continuous.
2. Merge Q2 into Q3 and publish one combined post in October —
   rejected. A quarter that silently absorbs into the next one is
   precisely the drift the cadence exists to catch, and `governance.md`
   reads a skipped quarter as a bus-factor signal. Skipping one to save
   effort would fire that signal for the wrong reason.
3. Amend the cadence to begin at the first *full* quarter (Q3) —
   rejected for the same reason, plus it would mean the release of
   1.0.0-beta went unrecorded in the only place that tracks quarters.

The rule that falls out, for anyone hitting this again: **a quarter in
which the project existed at all gets a post, however short.** State
the real window in the opening line when it isn't a full quarter.

## Template

Copy this into each new `YYYY-Qn.md`:

```markdown
# State of the fork — YYYY Qn

_Published: YYYY-MM-DD_

## Numbers

- Open PRs at quarter end: N
- PRs closed this quarter: N (merged: N, declined: N)
- Recent contributors: @alice, @bob, @carol
- Active maintainers: N (target per governance.md: ≥ 2)

## Roadmap delta

What moved between buckets in `docs/roadmap.md` this quarter:

- Now → Done: ...
- Soon → Now: ...
- Later → Soon: ...
- New entries: ...

## Retro: what didn't ship

Items that were planned for this quarter and slipped. One sentence
each on why — useful context for next quarter's planning.

- ...

## Operating costs

Cite `docs/governance.md` "Funding" categories:

- CI runtime: $X (or "GHA free tier, $0")
- Code-signing certs: $X
- Docker Hub: $X
- Error-tracking subscription: $X
- Domain: $X
- **Total this quarter:** $X

## Asks

Where help is most useful right now — review bandwidth, testing on a
specific platform, infra access, anything that unblocks the next
quarter's roadmap.

- ...
```

## Backlinks

- [`docs/governance.md`](../governance.md) — defines the cadence and
  the bus-factor consequences of skipping a writeup.
- [`docs/roadmap.md`](../roadmap.md) — the rolling priority list this
  writeup reports deltas against.
