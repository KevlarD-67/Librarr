# distribution/docker — Librarr docker images

Local-only docker artifacts for the `1.0.0-beta` release. **No image is
published to any public registry yet** — build locally and run from
your own image.

## Files

| File | Purpose |
|---|---|
| `Dockerfile` | Self-contained multi-stage build: compiles backend (`dotnet publish`) + frontend (`yarn build`) inside the image, then assembles a small runtime layer on `aspnet:6.0-alpine`. No local toolchain required. |
| `Dockerfile.prebuilt` | Thin packaging layer for when you already ran `./build.sh` and `yarn build` locally — copies `_output/` straight into the runtime image. Fast iteration, but requires the host toolchain. |

## Build

From the repo root:

```bash
docker build \
  -f distribution/docker/Dockerfile \
  --build-arg DOTNET_RID=linux-musl-x64 \
  -t librarr/librarr:1.0.0-beta \
  .
```

Adjust `DOTNET_RID` if you target a non-musl runtime
(`linux-x64`, `linux-arm64`, etc.). `win-arm64` is **not** in the RID
list and is unsupported (`src/Directory.Build.props:11`).

## Run

Minimum invocation:

```bash
docker run -d \
  --name librarr \
  --restart unless-stopped \
  -e PUID=1000 -e PGID=1000 -e TZ=Etc/UTC -e UMASK_SET=002 \
  -p 8787:8787 \
  -v /path/to/librarr/config:/config \
  -v /path/to/your/library:/books \
  -v /path/to/downloads/completed:/downloads/completed \
  librarr/librarr:1.0.0-beta
```

Volume layout the container expects:

| Mount | Purpose |
|---|---|
| `/config` | Persistent app data — `config.xml`, `librarr.db`, `Logs/`. |
| `/books` (or whatever you choose) | Your library root. Configure the root folder inside Librarr's UI to match. |
| `/downloads/completed` | Where your download client(s) drop completed grabs. Configure Remote Path Mappings if the path-as-seen-by-the-download-client differs from the path-as-seen-by-Librarr. |

Default ports: **8787** HTTP, **6868** HTTPS
(`src/NzbDrone.Host/Bootstrap.cs:135-136`). Override via `config.xml`
(`Port`, `SslPort`, `BindAddress`); `config.xml` reload-on-change is
disabled (`Bootstrap.cs:237`), so changes require a restart.

## Migrating from Readarr

Point `/config` at a copy of your old Readarr `config/` directory and
start the container. The first-boot `LegacyMigrationService` handles
the rest — see ["Migrating from Readarr"](../../README.md#migrating-from-readarr).
A persisted marker in `config.xml` (`LegacyMigrationCompleted`)
prevents re-runs on subsequent restarts.
