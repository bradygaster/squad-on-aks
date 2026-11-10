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
- Internal identifiers containing `daydream` are **intentionally kept** for
  storage and API compatibility: file names (`daydream-waves.json`,
  `daydream-title.txt`, `daydream-workspace.json`, `daydream-runtime.json`),
  Kubernetes labels (`daydream.dev/*`), resource/service-account names,
  `DAYDREAM_*` env vars, and `/ag-ui`, `/casting/*`, `/work/*`, `/waves`
  endpoints. Do not rename them, and do not "clean them up" as a side task.

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

The proving ground is Docker Desktop Kubernetes (`docs/local-kubernetes.md`), then
AKS (`docs/deployment.md`). Today only the eight-ball proof and static apps
deploy per namespace; generic managed runtimes are local Docker only. Closing
that gap for Aspire apps is the main work.

Work that does not move one of the four steps forward is **out of scope**
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
  CDNs, external fonts or icon packages.
- Never show raw transcripts, model reasoning, file contents, command output,
  credentials or infrastructure diagnostics in the UI (see PRODUCT.md).
- Do not invent activity, progress, or team-member assignments in the UI.
- `test/fixtures/eight-ball` and `src/SquadOnAks.Web/FixtureSource/eight-ball`
  are copies; if you change one, change the other identically.
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
