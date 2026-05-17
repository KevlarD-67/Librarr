# Librarr Docker image. Phase 9 skeleton.
#
# Build context: repo root.
#   docker build -f distribution/docker/Dockerfile -t librarr/librarr:1.0.0-beta.1 .
#
# The published image expects:
#   /config         persistent app data (config.xml, Readarr.db, Logs/)
#   /books          your library root
#   /downloads      where the download clients drop completed files
#
# This Dockerfile assumes the backend + frontend have ALREADY been built
# locally via ./build.sh (it copies _output/). For a self-contained
# multi-stage build that compiles inside the image, see Phase 9b.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

LABEL org.opencontainers.image.title="Librarr" \
      org.opencontainers.image.description="Ebook/audiobook collection manager — Readarr fork on Open Library" \
      org.opencontainers.image.source="https://github.com/Librarr/Librarr" \
      org.opencontainers.image.licenses="GPL-3.0-only"

# Install the same packages Readarr's hotio image carries: SQLite (for the
# main DB), sqlite-libs, libintl (NLog), ca-certificates (HTTPS).
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

# Phase 9 expects the build output for linux-musl-x64 (alpine RID). For
# other arches build with the matching RID and rebuild the image.
COPY _output/net6.0/linux-musl-x64/publish/ ${LIBRARR_HOME}/

VOLUME ["/config", "/books", "/downloads"]
EXPOSE 8787 6868

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://localhost:${LIBRARR_PORT}/ping || exit 1

ENTRYPOINT ["./Readarr"]
CMD ["-nobrowser", "-data=/config"]
