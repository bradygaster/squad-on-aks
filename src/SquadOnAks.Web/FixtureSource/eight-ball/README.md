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

The publish command generates Kubernetes artifacts only. Its app Service is cluster-internal; Daydream's protected app route still needs to bridge that Service before “Try it” can be enabled. Generated-code execution and deployment are intentionally not wired to Daydream yet; this fixture does not deploy to any cluster.
