# Local development

Run commands from the repository root. Start here to run the platform,
prepare app validation/runtime profiles, or iterate on its UI.
For the user workflow, see [Apps and waves](workspace.md).

## Start the platform

Install the .NET SDK pinned in `global.json` and a Docker-compatible runtime
before starting. From the repository root, restore the pinned Aspire tool:

```bash
dotnet --version
dotnet tool restore
dotnet aspire --version
```

The expected SDK is `10.0.301` and Aspire CLI is `13.6.5`. Use `dotnet aspire`
to select the repository tool; a bare `aspire` can select a different global
installation.

Set the required Aspire parameters, start the isolated AppHost, and wait for
the single web resource. Setting the inputs and opening the URL are manual
actions; startup/build and the health wait run asynchronously:

```bash
export Parameters__copilot_token="<copilot-supported-token>"
export Parameters__workspace_path="${TMPDIR:-/tmp}/squad-on-aks-fixture"

dotnet aspire start --isolated --non-interactive
dotnet aspire wait app --non-interactive
dotnet aspire describe --format Json --non-interactive
```

Open the `http` URL for `app` from `dotnet aspire describe`; root shows the app
list. Select an app to plan its first build. Stop the isolated AppHost when done:

```bash
dotnet aspire stop --non-interactive
```

Both parameters are required. The web process fails at startup with an explicit
configuration error when either value is empty. The first start initializes the
configured workspace from the packaged template; later starts resume its saved
library, squads and waves. The packaged source is never mutated, no `.squad` directory is copied,
the writable copy is initialized as its own Git repository, and the diagnostics
endpoint initially reports `Needs cast`.

Do not point `Parameters__workspace_path` at the repository checkout or another
directory containing the packaged fixture source. Source and workspace paths
must be separate so reset can safely remain deterministic.

### Build an app in waves locally

Real local work waves require Docker and an explicitly selected immutable worker
image for validation. After preparing an immutable worker image (see the
[worker image](local-kubernetes.md#build-and-deploy-generated-apps-on-local-kubernetes)), set this
before starting the AppHost:

```bash
export SQUAD_LOCAL_VALIDATION_IMAGE="$(docker image inspect \
  localhost:5057/soaks-worker:local-proof --format '{{.Id}}')"
```

The local runner mounts a filtered artifact-export snapshot, not the runtime
workspace, and runs validation on a disposable writable copy inside the
non-root worker. It has no network, Docker socket, host credentials, Kubernetes
identity or writable host-source mount. Runtime metadata is excluded; application
artifacts are filtered, not scanned for every possible embedded secret. A failed
or unavailable runner is an error, not a successful validation. Mutable image
tags are rejected. This option is only honored by the non-production local host;
cluster-hosted work continues to use its existing Kubernetes validation jobs.
The validation entry point is `sh tests/run.sh`: keep it POSIX-compatible.
The selected image must already contain the app's declared dependency cache for
its target platform. The worker cache is seeded from
`src/SquadOnAks.Worker/cache-seed/` (package references only, no app code) for
the Aspire app shape: AppHost, PostgreSQL, JavaScript and Kubernetes hosting,
EF Core with Npgsql, and Vite/React. Add a package there and rebuild the image
rather than giving the validator network access or host secrets.
For a .NET dependency image, expose that cache as an explicit local package feed
(for example, `ENV RestoreSources=/worker/nuget`). A populated global cache alone
can still trigger remote version queries. Offline build validation does not
provide an online package-vulnerability audit.
Planning and team setup retain native activity evidence but never execute
application validation or create work waves.

### First-build safeguards and running-app acceptance

Every work turn receives a shared generation contract. New starter apps provide
`app.build.json` (schema version 1), a declared validation profile, POSIX
`tests/run.sh`, and `tests/serve.sh`. The serve script runs app workloads on HTTP
port 8080 behind one same-origin frontend/API gateway. An Aspire app keeps its
own AppHost for normal development/deployment; the isolated serve script does
not invoke nested Docker or add generated resources to the platform AppHost.

Predictive checks reject unsupported profiles, newer-than-supported SDK targets,
inconsistent EF/provider versions, unscoped AppHost secret identities, unsafe
database-reset/process-killing tests, fragile origin parsing, hardcoded browser
loopback APIs, and incorrect static-preview paths. The local host reads actual
offline package-cache versions from immutable images before generation and
rejects uncached pinned and locked transitive dependencies before invoking
validation. Host-generated lockfiles must match the worker's cache and runtime
identifier, or be explicitly re-evaluated offline in the disposable copy. Issues are
available at `/work/preflight` and appear in the affected wave's Activity.
Existing fixture templates remain compatible; old apps are not automatically
rewritten or retroactively marked verified.

Install architecture-specific caches with an explicit profile-to-image map:

```bash
export SQUAD_LOCAL_VALIDATION_IMAGES='{"dotnet-postgres":"sha256:<prepared-image-id>","dotnet-sqlite":"sha256:<prepared-image-id>"}'
export SQUAD_LOCAL_RUNTIME_IMAGE="sha256:<prepared-worker-image-id>"
export SQUAD_LOCAL_POSTGRES_IMAGE="sha256:<cached-postgres-image-id>"
```

Replace the placeholders with real lowercase immutable digests. Runtime images
must contain the worker tools (including Python for the trusted HTTP gateway)
and the matching dependency cache. Extra profiles use their own image; the
`static`/`dotnet` profiles use `SQUAD_LOCAL_RUNTIME_IMAGE`. No image is implicitly
pulled and no profile is a universal dependency cache.

After build validation, the platform starts a **disposable isolated app** and
checks declared real health paths, then uses Playwright against its actual
runtime. Browser journeys contain bounded selector-based interactions and
observable assertions. Database apps also restart and read back matching
assertions in a fresh browser, without recreating the data. A phone viewport is
checked separately. Browser requests are confined to the owned app origin.
External services/CDNs, WebSocket/SSE-dependent flows, and large uploads are not
supported by this bounded local HTTP gateway.

Only successful acceptance promotes the app to an independently managed local
runtime. The app and optional PostgreSQL sidecar have an internal-only network;
a trusted gateway exposes a random **loopback-only** port. App code remains
non-root with a read-only root, bounded scratch/resources/logs, no Docker socket
or host credentials, and a sanitized source snapshot. Persistent volumes and
PostgreSQL credentials are app-specific. Acceptance data is disposable; failed
browser updates do not replace the running app or erase its data. Successful
PostgreSQL updates reuse the owned database process instead of starting a second
writer on its volume; failed candidates preserve that process and its data. Total
acceptance is bounded to five minutes and cleanup verifies resource ownership.
Managed containers survive a platform restart; their stored identity, port,
current revision and readiness are rechecked before exposing the saved URL.

The UI distinguishes build checks, predictive blockers, Playwright/persistence
checks and an actual running app. **Open app** uses the verified runtime URL;
server-backed apps are never represented by an embedded static landing page.
Modern static modules/styles receive CORS only on public asset responses; the
opaque-origin preview sandbox remains unchanged.

This managed runtime is opt-in and **local only**. Production still requires
app-owned Kubernetes deployment boundaries; unconfigured runtime capabilities
fail closed rather than claiming that a static preview proves a server app.
Kubernetes validation now mounts a sanitized snapshot and executes tests on a
writable disposable copy, retaining deny-egress isolation. Offline NuGet
`RestoreSources` is also built into the worker image.

To have a Squad build a new app from a nearly empty repository instead of the
`route-planner` remediation fixture, select the `starter` template. Locally,
the Copilot token parameter is also passed to the app as `GITHUB_TOKEN`, so use
a token that can create repositories if you want to publish:

```bash
export Parameters__copilot_token="$(gh auth token)"
export Parameters__workspace_path="${TMPDIR:-/tmp}/squad-on-aks-app"
export Parameters__workspace_template="starter"
dotnet aspire start --isolated --non-interactive
```



## UI development

For local UI iteration, CSS and JavaScript changes are refresh-visible without
restarting the app or rerunning a wave. Razor markup and server-code changes
require a rebuild/reload unless hot reload is explicitly enabled.

### Topology diagrams (React Flow bundle)

The topology islands are a small React Flow app in
`src/SquadOnAks.Web/ClientApp/`. Its build output is committed to
`src/SquadOnAks.Web/wwwroot/topology/`, so `dotnet build`, the web image and CI
need no Node. After changing anything under `ClientApp/src`, rebuild and commit
the output:

```bash
cd src/SquadOnAks.Web/ClientApp && npm ci && npm run build
```

The page loads the module through Blazor's import map (fingerprinted), and the
stylesheet through `@Assets` in `Components/App.razor`.
