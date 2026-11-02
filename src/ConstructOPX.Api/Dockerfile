# Build context is the repository root:  docker build -f src/ConstructOPX.Api/Dockerfile -t constructopx-api:<commit-sha> .
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY global.json Directory.Build.props ConstructOPX.sln ./
COPY src/ConstructOPX.Domain/ConstructOPX.Domain.csproj src/ConstructOPX.Domain/
COPY src/ConstructOPX.Domain/packages.lock.json src/ConstructOPX.Domain/
COPY src/ConstructOPX.Application/ConstructOPX.Application.csproj src/ConstructOPX.Application/
COPY src/ConstructOPX.Application/packages.lock.json src/ConstructOPX.Application/
COPY src/ConstructOPX.Infrastructure/ConstructOPX.Infrastructure.csproj src/ConstructOPX.Infrastructure/
COPY src/ConstructOPX.Infrastructure/packages.lock.json src/ConstructOPX.Infrastructure/
COPY src/ConstructOPX.Api/ConstructOPX.Api.csproj src/ConstructOPX.Api/
COPY src/ConstructOPX.Api/packages.lock.json src/ConstructOPX.Api/
# Locked restore: the build fails if a lock file is stale instead of silently resolving other package versions.
RUN dotnet restore src/ConstructOPX.Api/ConstructOPX.Api.csproj --locked-mode
COPY src/ src/
ARG CONSTRUCTOPX_RELEASE_SHA=local
RUN dotnet publish src/ConstructOPX.Api/ConstructOPX.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false /p:SourceRevisionId=${CONSTRUCTOPX_RELEASE_SHA}

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# tzdata: the business time zone (Asia/Manila by default) must resolve. icu-libs: culture-correct formatting.
RUN apk add --no-cache curl icu-libs tzdata
ARG CONSTRUCTOPX_RELEASE_SHA=local
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080 \
    CONSTRUCTOPX_RELEASE_SHA=${CONSTRUCTOPX_RELEASE_SHA}
LABEL org.opencontainers.image.revision=${CONSTRUCTOPX_RELEASE_SHA} org.opencontainers.image.title="constructopx-api"
WORKDIR /app
COPY --from=build /app .
# Where the session keys (DataProtection__KeysPath) and the document files (FileStorage__RootPath) are kept. Owned by the app user so a
# named volume mounted here inherits it.
RUN mkdir -p /var/lib/constructopx/keys /var/lib/constructopx/files && chown $APP_UID /var/lib/constructopx/keys /var/lib/constructopx/files
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=5 CMD curl -fsS http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "ConstructOPX.Api.dll"]
