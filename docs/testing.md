# Testing and acceptance

Run commands from the repository root. Scripted suites, real-model browser
acceptance, and deployed-cluster evidence are separate gates; a passing
mock or local run is not production acceptance.

## Validate the repository

Restore, build, and format-check:

```bash
dotnet restore SquadOnAks.slnx --locked-mode --nologo
dotnet build SquadOnAks.slnx \
  --configuration Release \
  --no-restore \
  --nologo
dotnet format SquadOnAks.slnx --verify-no-changes --no-restore
```

Test the deployment preflight and smoke-check assertions with local command
fixtures (no Azure resources or running cluster are needed):

```bash
bash test/aks-operations.sh
```

Run the native Squad/AG-UI casting boundary probe:

```bash
./scripts/run-native-casting-probe.sh
```

Run the deterministic production-host AG-UI boundary smoke:

```bash
./scripts/run-agui-smoke.sh
```

The smoke starts the same Blazor/AG-UI composition with a scripted
`IChatClient`, sends a turn through an external `AGUIChatClient`, and requires
the streamed response to cross `/ag-ui`.

Run the deterministic browser acceptance layer:

```bash
dotnet run \
  --project test/SquadOnAks.AcceptanceTests \
  --configuration Release \
  --no-build \
  -- \
  --layer deterministic
```

The acceptance host is test-only. It proves the planned browser and AG-UI
contract with scripted seams; it is not the deployed application.

## Workspace and runtime checks

Run the workspace regressions and production-UI lifecycle suite:

```bash
dotnet run --project test/SquadOnAks.WorkspaceTests
dotnet run --project test/SquadOnAks.AcceptanceTests -- --layer lifecycle-browser
```

Docker runtime checks are opt-in. Supply prepared immutable images, not tags:

```bash
SQUAD_RUNTIME_DOCKER_TEST_IMAGE="sha256:<prepared-worker-image-id>" \
SQUAD_RUNTIME_POSTGRES_TEST_IMAGE="sha256:<cached-postgres-image-id>" \
dotnet run --project test/SquadOnAks.WorkspaceTests
```

These cover actual SQLite/browser/restart acceptance and PostgreSQL
single-writer updates, failed-candidate preservation and retained rows.
The separate Docker validator checks opt in with `SQUAD_VALIDATION_DOCKER_TEST_IMAGE`.

## Production container reset proof

Run the local prerequisite gate with Docker available:

```bash
scripts/run-container-reset-tests.sh
```

The gate builds the `production` image, starts two sequential non-root
containers with read-only root filesystems and separate disposable `/workspace`
mounts, and drives casting, activation, work, evidence, readiness, redaction,
and shutdown through HTTP. It uses the disabled-by-default
`SQUAD_CONTAINER_PROBE_MODE` deterministic agent, so normal CI does not require
live model access. On failure, redacted container diagnostics are written under
`artifacts/container-tests/`.

The production image separately packages the official standalone
[Copilot CLI 1.0.103 release](https://github.com/github/copilot-cli/releases/tag/v1.0.103)
for Linux x64/ARM64, verifying the archive against pinned release SHA256 values.
Squad launches the full CLI with `--agent squad`; the SDK's bundled
`copilot-runtime` wrapper does not support that argument and is not used as
the production entrypoint. The container gate invokes the real packaged
`--version` and `--agent squad --help` commands without model access. These
checks prove CLI packaging/argument support, not a real-model conversation;
that remains the opt-in live browser acceptance gate in #17.

If Docker cannot reach the configured NuGet source, set
`SQUAD_CONTAINER_TEST_SDK_IMAGE` to an approved derivative of the pinned SDK
image that contains the locked global package cache; the final runtime stage
and all container assertions remain unchanged.

## Further evidence

- [Browser acceptance layers and live opt-ins](../test/SquadOnAks.AcceptanceTests/README.md)
- [Native casting probe](native-casting-probe.md)
- [Pinned stack compatibility](stack-compatibility.md)
- [Recorded AKS deployment evidence](evidence/w03/README.md)
- [Remaining reliability gates](https://github.com/bradygaster/squad-on-aks/issues/51)
