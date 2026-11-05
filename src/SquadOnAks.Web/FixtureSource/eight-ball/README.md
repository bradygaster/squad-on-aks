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

The worker image configured for Daydream must provide `/worker/build` and `/worker/deploy`. The build command must write the packaged Aspire chart to `/artifacts/package.tgz`; the deploy command receives the chart path and app-owned namespace/release from its arguments. The deploy service account also requires a pre-provisioned RoleBinding in that app's namespace. The worker image, safe image-publication boundary, and trusted per-app namespace/RBAC provisioner are not yet implemented, so this fixture is not deployable through Daydream yet. “Try it” remains gated on a live frontend response and a successful `/api/answer` call. No cluster is modified by artifact publishing.
