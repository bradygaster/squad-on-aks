# Apps and waves

Use the workspace to plan an app, see its team, request changes, and open
verified results. [Start the platform](development.md) first.

## Apps and teams

Root opens **Apps**, with app statuses and each app's latest request and time.
It does not implicitly select the last app or begin work. **Squad** and **Apps**
return here; the library is not repeated within an app. The GitHub-native light
and dark workspace shows a read-only team sidebar with native member names and
roles, and each wave's recorded contributors, validation result and changed-file
count. Team setup remains internal, without a separate approval step. Internal
storage names and the existing API contracts are retained.

`GET /apps` lists the library and `POST /apps` starts a fresh app. Each entry has
a stable ID. `POST /apps/{id}/select` validates a selection; selection is carried
in the page URL (`?app={id}`), not in shared server state, so different tabs can
work on different apps. The existing `/ag-ui`, `/casting/*`, `/work/*`, `/waves`,
`/workspace/*`, and `/publish` routes are available below `/apps/{id}`. Unprefixed
routes remain aliases for the original app.

Each app owns its squad, lifecycle, conversation sessions, evidence, waves,
download and publication services. The original workspace is retained; the
library registry and additional app workspaces live beneath its `.git` directory
and are excluded from app exports. Titles, activation and wave histories resume
independently after restart. On the separate preview listener, `/apps/{id}/`
serves only that app's generated static site; it never exposes Squad's APIs.
Generated sites should use document-relative links and assets.

Initialization validates an exact copy of the packaged template. Ongoing readiness
checks workspace and Git-directory presence, the configured production runtime
boundary, and shutdown state instead of comparing mutable files with the template.
Casting and application edits therefore do not invalidate readiness. Unhealthy
readiness returns HTTP 503 with an empty body, not the UI not-found page; liveness
remains independent.

## Build and iterate

The platform's `http` endpoint hosts this workspace. The separate `preview`
endpoint serves generated static assets; server-backed apps use their verified
managed runtime instead.

1. **Start an app.** Describe the app and choose **Plan app**. Answer any
   essential app question or add detail, then choose **Build my app**. The saved
   original idea plus refinements starts the first wave automatically; no team
   approval or second "go build it" prompt is needed. Native confirmation and
   activation validation remain internal, using the existing endpoints. Team
   setup is constrained to `.squad/` files before that first work turn. The
   internal request calls for focused ownership, bounded handoffs, independent
   validation and no duplicated work; this is guidance, not a measured token
   efficiency claim. Same-tab reload preserves refinements; the original brief
   is also saved server-side. Existing waves prevent a repeated first build.
2. **Waves.** Each request becomes a persistent wave. Its app-focused notice
   shows what was asked, the outcome, a bounded recap, validation, contributors and
   an action to try the result. Past waves remain reopenable. Raw transcripts,
   code, diffs and command output are not rendered. Native
   evidence remains available internally. Use **What would you like to change?**
   for subsequent work, or stop a running wave. Casting/reconfirmation and
   review edits remain rejected after activation, even after failed work.
3. **Keep the app.** **Open app** opens
   the verified runtime when available (otherwise a clearly labeled static
   preview for static apps), **Download ZIP** exports the workspace, and
   **Publish to GitHub** reviews the destination account/organization and
   repository name before creating a new **private-only** repository and
   pushing the full wave-by-wave commit history, including Squad team/configuration
   files. Before creating a remote, publishing scans every commit for credential
   content and Squad/Copilot runtime paths; tainted history is blocked with
   remediation guidance and commit SHAs are never rewritten. Publishing uses
   the server-configured credential, not an independent browser authorization
   flow. The form identifies that credential's account; organization choices
   are subject to GitHub creation permissions.
   If creation succeeds but publication fails, the result includes the created
   repository link. Retry with the same destination name to resume publication
   to that private repository; the workspace-local retry record prevents a
   replacement repository from being created. This retry record does not
   survive workspace or pod replacement.

## Downloads and publishing

ZIP export keeps at most 64 KiB in its memory spool, spills larger archives to
temporary disk, and rejects archives exceeding 2 GiB. The completed spool is
copied asynchronously to the response and deleted on disposal. Publication and
agent turns share exclusive workspace admission. Generated validation has a
two-minute execution limit; cancellation/failure cleanup checkpoints without
rerunning validation and is bounded to ten seconds (plus subprocess reaping).

ZIP downloads contain current workspace and Squad team/configuration files,
without Git history, `.git`, `node_modules`, symbolic links, or private Squad
runtime state. **Open app** opens the verified managed runtime for a server-backed
app, or a clearly labeled static preview for a static app. It does not deploy the
application.

## Stored state

Each app's wave timeline, title and activated squad are saved under its private `.git` metadata
and resume when Squad restarts against the same workspace path. Cast conversation
history is stored separately per app in the browser session and restored only
when that app's generation matches. An explicit workspace reset discards that
app's old browser context; an ordinary restart does not. Storage remains
ephemeral when the hosting environment replaces the entire workspace volume.
The exact original casting intent is stored
server-side in the standalone workspace's private `.git/squad-original-intent.json`
metadata, before the server's casting guard is appended. Fresh browsers retrieve
only that text through `GET /casting/intent` (`Cache-Control: no-store`).
For older workspaces, the endpoint recovers the first user event from a uniquely
identified native casting session, strips only the known injected suffix, and
saves the recovered intent. Missing, malformed, unsafe, or ambiguous histories
return explicit errors rather than substituting a generated team summary.
Neither intent metadata nor native runtime logs are exposed as artifacts or
included in ZIP downloads or GitHub pushes. This metadata survives server
recreation with the same workspace, not a destructive workspace reset. Linked
Git worktrees are not supported as runtime fixture workspaces.

Squad runtime state (`.squad/session-state/` and `.squad/session-store.db*`) is
git-excluded and hidden from artifacts, ZIP downloads, and published
repositories.

## Wave and artifact API

| Endpoint | Purpose |
|---|---|
| `GET /waves`, `GET /waves/{number}` | Wave timeline and per-wave evidence |
| `GET /workspace/files`, `GET /workspace/file?path=`, `GET /workspace/raw?path=` | Artifact listing, file content, and raw files such as images |
| `GET /workspace/export.zip` | ZIP download of the workspace |
| `GET /workspace/preview` | Static preview availability and verified managed runtime status/URL |
| `GET /work/preflight` | Predictive build-contract, compatibility and dependency blockers |
| `GET /publish/owners`, `POST /publish` | Account and organization choices, then publish to a new GitHub repository |

## Cluster-hosted apps

In AppHost publish mode (`aks` or `docker-desktop`), Squad sets
`SOAKS_HOSTING=cluster`. Each app can explicitly be put online through
`POST /apps/{id}/workspace/online` (`first` is the original app).
`GET` on that route returns `{ available, online, url, message }`; `online`
stays false until the deployed app passes readiness, and `url` then points
to `/online/{id}/` on Squad's existing address. The unscoped
`/workspace/online` route addresses the original app. `/casting/status` also
exposes `hostingMode` as `local` or `cluster`.

**Aspire apps** (every new app; `app.build.json` declares `architecture: "aspire"`):
going online runs a build job (front end, API container image and the Aspire
Helm chart, built offline in the worker) and then a deploy job that installs the
chart into the app's own namespace, `soaks-app-{id}`. That namespace is
created by the namespace provisioner with restricted Pod Security, a
deploy-only identity, and a NetworkPolicy that admits traffic only from Squad
host pods and the app's own pods, with egress limited to its own namespace and
cluster DNS. The PostgreSQL password is generated once per app at first deploy
and reused on every update. The app is online only after its front end serves
HTML and every declared health path (including the database check) returns 200.
After a completed wave, an app that is already online is rebuilt and upgraded in
place in the same namespace; a failed update leaves the previous revision running.

The `/online/{id}/` proxy forwards any well-formed path of an Aspire app,
including API writes (`POST`, `PUT`, `PATCH`, `DELETE`) with a bounded body
(256 KiB) and content type only, never cookies, credentials or forwarded
headers. Online apps run in an opaque sandboxed origin, so the proxy answers
CORS preflight for JSON writes. App 4xx results pass through; server errors
are opaque.

**Older static apps** keep the original static slice: an `index.html` with up
to 128 public files (256 KiB each, 512 KiB total), copied into a per-app
ConfigMap and served by an unprivileged static server through `GET`/`HEAD`
only. Apps from before the Aspire contract that are server-backed cannot be
deployed and are asked to be rebuilt as Aspire apps.

Online intent is saved with each app's state. As with all current workspace
state, it survives process restarts but not replacement of the disposable
workspace volume. Local Aspire mode retains the existing preview but rejects
online actions with a plain explanation.

## Topology and deployment visibility

Each app page has a **Topology** section with an **Aspire | Cluster** toggle
(remembered per browser). `GET /apps/{id}/workspace/topology` returns
`{ state, namespace, revision, aspire, cluster }`, where `state` is:

- `planned` before the first build: the contract's shape (front end, API,
  PostgreSQL) in the app's namespace, labeled as planned;
- `built` once the worker has rendered the Aspire chart (`helm template`
  output saved with the build artifacts);
- `deployed` once a revision is running in the namespace.

Built and deployed views are parsed from the rendered manifests: object names,
kinds, images (repository name only), Service selectors, volume claims and
references between components. Secret and configuration values are never read
into the topology. The diagrams are React Flow islands (`ClientApp/`, built into
`wwwroot/topology/`); each full diagram has a screen-reader list of its nodes and
connections, and compact copies appear in the app list and the Share section.

**Live status** (cluster mode, deployed apps): `GET /apps/{id}/workspace/topology/live`
returns `{ available, observedAt, nodes }`, keyed like the topology's nodes
(`workload:{name}` and the Aspire resource name). Each workload is `ready`,
`starting`, `failing` or `stopped`, with a detail built only from ready/desired
replica counts, Kubernetes container reason codes (for example
`CrashLoopBackOff`, `ImagePullBackOff`) and restart counts. Container messages,
events, logs, secrets and ConfigMaps are never read. Squad reads it through a
read-only `soaks-app-observer` Role (get/list/watch on pods, Deployments and
StatefulSets) that the namespace provisioner creates in each app namespace and
binds only to the Squad host's service account. Results are cached for three
seconds. When the cluster can't be read (or in local mode) the page says
"Live status unavailable" rather than showing a healthy state. Failing workloads
also appear in **What's happening** under "Check that it works".

**What's happening** is an on-demand disclosure under the diagram that shows the
latest revision's recorded steps (build, deploy to its namespace, health check)
with state and time, what to do when a step fails, and earlier revisions. It is
drawn from `GET /workspace/deployments`; logs and command output are not shown.

## Platform and casting API

The application exposes:

| Endpoint | Expected response |
|---|---|
| `GET /` | App library; `?app={id}` opens an app |
| `POST /ag-ui` | Streaming AG-UI server backed by `Squad.Agents.AI` |
| `GET /diagnostics/workspace` | Disposable fixture state, test command, expected changed files, and native harness files |
| `GET /casting/status` | `Needs cast`, `Cast review`, or activated `Ready` state |
| `GET /casting/review` | Native review allowlist and Markdown content after confirmation |
| `PUT /casting/review` | Save an allowlisted native Markdown review edit |
| `POST /casting/activate` | Validate the native cast and unlock work controls |
| `GET /health/live` | HTTP 200 |
| `GET /health/ready` | HTTP 200 when ready; HTTP 503 otherwise |
