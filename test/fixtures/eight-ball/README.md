# Aspire 8-ball proof

This fixture is a real multi-project app: a React/Vite frontend calls an ASP.NET Core Minimal API, and its own Aspire AppHost composes both services. The AppHost publishes the built frontend into the API's `wwwroot`, so the deployed app serves the UI and `/api` from the same origin.

From this directory:

```sh
npm config set registry "https://packagefeedproxy.microsoft.io/npm/"
npm config get registry
dotnet restore AppHost/AppHost.csproj
dotnet run --project AppHost
```

In the Aspire dashboard, open the `frontend` endpoint and ask the universe. For a production artifact build, run:

```sh
Parameters__registry_endpoint=registry.example/eight-ball aspire publish --apphost AppHost
```

The publish command generates Kubernetes artifacts only. Daydream's `POST /workspace/online` now starts or resumes the app-owned Aspire deployment instead of creating the legacy static ConfigMap. “Try it” stays unavailable until the latest release is recorded as deployed and a bounded probe validates both the frontend HTML and `/api/answer` JSON. The app route proxies the frontend and that exact API path to Aspire's generated `api-service` ClusterIP Service on port 8080; other static templates retain their existing hosting path.

Daydream now has a trusted provisioner that authenticates the coordinator service account and creates each app's namespace, deploy Role/RoleBinding, default service-account policy, and network boundary. The isolated worker image and safe image build/publication path remain outstanding, so this fixture is not yet deployable through Daydream. This fixture does not deploy to any cluster.
