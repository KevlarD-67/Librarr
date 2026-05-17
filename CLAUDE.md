# CLAUDE.md

Project memory for Claude sessions working in this repo.

## Project

**Readarr** — ebook/audiobook member of the Servarr family (sibling to
Sonarr, Radarr, Lidarr). Forked from Sonarr. **Upstream is archived as of
2025-06-27** (commit `0b79d300`, "Retirement announcement"). This local
checkout reflects an earlier `develop` state — see `azure-pipelines.yml:12`
for the local version baseline. Use `git diff origin/develop` to see how
far behind the working tree is.

Full architecture map: **`ARCHITECTURE.md`** at repo root, plus per-folder
`README.md` files in every major directory.

## Identity quirk (read this first)

The csproj/assembly names are `Readarr.*` but C# **namespaces are still
`NzbDrone.*`** — set deliberately in `src/Directory.Build.props:97-99` via
a `RootNamespace` rewrite. `using Readarr.Core;` will NOT compile —
`using NzbDrone.Core;` will. Don't "fix" this; it's intentional. The
`Stylecop.ruleset:1` file even still labels itself "Rules for Radarr"
from when the ruleset was forked over.

## Common commands

```bash
# Frontend
yarn install                                    # install deps (root package.json)
yarn start                                      # webpack --watch
yarn build                                      # one-shot → _output/UI
yarn lint                                       # ESLint
yarn stylelint-linux                            # Stylelint over CSS

# Backend
./build.sh --backend --enable-extra-platforms   # full backend build (multi-RID)
./test.sh                                       # backend tests
dotnet test src/NzbDrone.Core.Test/             # one test project
dotnet run --project src/NzbDrone.Console/      # run on :8787 (HTTPS :6868)

# Single test
dotnet test src/NzbDrone.Core.Test/ --filter "FullyQualifiedName~MyClassTests"
```

CI uses the same scripts. **Note:** StyleCop only enabled on the Linux CI
leg (`azure-pipelines.yml:79`); Mac/Windows skip it.

## Stack

- **Backend:** .NET 6, ASP.NET Core, **DryIoc 5.4.3** DI
  (`src/NzbDrone.Host/Bootstrap.cs:9-10,90`), custom Dapper-based ORM in
  `NzbDrone.Core/Datastore/`, Servarr-forked FluentMigrator, dual
  **SQLite + PostgreSQL**, NLog logging. Sentry 3.31 for error reporting.
- **Frontend:** React 17 + Redux 4 (**legacy `createStore`**, not RTK),
  Webpack 5, CSS Modules via PostCSS, `@microsoft/signalr`. Partial
  JS→TS migration (~29% TS). PropTypes still enforced on `.js`.
- **Tests:** NUnit + Moq + FluentAssertions. Selenium 3.141 + ChromeDriver
  91 in `NzbDrone.Automation.Test` (years out of date — treat that suite
  as historical).

## Conventions

- **Strict build:** `TreatWarningsAsErrors=true`,
  `EnforceCodeStyleInBuild=true` (`src/Directory.Build.props:4-5`).
- **Backend file layout** per domain under `NzbDrone.Core/{Domain}/`:
  `Model/`, `{Entity}Repository.cs`, `{Entity}Service.cs`, `Commands/`,
  `Events/` (with `Handlers/`).
- **REST:** `Readarr.Api.V1/{Domain}/{Entity}Controller.cs` +
  `{Entity}Resource.cs` DTO. Manual mapping (no AutoMapper).
- **Frontend:** PascalCase folder per component, `Foo.js` + `Foo.css` +
  `FooConnector.js`. PropTypes for `.js`, TS types for `.ts/.tsx`
  (`react/prop-types: 2 / off` in `frontend/.eslintrc.js:317,365`).
- **Provider plugins** (indexers, download clients, notifications, import
  lists, metadata): all derive from a `ThingiProvider`-rooted base and
  are auto-discovered by DryIoc reflection — no manual registration.
- **Messaging:** `EventAggregator` (in-process pub/sub) + `CommandQueueManager`
  (DB-backed background queue). Handlers implement `IHandle<TEvent>` /
  `IExecute<TCommand>`.

## Where to add things

| Task | Location |
|---|---|
| New indexer | `src/NzbDrone.Core/Indexers/` — extend `HttpIndexerBase` |
| New download client | `src/NzbDrone.Core/Download/Clients/` — extend `TorrentClientBase` or `UsenetClientBase` |
| New notification | `src/NzbDrone.Core/Notifications/` — extend `NotificationBase` |
| New import list | `src/NzbDrone.Core/ImportLists/` — extend `HttpImportListBase` |
| New DB column | `src/NzbDrone.Core/Datastore/Migration/0XX_Name.cs` (FluentMigrator) + update model |
| New API endpoint | `src/Readarr.Api.V1/{Domain}/{Entity}Controller.cs` + `{Entity}Resource.cs` |
| New background job | Define `ICommand` + `IExecute<TCommand>` handler; schedule in `src/NzbDrone.Core/Jobs/TaskManager.cs` |
| New UI page | `frontend/src/{Feature}/` + route in `frontend/src/App/AppRoutes.js` (no lazy loading) |
| New Redux slice | `frontend/src/Store/Actions/{feature}Actions.js`, use the `Creators/` factories |

## Gotchas

- **Dual SQLite + Postgres:** every new query must be valid on both. Use
  `WhereBuilderSqlite` / `WhereBuilderPostgres` for predicate translation.
  Date/time types in particular have dedicated Postgres migrations.
- **`Parser/Parser.cs` is ~905 lines of regex** — most fragile file. A
  single careless change breaks many release-name patterns. There is no
  golden-corpus test fixture; treat with respect.
- **jQuery `$.ajax`** is the only HTTP client on the frontend
  (`frontend/src/Utilities/createAjaxRequest.js`). Don't introduce `fetch`
  or `axios` without a plan to migrate the whole layer.
- **`NzbDrone.Windows` vs `NzbDrone.Mono`** — platform shims, only one is
  active at runtime. Don't reference them directly from `Core/`; let DI
  pick via `OsInfo.IsWindows`.
- **`win-arm64` is intentionally NOT in the RID list**
  (`src/Directory.Build.props:11`). Windows-on-ARM is unsupported.
- **`config.xml` reload-on-change is disabled** (`Bootstrap.cs:237`) —
  changes to bootstrap config require a restart.
- **No pre-commit hooks** — lint runs in CI only.
- **Architecture is now historical:** project is archived upstream, so any
  changes here will diverge from the rest of the Servarr ecosystem.

## Process modes

`Bootstrap.GetApplicationMode` (`src/NzbDrone.Host/Bootstrap.cs:186-227`)
picks one of: `Help`, `RegisterUrl`, `InstallService`, `UninstallService`,
`Service` (Windows service), `Interactive` (tray/console). The
self-updater is a separate exe (`src/NzbDrone.Update/`).

## Default ports

- HTTP `8787`, HTTPS `6868` (`Bootstrap.cs:135-136`).
- Override via `config.xml` (`Port`, `SslPort`, `BindAddress`).

## Documentation here

- **`ARCHITECTURE.md`** — full map, ~1100 lines, 10 sections.
- **`src/*/README.md` and `frontend/src/*/README.md`** — per-folder
  signposts pointing back to ARCHITECTURE.md sections.
- **`README.md`** — upstream marketing page (and at develop HEAD, the
  retirement announcement).
