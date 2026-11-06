# Aspire 8-ball proof

This fixture is a real multi-project app: a React/Vite frontend calls an ASP.NET Core Minimal API, and its own Aspire AppHost composes both services. The AppHost publishes the built frontend into the API's `wwwroot`, so the deployed app serves the UI and `/api` from the same origin.

From this directory:

```sh
npm config set registry "https://packagefeedproxy.microsoft.io/npm/"
npm config get registry
dotnet restore AppHost/AppHost.csproj
dotnet run --project AppHost
```

In the Aspire dashboard, open the `frontend` endpoint and ask the universe. For a production artifact publish, run:

```sh
Parameters__registry_endpoint=registry.example/eight-ball aspire publish --apphost AppHost
```

Aspire `publish` generates the Helm chart and values, but does not build or push the API image. Daydream's deployment coordinator now persists and resumes per-app build/deploy Job phases through `POST /workspace/deployments`; `GET /workspace/deployments` reports the durable revision history.

For this template, `POST /workspace/online` now starts or resumes its app-owned Aspire deployment instead of creating the legacy static ConfigMap. “Try it” stays unavailable until the latest release is recorded as deployed and a bounded probe validates both the frontend HTML and `/api/answer` JSON. The app route proxies the frontend and that exact API path to Aspire's generated `api-service` ClusterIP Service on port 8080; other static templates retain their existing hosting path.

The real worker is implemented in the platform's `src/SquadOnAks.Worker`. It builds without network access, a Docker socket, or a Kubernetes token, producing `/artifacts/package.tgz` and an OCI image archive. A separate app-scoped deployment job publishes the archive to the dedicated local registry and installs the app-owned release. The generated namespace enforces restricted Pod Security; the API runs nonroot with a read-only root filesystem and no mounted service-account token.

The platform README documents the local registry/node prerequisite and Aspire deployment inputs. This is a fixed-fixture local proof, not production registry authentication or verified Docker Desktop NetworkPolicy enforcement. No cluster is modified by artifact publishing alone.

The platform initializes this template with the shared native Squad harness from its packaged starter template. `sh tests/run.sh` runs in the isolated worker, building the frontend and checking API readiness and the bounded answer JSON contract without modifying the source workspace.
