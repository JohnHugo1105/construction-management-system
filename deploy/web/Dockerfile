# Build context is the repository root:  docker build -f deploy/web/Dockerfile -t constructopx-web:<commit-sha> .
FROM node:22-alpine AS build
WORKDIR /repo/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build
# The version file is written here, where the user can write. The runtime image runs as an unprivileged user that can't write to
# the web root, so it only copies the finished folder.
ARG CONSTRUCTOPX_RELEASE_SHA=local
RUN printf '{"service":"constructopx-web","commit":"%s"}\n' "${CONSTRUCTOPX_RELEASE_SHA}" > dist/web/browser/version.json

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
ARG CONSTRUCTOPX_RELEASE_SHA=local
LABEL org.opencontainers.image.revision=${CONSTRUCTOPX_RELEASE_SHA} org.opencontainers.image.title="constructopx-web"
COPY deploy/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY deploy/web/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /repo/web/dist/web/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 CMD wget -qO- http://127.0.0.1:8080/version.json >/dev/null || exit 1
