# Build context is the repository root:  docker build -f src/Cms.Api/Dockerfile -t cms-api:<commit-sha> .
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY global.json Directory.Build.props ConstructionManagementSystem.sln ./
COPY src/Cms.Domain/Cms.Domain.csproj src/Cms.Domain/
COPY src/Cms.Domain/packages.lock.json src/Cms.Domain/
COPY src/Cms.Application/Cms.Application.csproj src/Cms.Application/
COPY src/Cms.Application/packages.lock.json src/Cms.Application/
COPY src/Cms.Infrastructure/Cms.Infrastructure.csproj src/Cms.Infrastructure/
COPY src/Cms.Infrastructure/packages.lock.json src/Cms.Infrastructure/
COPY src/Cms.Api/Cms.Api.csproj src/Cms.Api/
COPY src/Cms.Api/packages.lock.json src/Cms.Api/
# Locked restore: the build fails if a lock file is stale instead of silently resolving other package versions.
RUN dotnet restore src/Cms.Api/Cms.Api.csproj --locked-mode
COPY src/ src/
ARG CMS_RELEASE_SHA=local
RUN dotnet publish src/Cms.Api/Cms.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false /p:SourceRevisionId=${CMS_RELEASE_SHA}

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# tzdata: the business time zone (Asia/Manila by default) must resolve. icu-libs: culture-correct formatting.
RUN apk add --no-cache curl icu-libs tzdata
ARG CMS_RELEASE_SHA=local
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080 \
    CMS_RELEASE_SHA=${CMS_RELEASE_SHA}
LABEL org.opencontainers.image.revision=${CMS_RELEASE_SHA} org.opencontainers.image.title="cms-api"
WORKDIR /app
COPY --from=build /app .
# Where the session keys are kept (DataProtection__KeysPath). Owned by the app user so a named volume mounted here inherits it.
RUN mkdir -p /var/lib/cms/keys && chown $APP_UID /var/lib/cms/keys
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=5 CMD curl -fsS http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Cms.Api.dll"]
