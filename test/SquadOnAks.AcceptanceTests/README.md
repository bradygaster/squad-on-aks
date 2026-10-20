# Playwright acceptance layers

W04 defines one browser contract in three explicitly separated execution layers.
The deterministic layer is the only default layer and cannot start Copilot,
Aspire deployment, `kubectl`, or Azure.

## Deterministic

The test-only `SquadOnAks.AcceptanceHost` renders a real Blazor page and maps a
scripted `/ag-ui` streaming endpoint. The request uses AG-UI run input fields,
and the SSE response uses standard `RUN_*`, `TEXT_MESSAGE_*`, and `CUSTOM`
events. Its scripted agent, event stream, and workspace are injected at the
same boundaries later production work will implement; it is not referenced by
`src/` and is not a production fallback.

```bash
dotnet run --project test/SquadOnAks.AcceptanceTests -- --layer deterministic
```

Every scenario creates an isolated Chromium browser context. Selectors use
accessible roles and names. Each run writes a Playwright trace with DOM
snapshots, screenshots, and sources to `artifacts/playwright/deterministic`.
On failure it also writes a full-page screenshot plus browser console and
failed/error network diagnostics.

Stable controls and regions:

| Boundary | Accessible selector |
|---|---|
| Lifecycle | `role=status` |
| Errors/busy/validation | `role=alert` |
| Mission | label `Mission` |
| Harness edit path/content | labels `Harness path`, `Harness content` |
| Streamed response | list `Conversation stream` |
| Native activity evidence | list `Specialist activity` |
| Generated harness evidence | list `Harness evidence` |
| Application-change evidence | list `Application evidence` |

The deterministic fixture uses `FLIGHT`, `GNC`, and `RETRO`; allow-lists only
`.squad/team.md`, `.squad/routing.md`, and the active GNC/RETRO charters; and
uses `src/Fixture/Repair.cs` as the scripted application change.

## Live local

Start and discover the real application through Aspire first:

```bash
aspire start --isolated
aspire wait squad-on-aks
aspire describe --format Json
```

Then opt in with the discovered application URL:

```bash
SQUAD_ACCEPTANCE_LIVE_LOCAL=1 \
dotnet run --project test/SquadOnAks.AcceptanceTests -- \
  --layer live-local --base-url http://localhost:<port>
```

This layer is intentionally gated because later work supplies the real Blazor,
`Squad.Agents.AI`, Copilot authentication, native casting, and fixture flow.

## Live AKS

After an explicitly requested deployment, pass the discovered public
application URL:

```bash
SQUAD_ACCEPTANCE_LIVE_AKS=1 \
dotnet run --project test/SquadOnAks.AcceptanceTests -- \
  --layer live-aks --base-url http://<public-address>
```

The live-AKS layer is the only layer allowed to coordinate later acceptance
with `kubectl` for external pod replacement and backend evidence. It never runs
without both the live-AKS gate and an explicit URL. Default build and
deterministic validation therefore consume no model credits and create no
Azure resources.
