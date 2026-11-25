# Agent instructions

Read this before changing anything. It overrides conventions you might infer from
the code, commit history or older docs.

## What this is

**Squad on AKS**: a GitHub-native web workspace where a person describes an app,
an AI team (Squad) builds it in persistent **waves**, the result is deployed
into its own Kubernetes namespace, and later waves change the deployed app.
Results can also be exported or published. Blazor + Aspire host; AKS or Docker
Desktop Kubernetes for cluster mode.

- Product truth: [PRODUCT.md](PRODUCT.md). UI conventions: [DESIGN.md](DESIGN.md).
- Task docs: [docs/](docs/) (start with `docs/development.md` and `docs/testing.md`).

## Current identity — do not revisit

The product was briefly called **Daydream** (an "imagination tool" with a
neighborhood/mural UI). That direction is **abandoned**. Do not reintroduce its
copy, metaphors or visuals.

- User-visible text says **Squad**, "app", "team", "wave". Never "Daydream".
- Internal names don't need a product slug. Where one is needed so Kubernetes
  or storage can recognize our resources, it is **`soaks`** (Squad on AKS):
  state files (`soaks-waves.json`, `soaks-title.txt`, `soaks-workspace.json`),
  Kubernetes labels (`soaks.dev/*`), app namespaces (`soaks-app-{id}`),
  resource/service-account names and `SOAKS_*` env vars. Never `daydream`.
  The `/ag-ui`, `/casting/*`, `/work/*` and `/waves` endpoints stay as they are.

## The goal — what "done" means for the product

The product is done when, through the UI, a person can:

1. **Generate a new app.** Describe it → **Plan app** → **Build my app**. The
   team produces an **Aspire app** (a real AppHost plus its services). Every
   generated app is an Aspire app; there is no static-only or bare-server
   shortcut.
2. **Deploy it into its own namespace.** Each app runs in a dedicated Kubernetes
   namespace created by the namespace provisioner, and the UI shows a working URL
   only after that deployment is healthy.
3. **Edit it after deployment.** Request another wave on a deployed app. The team
   changes the source, and the new revision is redeployed into the **same**
   namespace. If the update fails, the previous working revision and its data are
   kept.
4. Repeat for more apps, each isolated in its own namespace.
5. **See how it's built and where it runs.** Each app page shows its Aspire and
   cluster topology (toggle), and an on-demand **What's happening** view of the
   recorded build/deploy steps.

The proving ground is Docker Desktop Kubernetes (`docs/local-kubernetes.md`), then
AKS (`docs/deployment.md`). The generic Aspire build/deploy path is implemented and
unit-tested, and the contract's chart passes a server-side dry run; a real-model
end-to-end run on a cluster is the next proof, not more platform features.

Work that does not move one of these steps forward is **out of scope**
unless the user asks for it by name. In particular, do **not** on your own:

- add architectures, runtime profiles or databases beyond what an Aspire app
  needs to deploy and update;
- grow `Work/AppBuildPolicy.cs` with new generation rules to get past a single
  generated-app failure. Report the failure and the smallest fix; ask before
  building infrastructure around it;
- pursue CI matrices, multi-architecture builds, backup/restore or multi-tenant
  auth hardening from issue #51;
- redesign the UI, rename the product or rewrite PRODUCT.md / DESIGN.md;
- add `docs/evidence/*` files or "record proof" commits.

If a task seems to require any of the above, stop and ask.

## Working rules

- Prefer the smallest change that fixes the problem. Do not refactor nearby code.
- Edit an existing file over creating a new one. Do not add docs files unless asked.
- The UI is a static Blazor shell (`Components/Pages/Home.razor`) driven by
  vanilla JS in `wwwroot/casting.js`. Keep it that way: no component libraries,
  CDNs, external fonts or icon packages. The one exception is React Flow, used
  only for topology islands: source in `src/SquadOnAks.Web/ClientApp/`, built
  output committed to `wwwroot/topology/` (rebuild with
  `cd src/SquadOnAks.Web/ClientApp && npm ci && npm run build`).
- Never show raw transcripts, model reasoning, file contents, command output,
  credentials or infrastructure diagnostics in the UI (see PRODUCT.md).
- Do not invent activity, progress, or team-member assignments in the UI.
- There is no demo app. Do not add a fixed sample app back; new apps start from
  a description, an example idea or **Surprise me** (prompts live in `casting.js`).
- The generated-app contract lives in `Work/AppBuildPolicy.cs` (instructions +
  preflight) and `FixtureSource/starter/AppHost/SquadHosting.cs` (hardened
  publishing for the app's namespace). Keep them consistent with
  `src/SquadOnAks.Worker/build|deploy` and `Workspace/AppTopology.cs`; the
  resource names `api`, `db`, `appdb` and `frontend` are relied on by all four.
- The topology never reads secret or configuration values, and live status uses
  only replica counts, reason codes and restart counts (never logs, events or
  container messages). The `soaks-app-observer` role stays read-only.
- Workspaces live **outside** this checkout. Never point `workspace_path` here.
- Never commit tokens. `Parameters__copilot_token` comes from the environment.

## Definition of done for a change

A change is done when it moves a goal step forward **and** these gates pass.
Run from the repository root. All must pass before you say a change is done;
report any failure verbatim rather than working around it.

```bash
dotnet restore SquadOnAks.slnx --locked-mode --nologo
dotnet build SquadOnAks.slnx --configuration Release --no-restore --nologo
dotnet format SquadOnAks.slnx --verify-no-changes --no-restore
dotnet run --project test/SquadOnAks.WorkspaceTests --configuration Release --no-build
```

For UI changes, also run:

```bash
dotnet run --project test/SquadOnAks.AcceptanceTests --configuration Release --no-build -- --layer deterministic
```

Mocked and deterministic suites prove contracts, not real-model builds. A goal
step counts as working only when a real Aspire app was generated, deployed to
its own namespace and (for step 3) updated in place on a cluster, and the user
has seen it. Do not claim that from tests alone.
