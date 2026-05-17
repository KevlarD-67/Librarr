# Contributing to Librarr

Thank you for your interest in helping build Librarr.

## Before you start

* **Read [`MASTER-PLAN.md`](MASTER-PLAN.md)** to see where the project
  is in its 12-phase revival roadmap. Pick a task that lines up with
  the current phase.
* **Read [`ARCHITECTURE.md`](ARCHITECTURE.md)** for a map of the
  codebase — namespaces, layers, the metadata seam, and the provider
  plugin model.
* **Read [`CLAUDE.md`](CLAUDE.md)** if you're working alongside an AI
  assistant; it captures the project-specific quirks (identity remap,
  dual SQLite + Postgres, etc.).

## How to contribute

1. **Open an issue first** for non-trivial changes so the design can be
   discussed before code is written. Bug fixes and small cleanups can
   skip this and go straight to a PR.
2. **Fork the repo, branch off `develop`**, push your branch, open a
   pull request against `develop`.
3. **Run `./build.sh --backend && yarn build` locally** to confirm the
   build is clean before pushing. Backend tests: `./test.sh`. Frontend
   lint: `yarn lint`.
4. **Follow the existing code style.** StyleCop is enforced on the
   Linux CI leg; running `dotnet build` locally will surface most style
   violations. Frontend code goes through ESLint + Stylelint.
5. **Keep commits focused.** One logical change per commit; squash
   "fixup" commits before requesting review.

## Licensing

Contributions are accepted under GPL v3 (inbound = outbound). See
[`CLA.md`](CLA.md) — there is no CLA to sign.

## Documentation

If your change touches public behavior — config keys, API surface,
provider plugin contracts, the metadata source seam — update the
relevant doc:

* `ARCHITECTURE.md` for cross-cutting structure changes.
* `MASTER-PLAN.md` to amend the phase plan.
* Per-directory `README.md` files for narrower changes.

## Getting help

* GitHub Discussions on this repo for design questions.
* GitHub Issues for bug reports and feature requests.
* Wiki articles from upstream Readarr at
  <https://wiki.servarr.com/readarr> are still mostly accurate for
  install / operational topics, but treat the metadata-source guidance
  as obsolete.
