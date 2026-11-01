# Build context is the repository root:  docker build -f deploy/web/Dockerfile -t cms-web:<commit-sha> .
FROM node:22-alpine AS build
WORKDIR /repo/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
ARG CMS_RELEASE_SHA=local
LABEL org.opencontainers.image.revision=${CMS_RELEASE_SHA} org.opencontainers.image.title="cms-web"
COPY deploy/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/web/dist/web/browser /usr/share/nginx/html
RUN printf '{"service":"cms-web","commit":"%s"}\n' "${CMS_RELEASE_SHA}" > /usr/share/nginx/html/version.json
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 CMD wget -qO- http://127.0.0.1:8080/version.json >/dev/null || exit 1
