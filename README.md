# Squad on AKS

Build apps with an AI team, then iterate in persistent waves.
Squad on AKS brings together Squad, Blazor and Aspire in a GitHub-native workspace.

- Describe an app and see the team working on it.
- Or start from a GitHub repository: Squad uses the repository's own Squad team
  (or casts one), lists its open issues in the order its plan sets out, and can
  open a draft pull request for a finished wave.
- Each app is an Aspire app deployed into its own Kubernetes namespace, then
  updated in place by later waves.
- Follow changes, contributors and validation in each wave, and see each app's
  Aspire and cluster topology.
- Open the verified result, download the source, or publish a private repository.

## Run locally

Install the SDK pinned in [global.json](global.json) and a Docker-compatible runtime.
Use a supported Copilot token and a workspace **outside this checkout**:

```bash
dotnet tool restore

export Parameters__copilot_token="<copilot-supported-token>"
export Parameters__workspace_path="${TMPDIR:-/tmp}/squad-on-aks-app"
export Parameters__workspace_template="starter"

dotnet aspire start --isolated --non-interactive
dotnet aspire wait app --non-interactive
dotnet aspire describe --format Json --non-interactive
```

Open the `app` resource's **http** URL. Choose **New app**, describe it (or pick
an idea, or **Surprise me**), then use **Plan app** and **Build my app**. Every app
is an Aspire app: an `api` project that serves the app, plus whatever front ends,
extra APIs and PostgreSQL databases the plan calls for. Its page shows the app's
Aspire and cluster topology.

Building and running generated apps also requires prepared
[worker and runtime profiles](docs/development.md#build-an-app-in-waves-locally).
The startup commands above alone do not install those profiles.

When finished:

```bash
dotnet aspire stop --non-interactive
```

## Status

Local builds can verify real browser interactions, database persistence and app
updates before exposing a running URL. In cluster mode, generated Aspire apps
are built by an offline worker and deployed into their own namespace, then
updated in place after later waves. Real-model runs have built, deployed and
updated apps of several shapes on Docker Desktop Kubernetes and on AKS.

On AKS, Squad is private: it has no sign-in of its own, so it is reached through
`kubectl port-forward`, and the cluster API accepts only allowed IP ranges.

This is not yet a production-ready service. Required CI, portable agent skills,
broader fresh-build acceptance, production recovery and per-user publication
authorization are tracked in [#51](https://github.com/bradygaster/squad-on-aks/issues/51).

## Documentation

| Guide | What it covers |
|---|---|
| [Apps and waves](docs/workspace.md) | Teams, activity, app results, downloads and publishing |
| [Local development](docs/development.md) | Setup, worker images, build rules, runtime profiles and UI iteration |
| [Local Kubernetes](docs/local-kubernetes.md) | Docker Desktop deployment and building/deploying generated apps into their own namespaces |
| [AKS deployment and operations](docs/deployment.md) | Azure setup, topology, diagnostics, redeployment and teardown |
| [Testing and acceptance](docs/testing.md) | Repository checks, isolated runtime tests and evidence |

Product direction: [PRODUCT.md](PRODUCT.md). UI conventions: [DESIGN.md](DESIGN.md).
