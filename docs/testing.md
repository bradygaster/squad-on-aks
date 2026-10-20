# Playwright acceptance layers

Wave 1 uses three explicit Playwright for .NET layers.

| Layer | Default CI | Boundary | Purpose |
|---|---|---|---|
| `Deterministic` | Runs | Real Blazor UI and AG-UI HTTP endpoint with scripted agent/event/workspace seams | Prove browser behavior without model credits or Azure resources. |
| `LiveLocal` | Skipped | `aspire start --isolated` with real Squad and Copilot authentication | Prove native casting and fixture work before deployment. |
| `LiveAks` | Skipped | Public AKS URL plus explicit `kubectl` verification | Prove deployed behavior, internal dashboard exposure, and pod replacement. |

## Deterministic contract

The deterministic host is a real interactive Blazor application. Its page uses
`AGUIChatClient` to call the mapped `MapAGUIServer` endpoint. The endpoint wraps a
scripted `IChatClient` as an `AIAgent`, so the browser crosses the same HTTP and
streaming boundary as production without introducing a second production runtime.

Run:

```bash
dotnet build SquadOnAks.slnx -c Release
dotnet run --project eng/SquadOnAks.PlaywrightCli -- install chromium
dotnet test test/SquadOnAks.Playwright/SquadOnAks.Playwright.csproj \
  -c Release --no-build \
  --filter 'Layer=Deterministic'
```

The suite uses accessible role/name selectors and a fresh browser context. It always
writes the following ignored diagnostics under `artifacts/playwright/deterministic/`:

- `trace.zip` with DOM snapshots, screenshots, and sources;
- `final.png`;
- `console.log`;
- `network-failures.log`.

Open the trace with:

```bash
dotnet run --project eng/SquadOnAks.PlaywrightCli -- \
  show-trace artifacts/playwright/deterministic/trace.zip
```

Later work items extend the scripted seams with the fixed scenario vocabulary:
`Starting`, `Needs cast`, `Casting`, `Cast review`, `Ready`, `Active`,
`Cancelling`, `Failed`, and `Replacing`. Test data uses the mission and remediation
prompts from issue #3. Selectors remain stable accessible names rather than CSS
implementation details.

## Live opt-in rules

Live tests remain skipped in normal test runs. Enabling them requires a deliberate
operator command, real Copilot authentication, and either:

- a resource discovered after `aspire start --isolated`; or
- an explicit AKS public URL and matching `kubectl` context.

Normal CI must never set these inputs, call a model, consume Copilot credits, or create
Azure resources.
