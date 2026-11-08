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

The lifecycle layer hosts the production Squad page and AG-UI endpoint with a deterministic agent. It exercises the app list → brief → clarification → Build my app → wave journey, structured work results, reopenable history, reload persistence, cancellation and failure recovery. It verifies that root is read-only, team setup stays internal, and an activated app can retry a failed first-work submission without another setup or duplicate wave. Real native team names/roles, recorded contributors, app-isolated rosters, roster-load failure recovery and section navigation are covered. It asserts the absence of source-code/infrastructure surfaces, keyboard validation, and no horizontal page overflow or scrollable app panels at 1440px, 390px and 360px.

```bash
dotnet build test/SquadOnAks.AguiSmoke
dotnet run --project test/SquadOnAks.AcceptanceTests -- --layer lifecycle-browser
```

Screenshots (first-run, active-app, and publishing at desktop and phone sizes), a trace, and a failure screenshot if needed are written to `.playwright-mcp/daydream/`. The preview response and a small sample app are intercepted because the fixture does not start a preview service. Those screenshots verify the real viewing-window UI, not a live generated app.

Publishing uses intercepted owner/publication responses: private-only destination review, validation with `aria-invalid`, native-dialog keyboard focus and Escape, cancel-without-publishing, changed-details review invalidation, duplicate suppression, safe failure messages and a success link. These mocks are not evidence of live publication or browser account authorization. The deterministic layer above remains a separate test-only backend contract UI, not Daydream's user interface.

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
