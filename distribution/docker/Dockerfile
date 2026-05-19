# Librarr Docker image — self-contained multi-stage build (Phase 9b).
#
# Build context: repo root.
#   docker build -f distribution/docker/Dockerfile -t librarr/librarr:1.0.0-beta.1 .
#
# Compiles backend + frontend INSIDE the image — no local .NET / Node
# toolchain required. If you already build locally and just want to
# package _output/, use distribution/docker/Dockerfile.prebuilt.
#
# Stages:
#   1. backend-build  → dotnet publish for ${DOTNET_RID}
#   2. frontend-build → yarn install + yarn build → _output/UI
#   3. runtime        → aspnet:6.0-alpine, copies from 1 + 2
#
# Status: skeleton — the layer graph and stage flow are wired, but a
# full smoke (build → `docker run` → web UI loads) hasn't been run on
# this fork yet. If you build this and hit problems, file an issue
# rather than working around them silently.
#
# The runtime image expects:
#   /config         persistent app data (config.xml, Readarr.db, Logs/)
#   /books          your library root
#   /downloads      where download clients drop completed files

# ────────────────────────────────────────────────────────────────────
# Stage 1 — backend build
# ────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS backend-build

ARG LIBRARR_VERSION=1.0.0-beta.0
ENV READARRVERSION=${LIBRARR_VERSION}

# Override at build time for non-x64 hosts. Apple Silicon devs should
# pass `--build-arg DOTNET_RID=linux-musl-arm64` (and drop the
# `--platform linux/amd64` flag) — Roslyn segfaults under x86_64-on-arm64
# emulation. CI keeps the x64 default.
ARG DOTNET_RID=${DOTNET_RID}

WORKDIR /src

# The Phase 9b skeleton tried to flatten csprojs into a separate
# restore layer for cache locality. That optimization assumed the
# project-folder name matched the csproj basename, which isn't true
# in this repo (folders are `NzbDrone.X`, csprojs are `Readarr.X`).
# Until someone wires up an explicit name-mapping, do a single
# COPY src/ + restore + publish. Slower on iterative builds; correct.
COPY .editorconfig ./
# NzbDrone.Core.csproj embeds ../../Logo/64.png — i.e., expects the
# Logo/ tree at the WORKDIR root, sibling to src/. Without this the
# Release publish dies with CS1566 on the embedded resource.
COPY Logo/ ./Logo/
COPY src/ ./src/
RUN dotnet restore src/Readarr.sln -p:Configuration=Release -p:Platform=Posix -r ${DOTNET_RID}
RUN dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
        -c Release \
        -f net6.0 \
        -r ${DOTNET_RID} \
        --no-restore \
        --self-contained false \
        -p:Platform=Posix \
        -o /publish

# The platform shim (Readarr.Mono on Linux/macOS; Readarr.Windows on
# Windows) is loaded by name at runtime via AssemblyLoader, not as a
# static ProjectReference of Console. dotnet publish on Console alone
# therefore doesn't drop it into /publish — we have to publish it
# separately and let its output overlay. Mirrors the per-project
# publish loop in build.sh's PublishAllRids target.
RUN dotnet publish src/NzbDrone.Mono/Readarr.Mono.csproj \
        -c Release \
        -f net6.0 \
        -r ${DOTNET_RID} \
        --no-restore \
        --self-contained false \
        -p:Platform=Posix \
        -o /publish

# ────────────────────────────────────────────────────────────────────
# Stage 2 — frontend build
# ────────────────────────────────────────────────────────────────────
FROM node:20-alpine AS frontend-build

WORKDIR /src

# Yarn install layer.
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile --network-timeout 120000

# Webpack build. tsconfig.json at repo root is read by the
# fork-ts-checker plugin; the rest of the frontend config (babel,
# postcss, webpack) lives under frontend/.
COPY frontend/ ./frontend/
COPY tsconfig.json ./
RUN yarn run build --env production

# Output lands at /src/_output/UI (per project webpack config).

# ────────────────────────────────────────────────────────────────────
# Stage 3 — runtime image
# ────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS runtime

LABEL org.opencontainers.image.title="Librarr" \
      org.opencontainers.image.description="Librarr — OpenLibrary continuation of the archived Readarr ebook manager" \
      org.opencontainers.image.source="https://github.com/Librarr/Librarr" \
      org.opencontainers.image.licenses="GPL-3.0-only"

RUN apk add --no-cache \
        sqlite-libs \
        icu-libs \
        ca-certificates \
        bash \
        curl \
    && rm -rf /var/cache/apk/*

ENV LIBRARR_HOME=/app \
    LIBRARR_DATA=/config \
    LIBRARR_PORT=8787 \
    LIBRARR_SSL_PORT=6868 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR ${LIBRARR_HOME}

COPY --from=backend-build /publish/ ${LIBRARR_HOME}/
COPY --from=frontend-build /src/_output/UI/ ${LIBRARR_HOME}/UI/

VOLUME ["/config", "/books", "/downloads"]
EXPOSE 8787 6868

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://localhost:${LIBRARR_PORT}/ping || exit 1

ENTRYPOINT ["./Readarr"]
CMD ["-nobrowser", "-data=/config"]
