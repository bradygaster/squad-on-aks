# Squad on AKS

**Run governed, event-driven AI engineering Squads as ephemeral Azure Kubernetes Service Jobs while durable Azure services preserve workflow state, approvals, effects, and evidence.**

Squad on AKS turns a GitHub request into a traceable unit of work. A durable .NET control service validates policy, records intent, and places work on an Azure Service Bus queue. Managed KEDA creates a short-lived AKS Job that hosts the existing Squad/Copilot runtime inside a small .NET worker shell. The Job produces a draft pull request or a governed evidence report, then disappears. Azure SQL remains authoritative and GitHub holds the reviewable output.

From a clean clone, a developer can authenticate and submit a deployment in **three manual steps and under five minutes of human interaction**. A new Azure environment normally takes **30-45 minutes** to provision asynchronously. Against an existing environment, the sample is ready to exercise in **about 10 minutes**.

![Editorial architecture diagram showing GitHub and squad-aks requests entering a durable Container Apps control application, Azure SQL as business authority, Service Bus as transport, and a disposable AKS Job running the unchanged Squad runtime](https://github.com/user-attachments/assets/22122e7d-6445-44ac-a2ec-3fafb8d49b36)

```text
┌─ Remediation Flight Control ─────────────────────────────────────────────────┐
│ checkout-api #184 · HTTP dependency remediation              ACTIVE  06:41 │
│                                                                            │
│  ✓ Request accepted      ✓ Policy passed       ✓ AKS Job scheduled         │
│  ✓ Repository cloned     ● Running tests       ○ Publish draft PR          │
│                                                                            │
│  Squad: remediation@1.2.0        Attempt: sqa-01J9YQ7M8K                   │
│  Branch: squad/184-http-fix      Budget: $1.82 / $8.00                    │
│  Trace:  7b1c…e93                [Open evidence] [Request cancellation]    │
└────────────────────────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> Squad is not rewritten in .NET, and this sample does not add deployment commands to the upstream Squad CLI. This repository ships its own `./squad-aks` deployment and operations CLI. Each AKS Job runs the existing Squad/Copilot runtime inside a small Azure/.NET worker shell that handles leases, identity, telemetry, cancellation, evidence, and process lifecycle.

## What you will build and see

The sample deploys a complete **Remediation Flight Control** scenario:

1. A GitHub issue, webhook, or repository-local CLI request reports work to be done.
2. The control plane authenticates the caller, resolves the selected Squad configuration, checks repository and budget policy, and records the request in Azure SQL.
3. A Service Bus message tagged with the selected Squad route becomes demand for a managed KEDA `ScaledJob`.
4. AKS schedules an isolated Job. Node Auto-Provisioning (NAP) adds node capacity when the cluster does not already have a suitable node.
5. The .NET worker shell locks one message, acquires the SQL attempt lease, materializes the exact repository revision, starts the existing Squad runtime, and streams correlated logs, traces, status, usage, and cost signals.
6. Squad investigates the repository, implements the remediation, validates it, and either opens one draft pull request or stops at a governed boundary with an evidence report.
7. The worker records GitHub effects and a terminal evidence record in Azure SQL before settling the message and exiting. Flight Control and the GitHub-native workflow show the same truthful terminal result.

The completed experience includes:

- **GitHub-native entry points** for requests, status, approval, rejection, and cancellation. Flight Control is useful, but never required.
- **Flight Control** for operators who need queue state, active work, approvals, cancellation, evidence, cost, and reconciliation in one calm view.
- **Demand-driven execution** with managed KEDA Jobs and NAP-backed compute elasticity.
- **N logical Squads** selected from versioned configuration while sharing the same cluster, control plane, identity foundation, and observability stack.
- **Durable authority outside AKS** so rescheduling a pod, deleting a Job, or losing a node cannot invent, erase, or finalize business state.

## Minimum useful architecture

The sample keeps the workflow topology to four concepts:

1. **AKS Automatic** runs disposable Jobs and supplies managed KEDA, Node Auto-Provisioning, and workload identity.
2. **Azure Service Bus** holds admitted work until a Job can claim it.
3. **Azure SQL** owns requests, leases, approvals, cancellation, effects, and terminal evidence.
4. **One .NET control application on Azure Container Apps** hosts admission, reconciliation, the GitHub integration, and Flight Control.

The deployment also creates supporting resources such as a container registry, Key Vault, and Azure Monitor workspace. They are implementation necessities, not additional workflow tiers users must understand or operate individually.

This is the starting point, not a speculative production platform. New components belong only when a measured user, scale, security, or availability requirement cannot be met by this topology.

## Quickstart: clone to deployment submission in three steps

### Prerequisites

Use macOS, Linux, or Windows with WSL 2 and install:

- [Git](https://git-scm.com/downloads)
- [Azure CLI 2.76 or later](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [GitHub CLI 2.78 or later](https://cli.github.com/)
- An Azure subscription where you can create a resource group and assign roles at that resource-group scope
- Permission to create a GitHub App in the target organization, or the ID and private-key secret for an existing compatible GitHub App

You do **not** need `kubectl`, Helm, Bicep, the .NET SDK, or Node.js for the standard deployment path. The repository's pinned dev container contains those tools for contributors.

### Authentication assumptions

`./squad-aks login` is the only interactive authentication command. It:

- opens Azure device-code or browser authentication through Azure CLI;
- lets you select the Azure tenant and subscription;
- runs `gh auth login --web` when GitHub CLI has no valid session;
- verifies that the GitHub identity can administer the selected organization or use the supplied GitHub App;
- stores only non-secret environment selection under `.squad-aks/local/`;
- places generated deployment secrets in Azure Key Vault, never in the repository or local configuration.

The signed-in user is used only for bootstrap. Running services use Microsoft Entra workload identities and the GitHub App installation identity. No long-lived Azure client secret or personal access token is created.

### 1. Clone

```bash
git clone https://github.com/bradygaster/squad-on-aks.git
cd squad-on-aks
```

Expected:

```text
Cloning into 'squad-on-aks'...
✓ repository ready
```

### 2. Log in and bootstrap local context

```bash
./squad-aks login --github-org YOUR_ORG --subscription YOUR_SUBSCRIPTION
```

Expected:

```text
✓ Azure identity          brady@example.com
✓ Tenant                  Contoso Engineering (72f9…db47)
✓ Subscription            AI Samples (3f2d…019a)
✓ GitHub identity         @bradygaster
✓ GitHub organization     YOUR_ORG
✓ Required providers      registered
✓ Local context           .squad-aks/local/default.json

Ready to deploy. No cloud resources have been created yet.
```

### 3. Submit the deployment

```bash
./squad-aks deploy --environment demo --location eastus2 --wait-ready
```

Within five minutes, the command validates configuration, creates the deployment record, and submits provisioning:

```text
Squad on AKS · demo · eastus2

✓ Configuration validated
✓ Deployment identity prepared
✓ Provisioning submitted       deployment: sqdep-01J9Y6A4P2
✓ Progress endpoint available  https://squad-demo.example.net/deployments/sqdep-01J9Y6A4P2

Human interaction complete in 02:37.
Provisioning continues asynchronously and usually takes 30-45 minutes.

  10:14:08  resource group       ready
  10:16:31  network              ready
  10:25:44  AKS control plane    ready
  10:31:17  worker capacity      ready
  10:35:52  data and messaging   ready
  10:40:06  durable control      ready
  10:43:19  KEDA execution path  verified
  10:43:41  sample request       ready

✓ Environment ready
Flight Control: https://squad-demo.example.net/
Run sample:     ./squad-aks run remediation --sample checkout-regression
```

`--wait-ready` streams progress but does not require attention. You can close the terminal after **Provisioning submitted** and later run `./squad-aks status --environment demo`.

If `demo` already exists, `deploy` performs an idempotent configuration and health reconciliation. That path usually reaches **Environment ready** in 5-10 minutes.

## What `squad-aks deploy` provisions

The deploy command executes the repository's signed, pinned infrastructure and application release:

| Area | Provisioned capability |
|---|---|
| Resource boundary | One Azure resource group per environment with consistent tags, expiration, and cost attribution |
| Execution | AKS Automatic with managed KEDA, NAP, workload identity, execution policy, and ephemeral Squad Job templates |
| Control application | One .NET application on Azure Container Apps containing admission, dispatch, reconciliation, GitHub integration, and Flight Control |
| Durable state | Azure SQL for requests, attempts, leases, transitions, approvals, cancellation, effects, and terminal evidence |
| Messaging | One Azure Service Bus work queue and dead-letter queue with managed-identity access |
| Supporting resources | Azure Container Registry, Key Vault, Application Insights, and Log Analytics |
| Sample | Versioned remediation Squad configuration, deterministic fixtures, policy defaults, and an end-to-end smoke test |

The deployer prints every planned role assignment before applying it. Use `./squad-aks deploy --plan` to review changes without provisioning.

## First run: remediate a checkout regression

Start the included scenario after the environment reports ready:

![Six-stage request flow from admission and durable intent through Service Bus, an ephemeral AKS Job, effect reconciliation, and GitHub review](https://github.com/user-attachments/assets/87f55447-2b39-4d9d-975a-15e874e25b52)

```bash
./squad-aks run remediation \
  --environment demo \
  --sample checkout-regression \
  --repo YOUR_ORG/checkout-api
```

Expected:

```text
✓ Request accepted       request: sqr-01J9YQ4N3C
✓ Squad resolved         remediation@1.2.0
✓ Policy passed          repository allowlist, $8.00 budget, draft PR only
✓ Work queued            route: remediation

Follow:
  ./squad-aks run watch sqr-01J9YQ4N3C
  https://squad-demo.example.net/runs/sqr-01J9YQ4N3C
```

Watch the request move from durable intent to ephemeral compute:

```bash
./squad-aks run watch sqr-01J9YQ4N3C
```

```text
WAITING   10:51:02  Request committed to Azure SQL
WAITING   10:51:03  Service Bus message available
WAITING   10:51:08  KEDA requested an execution Job
WAITING   10:51:11  NAP is adding compatible node capacity
ACTIVE    10:53:47  Message locked; attempt lease acquired by sqa-01J9YQ7M8K
ACTIVE    10:53:50  Existing Squad runtime started
ACTIVE    10:54:31  Repository materialized at 4d9e12a
ACTIVE    10:57:09  Regression reproduced
ACTIVE    11:00:42  Candidate fix implemented
ACTIVE    11:02:18  Validation passed: 183 tests
ACTIVE    11:03:04  Draft pull request recorded: YOUR_ORG/checkout-api#184
SUCCESS   11:03:07  Effects reconciled; terminal evidence durable; message settled

✓ Completed in 12m 05s · compute $0.41 · model budget $2.36 / $8.00
```

The draft pull request contains the implementation, validation summary, request ID, trace link, and evidence manifest. GitHub remains a complete interaction surface: a developer can request, follow, approve, reject, cancel, and review work without opening Flight Control.

## Remediation Flight Control

Flight Control is a projection of durable state, not a second scheduler and not an alternative source of truth.

### Deployment view

```text
Environment  demo / eastus2                                      HEALTHY

Control API       Healthy       SQL authority       Connected
AKS execution     Ready         Service Bus         0 active / 0 dead-letter
KEDA              Ready         GitHub App          Installed in 12 repositories
NAP               Enabled       Last reconciliation 18 seconds ago

Recent deployment events
  ✓ Infrastructure release 1.0.0 applied
  ✓ Worker image sha256:8c1…92e admitted
  ✓ Synthetic remediation completed in 03:14
```

### Queue and run states

| State shown | What it means | Operator action |
|---|---|---|
| **Waiting** | The request is durably accepted but has no active lease. It may be queued, policy-delayed, waiting for capacity, or retrying after a recoverable failure. | Inspect the reason and estimated capacity or provisioning progress. |
| **Active** | One Job holds the current SQL lease and reports heartbeats. | Follow phases, logs, traces, budget, and evidence. |
| **Blocked** | Progress requires an explicit human decision or an external dependency. No approval is inferred. | Review evidence, approve, reject, provide input, or cancel. |
| **Cancellation requested** | Intent to stop is durable, but the matching Job has not acknowledged it. | Wait or inspect the active lease and worker heartbeat. |
| **Cancelling** | The worker acknowledged the request and is containing work. External effects may still require reconciliation. | Review cancellation evidence. |
| **Cancelled** | Work is contained, effects and evidence are reconciled, the lease is closed, and queue settlement is complete. | Open the terminal record. |
| **Succeeded** | Required validation, effects, evidence, and settlement are recorded. | Review the draft PR or report. |
| **Failed** | The terminal failure and available evidence are durable. | Retry from the recorded generation when policy permits. |

### Approval

When a Squad reaches a protected action, Flight Control shows the exact proposed effect:

```text
BLOCKED · Approval required

Request:       sqr-01J9YQ4N3C
Proposed:      Admit remediation and permit one draft pull request
Repository:    YOUR_ORG/checkout-api
Base revision: main @ 4d9e12a
Digest:        f31c…90aa
Policy:        draft-pr-approval
Expires:       on source, policy, or configuration change

[Approve and admit run] [Reject with reason]
```

Approval is optimistic, versioned, and digest-bound. If the source revision, Squad configuration, or policy changes, the previous approval cannot be reused.

### Truthful cancellation and reconciliation

Cancellation is deliberately not displayed as instantaneous:

```text
11:07:12  CANCELLATION_REQUESTED  Durable intent recorded
11:07:14  CANCELLING             Matching worker acknowledged
11:07:16  CANCELLING             Child work stopped; pod containment confirmed
11:07:19  CANCELLING             GitHub effects compared with effect journal
11:07:21  CANCELLING             Terminal evidence committed
11:07:23  CANCELLED              Lease closed; effects reconciled; message settled
```

Deleting an AKS Job does not mark a run cancelled. If a Job disappears before acknowledging cancellation, the reconciler waits for lease expiry, inspects recorded effects, and either resumes containment in a new attempt or records a failed/uncertain outcome. Flight Control shows that uncertainty rather than inventing success.

## Multiple Squads, one platform

Squad definitions are versioned data. Adding a logical Squad does not create another AKS cluster, SQL database, Service Bus namespace, control plane, or observability stack.

The included remediation definition looks like this:

```yaml
apiVersion: squad.azure.dev/v1
kind: Squad
metadata:
  name: remediation
  version: 1.2.0
spec:
  runtime:
    image: ghcr.io/bradygaster/squad-runtime:1.2.0
    workloadClass: interactive
    timeout: 45m
  routing:
    route: remediation
    concurrency: 20
  repositories:
    allow:
      - YOUR_ORG/checkout-api
  policy:
    maxRunCostUsd: 8
    pullRequests: draft-only
    protectedEffects:
      - github.pull_request.create
  agents:
    - dependency-analyst
    - implementer
    - test-engineer
    - security-reviewer
```

Register another Squad from version-controlled configuration:

```bash
./squad-aks squad add platform-upgrade \
  --environment demo \
  --file squads/platform-upgrade/squad.yaml
```

```text
✓ Schema valid              squad.azure.dev/v1
✓ Policy valid              3 repositories, $12.00 maximum
✓ Runtime image admitted    sha256:601…d2b
✓ Configuration published   platform-upgrade@1.0.0
✓ Routing key ready         platform-upgrade

No infrastructure resources were cloned.
```

Select it explicitly:

```bash
./squad-aks run platform-upgrade \
  --environment demo \
  --repo YOUR_ORG/catalog-api \
  --request "Upgrade the service to the approved .NET release"
```

Requests capture an immutable Squad version. Updating `platform-upgrade` affects new requests only; active runs continue with the version they started with. The shared queue, SQL records, identities, quotas, and telemetry are logically partitioned by environment, Squad, repository, and request.

## Architecture

```mermaid
flowchart LR
    REQUESTS["GitHub + squad-aks<br/>issues · webhooks · CLI"] --> CONTROL

    subgraph Durable["Durable control outside AKS"]
        CONTROL["Container Apps<br/>.NET control application"]
        SQL[("Azure SQL<br/>business authority")]
        SB[["Service Bus<br/>one work queue"]]
    end

    subgraph Execution["Disposable execution"]
        JOB["AKS Job<br/>worker shell + existing Squad<br/>KEDA demand · NAP capacity"]
    end

    CONTROL --> SB
    SB --> JOB
    CONTROL <--> SQL
    JOB <--> SQL
    JOB --> OUTPUT["GitHub<br/>draft PR + evidence"]

    classDef authority fill:#fff4ef,stroke:#eb6c36,color:#2d3142,stroke-width:2px
    class SQL authority
```

The arrows do not assign authority. KEDA translates queue demand into Jobs, and NAP translates unschedulable pods into node capacity. **Azure SQL leases, compare-and-swap transitions, and the effect journal remain the business authority.**

Azure Container Registry, Key Vault, Application Insights, and Log Analytics support this flow but are intentionally omitted from the topology because they do not add workflow stages.

### Run lifecycle

```mermaid
stateDiagram-v2
    [*] --> Received
    Received --> Rejected: invalid or unauthorized
    Received --> AwaitingApproval: protected operation
    AwaitingApproval --> Queued: digest-bound approval
    AwaitingApproval --> Rejected: rejected or expired
    Received --> Queued: policy permits admission
    Queued --> Starting: Job locks message and acquires lease
    Starting --> Active: source and approval revalidated
    Starting --> StaleRevision: source changed
    Active --> Blocked: test, policy, or reviewer boundary
    Active --> Publishing: outcome satisfies gates
    Active --> Cancelling: cancellation observed
    Queued --> Cancelling: cancelled before active attempt
    Publishing --> Reconciling: external effects attempted
    Cancelling --> Reconciling: exact attempt contained
    Reconciling --> Succeeded: effects and evidence durable
    Reconciling --> Failed: terminal error durable
    Reconciling --> Cancelled: cancellation reconciled and settled
    Active --> Queued: lease lost and retry remains eligible
    Succeeded --> [*]
    Failed --> [*]
    Cancelled --> [*]
    Blocked --> [*]
    StaleRevision --> [*]
    Rejected --> [*]
```

## Operations

### Status and health

```bash
./squad-aks status --environment demo
```

```text
Environment demo · eastus2 · release 1.0.0
HEALTHY

Control API       healthy     SQL leases       0 stale
Dispatcher        healthy     Queue            0 active / 0 dead-letter
Reconciler        healthy     AKS Jobs         0 active
GitHub App        healthy     Last synthetic   success · 18m ago
```

Use `./squad-aks status --watch` during provisioning or an incident.

### Logs and traces

```bash
./squad-aks logs sqr-01J9YQ4N3C --follow
./squad-aks trace sqr-01J9YQ4N3C --open
```

Logs are structured and redacted before ingestion. Every control-plane operation, Service Bus delivery, SQL transition, AKS Job, GitHub effect, and evidence record carries the request ID, work ID, attempt ID, and W3C trace context.

For cluster-level investigation:

```bash
az aks command invoke \
  --resource-group rg-squad-demo-eastus2 \
  --name aks-squad-demo \
  --command "kubectl get jobs,pods -n squad-execution"
```

The standard path does not grant developers direct cluster credentials. `az aks command invoke` is audited and can be disabled in stricter environments.

### Cost

```bash
./squad-aks cost --environment demo --period 7d --group-by squad
```

```text
SQUAD               RUNS   COMPUTE   PLATFORM SHARE   MODEL*    TOTAL*
remediation            18     $6.82           $19.40   $38.16    $64.38
platform-upgrade        4     $2.11            $4.31   $11.02    $17.44

* Model cost is estimated from provider usage records and reconciled when final
  billing data becomes available.
```

The control application, Azure SQL, Service Bus, registry, and telemetry have standing cost even when no Squad Job is running. Execution capacity scales with demand; NAP is not business authority and is not a scale-to-zero mechanism for the entire platform.

### Redeploy and upgrade

```bash
git pull --ff-only
./squad-aks deploy --environment demo --location eastus2 --wait-ready
```

Deployment is idempotent. The command shows the infrastructure and application plan, applies compatible database migrations before switching traffic, verifies the execution path with a synthetic run, and retains the previous application revision for rollback.

```bash
./squad-aks deploy --environment demo --release 1.0.0 --rollback
```

### Cleanup

```bash
./squad-aks destroy --environment demo --plan
./squad-aks destroy --environment demo --confirm demo
```

Destroy first disables new intake, drains or explicitly cancels active work, reconciles effects, exports the final evidence index, removes application and infrastructure resources, and verifies deletion. Key Vault soft-deleted secrets, Azure SQL backups, and Log Analytics retention follow the policies shown in the destroy plan and may outlive the resource group.

### Troubleshooting

| Symptom | Check | Resolution |
|---|---|---|
| Deployment remains on `AKS control plane` | `./squad-aks status --environment demo --deployment` | Confirm regional AKS quota and required resource providers; resume with the same `deploy` command. |
| Runs remain `Waiting: capacity` | `./squad-aks status --environment demo --capacity` | Review NAP constraints, regional VM availability, subscription vCPU quota, and the Squad workload class. |
| Runs remain `Waiting: queued` | `./squad-aks queue inspect --environment demo --squad remediation` | Check per-Squad concurrency, budget windows, queue health, and dispatcher status. |
| Job restarts after node loss | `./squad-aks run inspect REQUEST_ID --leases` | Normally no action. The SQL lease and effect journal prevent an unrecorded takeover. |
| GitHub effect is uncertain | `./squad-aks run reconcile REQUEST_ID` | The reconciler queries GitHub by idempotency marker before retrying or finalizing. |
| Cancellation does not finish | `./squad-aks run inspect REQUEST_ID --cancellation` | Inspect worker heartbeat, lease expiry, exact-attempt containment, and unresolved effects; do not delete the Job as a substitute for reconciliation. |
| No telemetry appears | `./squad-aks doctor --environment demo --check observability` | Validate managed identity, ingestion endpoints, sampling policy, and workspace retention. |
| Authentication fails | `./squad-aks login --reset` | Reauthenticate to the intended tenant, subscription, and GitHub organization. |

Run `./squad-aks doctor --environment demo` for a read-only platform, identity, networking, GitHub, data, queue, execution, and telemetry diagnostic.

## Security and identity

- **Microsoft Entra workload identity** authenticates AKS Jobs and control services to Azure. There are no application client secrets.
- **Distinct managed identities** separate deployment, the control service, KEDA, and worker execution permissions.
- **GitHub App installation tokens** are short-lived and scoped to approved repositories and permissions. Azure workload identity does not authenticate a Job to GitHub.
- **Repository allowlists and immutable Squad versions** are evaluated before work is queued.
- **Azure SQL leases and optimistic concurrency** prevent two Jobs from simultaneously owning one request or mutable Squad state partition.
- **The effect journal** records intent, idempotency keys, attempts, observed provider state, and reconciliation outcome for GitHub and other side effects.
- **Network policy** limits worker traffic in the sample.
- **Secrets are references**, resolved just in time from Key Vault and filtered from logs, traces, evidence, and model context.
- **Admission policy** permits only pinned runtime images, approved identities, resource limits, and supported Job templates.
- **Human approvals are versioned** and apply to a specific proposed effect, not to a mutable run in general.
- **Audit and evidence are append-oriented** and correlated from request through terminal settlement.

### Explicit non-goals

Squad on AKS does not:

- replace Squad or reimplement its agent runtime in .NET;
- use Kubernetes, KEDA, NAP, pod state, or queue depth as business authority;
- promise instantaneous or guaranteed cancellation of in-flight external effects;
- make Flight Control mandatory for GitHub-native workflows;
- create an AKS cluster or full service stack per logical Squad;
- provide arbitrary hostile multi-tenant code execution inside one shared cluster;
- merge pull requests or deploy generated changes to production by default;
- eliminate standing Azure platform cost;
- treat model-generated output as trusted without validation and policy checks;
- make preview Azure or Kubernetes features mandatory for the baseline.

## Repository layout

```text
.
├── README.md                         # This guide
├── squad-aks                         # Repository-local deployment and operations CLI
├── .devcontainer/                    # Pinned development toolchain
├── .github/
│   ├── workflows/                    # Build, scan, integration, release, and docs checks
│   └── dependabot.yml
├── cli/                              # squad-aks command implementation
├── deploy/
│   ├── bicep/                        # Azure infrastructure modules and environment entry point
│   ├── helm/                         # KEDA Job templates and AKS policies
│   └── releases/                     # Signed application release manifests
├── src/
│   ├── Squad.Control/                # Admission, dispatch, token brokering, reconciliation, and API
│   ├── Squad.Worker/                 # .NET lifecycle shell around existing Squad
│   └── Squad.FlightControl/          # Optional operator web experience
├── squads/
│   ├── remediation/                  # Finished remediation Squad definition
│   └── platform-upgrade/             # Example second logical Squad
├── samples/
│   ├── checkout-regression/          # Reproducible first-run request
│   ├── inventory-contract-block/     # Governed no-PR outcome
│   ├── storefront-approval/          # Pre-compute approval outcome
│   └── github-app/                   # GitHub App manifest and installation guide
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── contract/
│   └── end-to-end/
└── docs/
    ├── operations/
    ├── security/
    └── architecture/
```

## Architecture decisions and tradeoffs

### Why AKS

Squad work is container-native, bursty, long-running, and benefits from strong per-Job resource, identity, network, scheduling, and optional sandbox boundaries. AKS provides a first-class Kubernetes Job model, managed KEDA demand activation, NAP-backed capacity, workload classes, policy controls, and direct operational visibility. The tradeoff is a larger platform surface and more standing cost than a serverless-only design.

The sample uses AKS Automatic to minimize cluster setup while retaining managed KEDA, NAP, workload identity, and Kubernetes Job semantics. AKS Standard is an extension point only when a documented workload requirement needs lower-level cluster control.

### Why the .NET host boundary

Azure SDKs, OpenTelemetry, managed identity, health checks, durable retries, and lifecycle coordination are mature and consistent in .NET. The shell owns Azure integration and process supervision while the existing Squad runtime remains unchanged. This keeps the boundary narrow: .NET does not absorb agent behavior, prompts, routing, or orchestration semantics.

### Why Azure SQL and Service Bus

Azure SQL provides transactions, optimistic concurrency, leases, relational audit queries, and an authoritative effect journal. Service Bus provides efficient delivery, backpressure, PeekLock renewal, duplicate detection, and dead-lettering from one shared work queue that KEDA can scale from. A queue message is a delivery hint; SQL state decides whether a Job may act. This intentionally duplicates a small amount of coordination data to avoid pretending a queue is a workflow database.

### Why control-plane authority remains outside AKS

Execution Jobs are disposable by design. Nodes can disappear, pods can be evicted, Jobs can be retried, and operators can make Kubernetes changes. Durable state, approvals, cancellation intent, effect reconciliation, and evidence therefore live in managed services and a separately hosted control plane. AKS reports execution facts; it does not declare business truth.

## Done means this works end to end

The sample is complete when all of the following are continuously verified:

- A first-time user can clone, authenticate/bootstrap, and submit deployment in three manual steps and under five minutes of human interaction.
- Provisioning continues asynchronously with observable, resumable progress and no requirement to keep a terminal open.
- A pre-provisioned environment becomes ready for a sample run in roughly 10 minutes.
- `squad-aks deploy` creates the documented identity, AKS, managed KEDA, NAP, registry, SQL, Service Bus, Key Vault, control-application, GitHub, and observability resources.
- A GitHub or repository-local CLI request is durably accepted before execution is requested.
- KEDA creates an ephemeral AKS Job from Service Bus demand, and NAP supplies compatible capacity when needed.
- The Job locks one message, acquires and renews an Azure SQL lease, materializes the exact source revision, and runs the existing Squad runtime inside the .NET worker shell.
- The successful remediation scenario reproduces a failure, implements and validates a fix, and opens exactly one draft pull request with durable evidence.
- The contract-block scenario publishes evidence without pushing a code branch or opening a pull request.
- The elevated-risk scenario consumes no worker capacity until a valid digest-bound approval is durable.
- GitHub provides a complete path to request, follow, approve, reject, cancel, and review work without Flight Control.
- Flight Control accurately shows Waiting, Active, Blocked, approval, cancellation, reconciliation, Success, Failure, and Cancelled states.
- A killed, duplicated, stale, or redelivered Job cannot create concurrent authority or repeat a recorded external effect.
- Cancellation is terminal only after acknowledgement when applicable, exact-attempt containment, effect reconciliation, evidence recording, lease closure, and queue settlement.
- A second versioned Squad can be added and selected without cloning platform infrastructure.
- Status, logs, traces, cost attribution, redeploy, rollback, diagnostics, and cleanup behave as documented.
- Automated tests exercise control-plane state transitions, lease takeover, lock renewal/loss, duplicate delivery, effect idempotency, cancellation races, stale approval, GitHub reconciliation, dead-letter handling, and the full deployment smoke path.

## Authoritative platform documentation

- [Azure Kubernetes Service documentation](https://learn.microsoft.com/azure/aks/)
- [AKS Automatic](https://learn.microsoft.com/azure/aks/intro-aks-automatic)
- [AKS Workload Identity](https://learn.microsoft.com/azure/aks/workload-identity-overview)
- [AKS Node Auto-Provisioning](https://learn.microsoft.com/azure/aks/node-auto-provisioning)
- [Managed KEDA on AKS](https://learn.microsoft.com/azure/aks/keda-about)
- [KEDA ScaledJob specification](https://keda.sh/docs/latest/reference/scaledjob-spec/)
- [KEDA Azure Service Bus scaler](https://keda.sh/docs/latest/scalers/azure-service-bus/)
- [Azure Service Bus locks and settlement](https://learn.microsoft.com/azure/service-bus-messaging/message-transfers-locks-settlement)
- [Azure SQL documentation](https://learn.microsoft.com/azure/azure-sql/)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable)
- [Microsoft Entra workload identities](https://learn.microsoft.com/entra/workload-id/workload-identities-overview)
- [GitHub App installation authentication](https://docs.github.com/apps/creating-github-apps/authenticating-with-a-github-app/authenticating-as-a-github-app-installation)
- [GitHub webhook best practices](https://docs.github.com/webhooks/using-webhooks/best-practices-for-using-webhooks)

---

Squad on AKS is a goal-first reference implementation: the interfaces in this README define the product contract. Infrastructure, durable control-plane behavior, worker lifecycle, Flight Control, fixtures, and tests ship together so the documented path is the path the sample continuously proves.
