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
#   1. backend-build  → dotnet publish for linux-musl-x64
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

WORKDIR /src

# Restore in a separate layer so dependency changes invalidate the
# cache, but iterating on source code reuses the restored packages.
COPY src/*.sln ./src/
COPY src/Directory.Build.props src/Directory.Build.targets src/Directory.Packages.props src/.editorconfig src/stylecop.json src/Stylecop.ruleset ./src/
COPY src/*/*.csproj ./
RUN for csproj in *.csproj; do \
        mkdir -p "src/${csproj%.csproj}" && mv "$csproj" "src/${csproj%.csproj}/"; \
    done
RUN dotnet restore src/Readarr.sln -p:Configuration=Release -p:Platform=Posix -r linux-musl-x64

# Copy the rest of the source and publish for alpine RID.
COPY src/ ./src/
RUN dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
        -c Release \
        -r linux-musl-x64 \
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
      org.opencontainers.image.description="Ebook/audiobook collection manager — Readarr fork on Open Library" \
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
