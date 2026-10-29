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

## Real Blazor evidence journey

The lifecycle layer hosts the production Blazor page and production AG-UI
endpoint with a deterministic agent that emits the same evidence contract as
the native Squad trace adapter. It drives casting, blocked activation and
remediation, native-shaped delegation/progress, the known fixture repair,
focused application and harness diffs, validation output, follow-up context,
visible `409 Conflict`, cancellation, and failure without calling a model or
external environment.

```bash
dotnet run --project test/SquadOnAks.AcceptanceTests -- --layer lifecycle-browser
```

It writes a Playwright trace and completion screenshot to
`artifacts/playwright/lifecycle`. A failed run also writes a full-page
screenshot, browser console log, and failed/error network diagnostics.

This layer also exercises artifact action scope descriptions and the production
publish form with intercepted GitHub owner/publication responses: private-only
destination review, keyboard focus, cancel-without-publishing, changed-details
review invalidation, repeated-click suppression, and success/partial-failure
repository links. These mocks are not evidence of browser GitHub authorization,
live publication, or same-repository retry.

## Live local

Start and discover the real application through Aspire first:

```bash
dotnet aspire start --isolated --non-interactive
dotnet aspire wait app --non-interactive
dotnet aspire describe --format Json --non-interactive
```

Then opt in with the discovered application URL:

```bash
SQUAD_ACCEPTANCE_LIVE_LOCAL=1 \
dotnet run --project test/SquadOnAks.AcceptanceTests -- \
  --layer live-local --base-url http://localhost:<port>
```

This layer is intentionally gated because it contacts the running application.
It is a shallow root/health/browser check, not proof of the complete real-model
work journey.

The native-casting layer runs real model turns against an uncast disposable
workspace. It verifies proposal-before-write, two refinements, explicit
confirmation, native review files, blocked activation, remediation, and
activation. It also accepts an unconfirmed `Casting` state after an earlier
proposal, but rejects existing native team files. It consumes model credits:

```bash
dotnet run --project test/SquadOnAks.AcceptanceTests -- \
  --layer native-casting --base-url http://localhost:<port>
```

Its trace and ready screenshot are written to
`artifacts/playwright/native-casting`. The layer activates the cast; it does
not prove specialist work, follow-up, cancellation, or pod replacement.

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
