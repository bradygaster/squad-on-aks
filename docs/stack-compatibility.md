# W01 pinned stack compatibility

This document is the executable dependency and tooling contract for
[issue #4](https://github.com/bradygaster/squad-on-aks/issues/4). It covers only
the Wave 1 package boundary and does not define application UI, casting behavior,
AppHost topology, containers, Azure resources, or deployment.

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
| ASP.NET Core Blazor Web App APIs | `10.0.9` | `Microsoft.AspNetCore.App` shared framework supplied by .NET SDK `10.0.301` | The probe compiles `AddRazorComponents()`, `AddInteractiveServerComponents()`, `MapRazorComponents<T>()`, and `AddInteractiveServerRenderMode()` without an additional UI package. |
| Playwright for .NET | `1.63.0` | NuGet `Microsoft.Playwright` | Stable package; browser installation and browser tests are intentionally deferred to the test work item. |
| MessagePack compatibility override | `3.1.12` | NuGet `MessagePack` and `MessagePack.Annotations` | Aspire 13.4.3 requests vulnerable `2.5.192`; the graph is centrally pinned to a patched compatible release instead of suppressing `NU1902`/`NU1903`. |

All direct and transitive NuGet dependencies are captured in committed
`packages.lock.json` files. CI restores with `RestoreLockedMode=true`.

## Deliberate Blazor package exclusion

Wave 1 uses the standard ASP.NET Core Blazor Web App APIs from the pinned .NET 10
shared framework. `Microsoft.AspNetCore.Components.AI` is optional and is
deliberately excluded:

- its published packages currently carry only `lib/net11.0` assets and .NET 11
  dependency groups;
- its source generator requires compiler 5.11 while SDK 10.0.301 supplies
  compiler 5.6, producing `CS9057`;
- suppressing that diagnostic would disable the generator rather than make the
  package a supported .NET 10 dependency.

Excluding the helper preserves the required .NET 10 runtime and does not replace
Aspire, Squad, or AG-UI. The application can build its Blazor UI directly over
the verified AG-UI client and server boundary.

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
`NoWarn` suppressions. A diagnostic must not be suppressed merely because a
package is preview; it must identify the specific pinned API whose use is
intentional.

## Reproduction

From a clean checkout with .NET SDK `10.0.301` installed:

```bash
dotnet tool restore
dotnet restore SquadOnAks.slnx --locked-mode
dotnet build SquadOnAks.slnx --no-restore
```

The stack probe compiles the current AG-UI server registration and endpoint
mapping, external `AGUIChatClient`, `SquadAgent` DI registration, standard
Blazor Web App service/endpoint APIs, and Playwright API in one .NET 10
consumer. It does not start Copilot, call a model, install a browser, or create
cloud resources.

Aspire behavior and API selection for this contract were checked only against
[aspire.dev](https://aspire.dev) and the
[official `microsoft/aspire` repository](https://github.com/microsoft/aspire).
