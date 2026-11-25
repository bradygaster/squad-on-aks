ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.301-noble@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5
FROM registry:3@sha256:38907510915c3f1604c7e83d6ab602ab3020d092f73bdaed8696773693ff768c AS registry
FROM node:22-bookworm-slim@sha256:5ec23b7c622b00547753bc6ffd78085dd868a68aeb41c8e0a1418f033e13d96d AS node
FROM ${SDK_IMAGE}
ARG NUGET_SOURCE=https://api.nuget.org/v3/index.json
ARG NPM_REGISTRY=https://registry.npmjs.org/
ARG TARGETARCH
RUN apt-get update && apt-get install -y --no-install-recommends skopeo python3 curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=registry /bin/registry /usr/local/bin/registry
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && case "$TARGETARCH" in \
       arm64) helm_sha=8cf8f66ecbc252ac8f7f06cca68aba88508ddfde3a6a34feb1678ad1694b4f88 ;; \
       amd64) helm_sha=0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b ;; \
       *) echo "Unsupported worker architecture: $TARGETARCH" >&2; exit 1 ;; esac \
    && curl -fsSL "https://get.helm.sh/helm-v4.3.1-linux-${TARGETARCH}.tar.gz" -o /tmp/helm.tgz \
    && echo "$helm_sha  /tmp/helm.tgz" | sha256sum --check \
    && tar -xzf /tmp/helm.tgz -C /tmp && mv "/tmp/linux-${TARGETARCH}/helm" /usr/local/bin/helm
WORKDIR /worker
# Package-only seed (no app code) for the AppHost + Api + PostgreSQL + Vite/React app shape.
COPY src/SquadOnAks.Worker/cache-seed /worker/fixture
RUN dotnet nuget update source nuget.org --source "$NUGET_SOURCE" \
    && dotnet restore fixture/AppHost/AppHost.csproj -p:RestoreLockedMode=false --source "$NUGET_SOURCE" \
    && dotnet tool install Aspire.Cli --version 13.6.5 --tool-path /worker/tools --add-source "$NUGET_SOURCE" \
    && mkdir -p /worker/nuget && cp -a /root/.nuget/packages/. /worker/nuget/ \
    && cd fixture/frontend && npm config set registry "$NPM_REGISTRY" \
    && test "$(npm config get registry)" = "$NPM_REGISTRY" \
    && npm ci --ignore-scripts && mkdir /worker/npm-cache && cp -a /root/.npm/. /worker/npm-cache/
# The cluster journey check: the host's own browser checks (shared source) plus a pinned Chromium.
COPY global.json Directory.Build.props Directory.Packages.props /src/
COPY src/SquadOnAks.Worker/journeys /src/src/SquadOnAks.Worker/journeys
COPY src/SquadOnAks.Web/Workspace/AppBrowserAcceptance.cs src/SquadOnAks.Web/Workspace/AppBrowserJourney.cs \
    /src/src/SquadOnAks.Web/Workspace/
RUN dotnet publish /src/src/SquadOnAks.Worker/journeys -c Release -o /worker/journeys \
        --source "$NUGET_SOURCE" -p:RestoreLockedMode=true -p:NuGetAudit=false \
    && playwright_node="$(find /worker/journeys/.playwright/node -path '*linux*' -name node -type f | head -n 1)" \
    && test -n "$playwright_node" \
    && PLAYWRIGHT_BROWSERS_PATH=/worker/ms-playwright \
       "$playwright_node" /worker/journeys/.playwright/package/cli.js install --with-deps chromium \
    && rm -rf /src
RUN skopeo copy docker://mcr.microsoft.com/dotnet/aspnet@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 \
    oci:/worker/base:runtime
COPY src/SquadOnAks.Worker/build src/SquadOnAks.Worker/deploy src/SquadOnAks.Worker/secret-values /worker/
RUN rm -rf /worker/fixture && chmod 755 /worker/build /worker/deploy /worker/secret-values && chmod -R a+rX /worker
ENV HOME=/tmp/home DOTNET_CLI_HOME=/tmp/home NUGET_PACKAGES=/worker/nuget \
    RestoreSources=/worker/nuget \
    PATH=/worker/tools:$PATH \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    HELM_CACHE_HOME=/tmp/helm/cache HELM_CONFIG_HOME=/tmp/helm/config HELM_DATA_HOME=/tmp/helm/data \
    PLAYWRIGHT_BROWSERS_PATH=/worker/ms-playwright
USER 1654:1654
WORKDIR /tmp
