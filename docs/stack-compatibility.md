# W01 pinned stack compatibility

This document records the dependency investigation for
[issue #4](https://github.com/bradygaster/squad-on-aks/issues/4). W01 is
**blocked** by the required Blazor AI component package. It covers only the Wave
1 package boundary and does not define application UI, casting behavior, AppHost
topology, containers, Azure resources, or deployment.

## Compatibility matrix

| Component | Pinned version | Acquisition source | Constraint |
|---|---:|---|---|
| .NET SDK | `10.0.301` | `global.json` / Microsoft .NET SDK distribution | Exact SDK; roll-forward disabled. |
| Aspire CLI | `13.4.3` | NuGet tool `Aspire.Cli` | Exact local tool; roll-forward disabled. |
| Aspire AppHost SDK | `13.4.3` | NuGet `Aspire.AppHost.Sdk` | Exact project SDK. |
| Aspire AppHost hosting | `13.4.3` | NuGet `Aspire.Hosting.AppHost` | Stable package matching the CLI and AppHost SDK. |
| Aspire Kubernetes hosting | `13.4.3-preview.1.26305.13` | NuGet `Aspire.Hosting.Kubernetes` | Preview integration; exact build matching the Aspire 13.4.3 train. |
| Aspire AKS hosting | `13.4.3-preview.1.26305.13` | NuGet `Aspire.Hosting.Azure.Kubernetes` | Preview integration; exact build matching the Aspire 13.4.3 train. |
| Squad Agent Framework adapter | `1.0.0` | NuGet `Squad.Agents.AI` | Stable package is published and restores directly; no source checkout or upstream modification is required. |
| Microsoft Agent Framework AG-UI host | `1.24.0-preview.261006.1` | NuGet `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | Preview hosting package. The verified entry points are `AddAGUIServer()` and `MapAGUIServer(path, agent)`. Do not use the removed `Microsoft.Agents.AI.AGUI` hosting surface. |
| External AG-UI client | `1.0.1` | NuGet `AGUI.Client` | Stable external client. The verified client is `AGUIChatClient`; `AGUI.Abstractions`, `AGUI.Formatting`, and the hosting dependency `AGUI.Server` are pinned to the same release. |
| Blazor AI components | **No compatible version** | NuGet `Microsoft.AspNetCore.Components.AI` | Stop condition: every published version is a .NET 11 package. The `0.1.0-preview.1.26466.103` package has only `lib/net11.0`, depends on `Microsoft.AspNetCore.Components.Web` 11.0.0 RC, and its source generator requires compiler 5.11 while .NET SDK 10.0.301 supplies compiler 5.6. |
| Playwright for .NET | `1.63.0` | NuGet `Microsoft.Playwright` | Stable package; browser installation and browser tests are intentionally deferred to the test work item. |
| MessagePack compatibility override | `3.1.12` | NuGet `MessagePack` and `MessagePack.Annotations` | Aspire 13.4.3 requests vulnerable `2.5.192`; the graph is centrally pinned to a patched compatible release instead of suppressing `NU1902`/`NU1903`. |

All direct and transitive dependencies in the compatible subset are captured in
committed `packages.lock.json` files. CI restores with
`RestoreLockedMode=true`. The Blazor AI package is deliberately not referenced:
adding a package that only appears to restore while its compile assets and
source generator are unusable on .NET 10 would be false compatibility evidence.

## Stop-condition evidence

The required .NET 10 and Blazor AI component matrix cannot currently be built
truthfully:

1. NuGet publishes `Microsoft.AspNetCore.Components.AI`
   `0.1.0-preview.1.26459.102`, `0.1.0-preview.1.26466.103`, and an
   `11.0.0-preview` line.
2. Both `0.1.0` packages declare `net11.0` dependency groups and carry only a
   `lib/net11.0` assembly.
3. Building either `0.1.0` package with SDK `10.0.301` emits `CS9057`: its
   `Microsoft.AspNetCore.Components.AI.SourceGenerators.dll` references compiler
   `5.11.0.0`, newer than SDK 10.0.301's compiler `5.6.0.0`.
4. Suppressing `CS9057` would merely disable the package source generator; it
   would not make the .NET 11 component assembly a supported .NET 10 asset.

Per issue #4, W01 stops here rather than upgrading the application to .NET 11,
substituting another component library, or pretending the warning-only restore
is a supported package combination.

## Squad and Copilot payload contract

`Squad.Agents.AI` 1.0.0 is consumed from NuGet. The published package resolves
`Microsoft.Agents.AI.GitHub.Copilot` 1.23.0 and `GitHub.Copilot.SDK` 1.0.14.
Its package contains `buildTransitive/Squad.Agents.AI.props`, which pins the
SDK build target used by downstream consumers. That target downloaded Copilot
CLI 1.0.85 during the verified build and copied `copilot`,
`copilot-runtime`, `runtime.node`, and the native runtime library into the
consumer output for `osx-arm64`. Other runtime identifiers receive their
corresponding payload. Consumers must not separately select or float a Copilot
CLI payload version.

The AG-UI host resolves Microsoft Agent Framework 1.24.0 while
`Squad.Agents.AI` was built against its 1.23.0 Copilot adapter. The .NET 10
probe compiles the shared `AIAgent` boundary, `AddSquadAgent`,
`AddAGUIServer`, and `MapAGUIServer` together without diagnostics; the lock file
records the exact mixed-version graph.

The supported credential paths are the GitHub Copilot SDK paths:

- a locally signed-in Copilot/GitHub user for local development;
- a Copilot-supported token supplied by the host through `GitHubTokenProvider`,
  `GitHubToken`, or `ConfigureCopilotClient`.

A classic `ghp_` personal access token is not a supported Copilot SDK
credential. Credentials must not be embedded in source, package configuration,
logs, or validation artifacts. Enabling `SquadAgentOptions.TraceEvents` emits a
warning because verbose operational traces can contain sensitive details.

## Preview and evaluation diagnostics

The AppHost and stack probe keep warnings as errors. There are no blanket
`NoWarn` suppressions. In particular, `CS9057` from the Blazor AI package is not
suppressed because doing so would hide a real compiler/package incompatibility.
A future diagnostic must not be suppressed merely because a package is preview;
it must identify the specific pinned API whose use is intentional.

## Reproduction

From a clean checkout with .NET SDK `10.0.301` installed:

```bash
dotnet tool restore
dotnet restore SquadOnAks.slnx --locked-mode
dotnet build SquadOnAks.slnx --no-restore
```

The compatible-subset probe compiles the current AG-UI server registration and
endpoint mapping, external `AGUIChatClient`, `SquadAgent` DI registration, and
Playwright API in one .NET 10 consumer. It does not start Copilot, call a model,
install a browser, or create cloud resources. It is not W01 completion evidence
until a supported .NET 10 Blazor AI component package exists.

Aspire behavior and API selection for this contract were checked only against
[aspire.dev](https://aspire.dev) and the
[official `microsoft/aspire` repository](https://github.com/microsoft/aspire).
