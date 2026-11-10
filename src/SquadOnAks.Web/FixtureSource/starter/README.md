# New Squad app

An empty workspace for a Squad to build a new Aspire app from scratch. Every app
has a front end, an API and a PostgreSQL database, and is deployed into its own
Kubernetes namespace.

- `AppHost/`: the Aspire AppHost. `AppHost/SquadHosting.cs` is provided by Squad;
  keep it unchanged and call `AddSquadKubernetes()`, `PublishAsSquadDatabase()` and
  `PublishAsSquadApi()` from `AppHost/Program.cs`.
- `Api/`: the ASP.NET Core API, which also serves the built front end.
- `frontend/`: the Vite + React front end.
- `Directory.Build.props`, `Directory.Packages.props`, the two `.csproj` files and
  `frontend/package.json` with `package-lock.json` pin the exact packages the offline
  build worker has cached. Keep those packages and versions unchanged.
- `tests/run.sh` is the validation entry point that Squad on AKS runs after
  every wave. Replace its body with the project's real test command.
