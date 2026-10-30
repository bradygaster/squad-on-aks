ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.301-noble@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5
FROM ${SDK_IMAGE} AS build
ARG TARGETARCH
ARG NUGET_SOURCE=
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json SquadOnAks.slnx ./
COPY src/SquadOnAks.ServiceDefaults/SquadOnAks.ServiceDefaults.csproj src/SquadOnAks.ServiceDefaults/packages.lock.json src/SquadOnAks.ServiceDefaults/
COPY src/SquadOnAks.Web/SquadOnAks.Web.csproj src/SquadOnAks.Web/packages.lock.json src/SquadOnAks.Web/
RUN if [ -n "$NUGET_SOURCE" ]; then set -- --source "$NUGET_SOURCE"; else set --; fi \
    && dotnet restore src/SquadOnAks.Web/SquadOnAks.Web.csproj "$@" \
      --locked-mode \
      --ignore-failed-sources \
      -p:NuGetAudit=false \
      -p:TreatWarningsAsErrors=false

COPY src/SquadOnAks.ServiceDefaults/ src/SquadOnAks.ServiceDefaults/
COPY src/SquadOnAks.Web/ src/SquadOnAks.Web/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet publish src/SquadOnAks.Web/SquadOnAks.Web.csproj \
      --configuration Release \
      --runtime "$rid" \
      --self-contained false \
      --no-restore \
      --output /app/publish

RUN case "$TARGETARCH" in \
      amd64) cli_arch=x64; cli_sha=9aeb4430e9ac9523546db07b227c7aba179a4710a55f9ac29ad143b0f9e1b325 ;; \
      arm64) cli_arch=arm64; cli_sha=728a91dffb9a02817bd98c5b01bf2f9cb08c4fdac3c0ca8c48d7f1ccbfe282a1 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && curl --fail --location --retry 3 \
      "https://github.com/github/copilot-cli/releases/download/v1.0.103/copilot-linux-${cli_arch}.tar.gz" \
      --output /tmp/copilot.tar.gz \
    && echo "$cli_sha  /tmp/copilot.tar.gz" | sha256sum --check - \
    && mkdir /app/copilot-cli \
    && tar -xzf /tmp/copilot.tar.gz -C /app/copilot-cli \
    && rm /tmp/copilot.tar.gz

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS runtime-base
ARG GIT_VERSION=1:2.43.0-1ubuntu7.4
RUN apt-get update \
    && apt-get install --yes --no-install-recommends "git=${GIT_VERSION}" \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    HOME=/workspace/runtime/home \
    TMPDIR=/workspace/runtime/tmp \
    XDG_CACHE_HOME=/workspace/runtime/cache \
    XDG_CONFIG_HOME=/workspace/runtime/config

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SquadOnAks.Web.dll"]

FROM runtime-base AS production
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/ ./
COPY --from=build --chown=$APP_UID:$APP_UID /app/copilot-cli/copilot /app/copilot
ENV COPILOT_CLI_PATH=/app/copilot
