# Squad on AKS

**Run a real [Squad](https://github.com/bradygaster/squad) inside Azure Kubernetes Service and talk to it from a Blazor web application.**

Squad on AKS is a focused reference sample: one ASP.NET Core application, one configured Squad, one AKS application pod, and one disposable repository workspace. The browser streams a real Squad conversation, shows specialist activity as the coordinator delegates work, and reports the files and validation results produced inside the pod.

The sample uses [`Squad.Agents.AI`](https://github.com/bradygaster/squad/tree/dev/src/Squad.Agents.AI) directly. Squad remains unchanged; this repository hosts it as a Microsoft Agent Framework `AIAgent` and presents it through the Blazor Agentic UI components.

```text
┌─ Squad on AKS ──────────────────────────────────────────────────────────────┐
│ Connected to sample-api                                    AKS · Ready     │
│                                                                            │
│ You                                                                        │
│ Ask the API specialist to add a /health endpoint with a test, then have    │
│ the reviewer check the result.                                             │
│                                                                            │
│ Squad                                                       Working...     │
│ I will inspect the service and delegate implementation and review.         │
│                                                                            │
│ Activity                                                                   │
│ ● coordinator   Planning the work                                          │
│ ✓ api           Added /health and tests                                    │
│ ● reviewer      Reviewing the implementation                               │
│                                                                            │
│ Workspace                                                                  │
│ M src/Api/Program.cs        A tests/Api.Tests/HealthEndpointTests.cs        │
│ ✓ 18 tests passed                                                          │
└────────────────────────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> Wave 1 intentionally demonstrates the smallest useful experience. It does not provide durable conversations, pull request automation, multiple concurrent users, per-session Kubernetes Jobs, fleet management, or a production identity system. The complete requirements and acceptance criteria are tracked in [Wave 1: Run an interactive Squad in AKS with Blazor](https://github.com/bradygaster/squad-on-aks/issues/3).

## What you will build

The finished sample deploys:

- an **ASP.NET Core Blazor Web App** with the chat and activity experience;
- an in-process **AG-UI endpoint** backed by `Squad.Agents.AI`;
- the existing **Squad and Copilot runtime**, unchanged;
- a small sample Squad with coordinator, API specialist, and reviewer roles;
- an **ephemeral workspace** populated with a deterministic sample repository;
- health endpoints, structured logs, and OpenTelemetry traces;
- one container image in Azure Container Registry;
- one AKS application deployment with one replica;
- one public AKS `LoadBalancer` service and Azure Monitor-backed observability.

The result is a vertical slice, not a platform:

```mermaid
flowchart LR
    USER["Browser"] -->|"HTTP via LoadBalancer"| UI["Blazor Agentic UI"]

    subgraph POD["One AKS application pod"]
        UI --> AGUI["AG-UI endpoint"]
        AGUI --> AGENT["Squad.Agents.AI<br/>SquadAgent"]
        AGENT --> COPILOT["Copilot runtime<br/>--agent squad"]
        COPILOT --> SQUAD["Sample Squad<br/>coordinator + specialists"]
        SQUAD --> WORKSPACE[("Ephemeral workspace<br/>emptyDir")]
        AGENT --> TRACE["Subagent activity<br/>OpenTelemetry"]
    end

    TRACE --> MONITOR["Azure Monitor"]
```

All application components run together in the same pod. There is no separate scheduler, queue, workflow database, worker service, or custom Squad protocol.

## Quickstart

### Prerequisites

Install:

- [Git](https://git-scm.com/downloads)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- access to an Azure subscription where you can create a resource group, AKS cluster, container registry, and role assignments
- a Copilot-supported GitHub credential exported as `COPILOT_GITHUB_TOKEN`

The [Copilot SDK authentication documentation](https://github.com/github/copilot-sdk/blob/main/docs/auth/authenticate.md) lists supported credentials. For Wave 1, the credential is a sample deployment secret used by the single application instance. Do not use a classic `ghp_` personal access token; the Copilot SDK does not support it.

You do not need to install .NET, Node.js, `kubectl`, Helm, Squad, or the Copilot CLI for the standard deployment path. The build and container image carry the required runtime, and deployment hooks apply the Kubernetes resources through Azure CLI. Install `kubectl` only if you want to run the optional operations commands later in this guide.

### Timing

- **Human interaction:** three steps and normally under five minutes.
- **New Azure environment:** AKS and supporting resources can take 20-40 minutes to provision.
- **Existing environment:** an application-only redeployment normally completes in 5-10 minutes.

`azd up` streams progress. After Azure accepts the deployment, you can leave it running unattended and return to `azd show`.

### 1. Clone

```bash
git clone https://github.com/bradygaster/squad-on-aks.git
cd squad-on-aks
```

### 2. Authenticate and configure the sample credential

```bash
azd auth login
azd env set COPILOT_GITHUB_TOKEN "$COPILOT_GITHUB_TOKEN"
```

Expected:

```text
Logged in to Azure.
Environment value COPILOT_GITHUB_TOKEN set.
```

`azd` stores the local environment under `.azure/`, which is ignored by Git but still contains sensitive local configuration and must not be shared. The deployment writes the credential to the cluster as a Kubernetes Secret and exposes it only to the application container. Wave 1 does not implement user sign-in or per-user credentials.

### 3. Deploy

```bash
azd up
```

Select a subscription and location when prompted.

Expected:

```text
Provisioning Azure resources
  ✓ Resource group
  ✓ Azure Container Registry
  ✓ Azure Kubernetes Service
  ✓ Log Analytics and Application Insights

Building and publishing squad-on-aks
  ✓ Container image pushed
  ✓ Kubernetes Secret applied
  ✓ LoadBalancer assigned a public IP
  ✓ Deployment available: 1/1
  ✓ Readiness probe passed

SUCCESS: Squad on AKS is ready
Web:    http://<public-ip>/
Health: http://<public-ip>/health/ready
```

The public IP depends on the selected Azure environment. `azd show` prints it again later.

## First conversation

Open the URL printed by `azd up`. The page reports **Ready** only after:

1. the pod-local workspace has been initialized;
2. `.github/agents/squad.agent.md` is present;
3. `SquadAgent` resolves successfully from dependency injection;
4. the Copilot runtime can start with the configured credential.

Use the included acceptance prompt:

> Ask the API specialist to inspect this repository, add a `/health` endpoint with a test, and have the reviewer check the result.

The chat response streams as it is produced. The activity panel separately reports coordinator and specialist events:

```text
00:00  coordinator  Planning the request
00:03  api          Specialist started
00:11  api          Inspecting the sample API
00:37  api          Implementation complete
00:39  reviewer     Specialist started
00:54  reviewer     Review complete
00:55  coordinator  Final response complete
```

The workspace panel then shows concrete evidence:

```text
Changed files
  M src/Api/Program.cs
  A tests/Api.Tests/HealthEndpointTests.cs

Validation
  dotnet test
  Passed: 18 · Failed: 0 · Skipped: 0
```

Send a follow-up in the same browser session:

> Explain the implementation and why the test proves the endpoint is healthy.

The response uses the preceding conversation and current workspace. Conversation state is scoped to the running application instance and is not durable across pod replacement.

## How the application works

The application registers the Squad package as the agent implementation:

```csharp
builder.Services.AddSquadAgent(options =>
{
    options.SquadFolderPath = "/workspace";
    options.OnSubagentTrace = activityFeed.Record;
});
```

The Microsoft Agent Framework/AG-UI hosting integration exposes that agent to the Blazor application. The UI uses `AGUIChatClient` and the Blazor AI components to render streaming messages and typed interaction state.

```mermaid
sequenceDiagram
    actor User
    participant UI as Blazor UI
    participant AGUI as AG-UI endpoint
    participant Agent as SquadAgent
    participant Squad as Squad coordinator
    participant Specialist as Squad specialist
    participant Workspace as /workspace

    User->>UI: Submit prompt
    UI->>AGUI: Start streamed run
    AGUI->>Agent: RunStreamingAsync
    Agent->>Squad: Start Copilot session with --agent squad
    Squad-->>UI: Stream coordinator response
    Squad->>Specialist: Delegate API work
    Specialist->>Workspace: Inspect, edit, and test
    Specialist-->>Agent: Subagent lifecycle events
    Agent-->>UI: Activity updates
    Squad-->>UI: Stream final response
    UI->>Workspace: Read changed-file and validation summary
    UI-->>User: Conversation + evidence
```

### Sample Squad

The image contains a real Squad definition:

```text
/workspace/.github/agents/squad.agent.md
/workspace/.squad/team.md
/workspace/.squad/agents/api/charter.md
/workspace/.squad/agents/reviewer/charter.md
```

The coordinator delegates implementation to the API specialist and validation to the reviewer. The application does not simulate these events: the activity panel consumes `Squad.Agents.AI` subagent trace callbacks, and the same dispatches appear as `squad.subagent` OpenTelemetry spans.

### Ephemeral workspace

An init container copies the deterministic sample repository into an `emptyDir` volume mounted at `/workspace`. Squad can read, edit, build, and test only that disposable copy.

```yaml
volumes:
  - name: workspace
    emptyDir:
      sizeLimit: 2Gi
```

Deleting the pod deletes its uncommitted workspace. The replacement pod receives a clean copy of the fixture. Wave 1 deliberately does not persist branches, commits, conversation state, or generated files.

### Permissions

`Squad.Agents.AI` currently configures the Copilot runtime for permissive tool execution by default. Wave 1 contains that behavior with:

- one trusted demonstration user;
- one deterministic fixture repository;
- one pod-local `emptyDir` workspace;
- no host filesystem mount;
- one application replica;
- Kubernetes CPU, memory, and ephemeral-storage limits;
- no production repository credentials.

Do not point this sample at untrusted repositories or expose it as a multi-user service. Interactive approvals and stricter runtime policy are later hardening work, not hidden Wave 1 capabilities.

## What `azd up` provisions

| Resource | Purpose |
|---|---|
| Azure Kubernetes Service | Hosts the single application pod and its ephemeral workspace |
| Azure Container Registry | Stores the pinned application image |
| Log Analytics and Application Insights | Receives application logs, traces, and `squad.subagent` activities |
| Managed identity and role assignments | Lets AKS pull the image and send telemetry |

The deployment also applies:

- one Kubernetes Deployment with `replicas: 1`;
- one `emptyDir` workspace;
- one Kubernetes Secret containing `COPILOT_GITHUB_TOKEN`;
- liveness and readiness probes;
- resource requests and limits;
- one public `LoadBalancer` service.

No Front Door profile, ingress controller, TLS certificate, SQL database, message broker, Blob account, Redis cache, KEDA scaler, API gateway, or separate control service is created.

## Application states

The UI uses a small, truthful state model:

| State | Meaning |
|---|---|
| **Starting** | The pod or Copilot runtime is initializing. Chat is unavailable. |
| **Ready** | The application can resolve the agent and accept a prompt. |
| **Working** | A conversation turn is active and streaming. |
| **Waiting** | The agent is waiting for the next user message. |
| **Cancelling** | The application has requested cancellation and is waiting for the active run to stop. |
| **Cancelled** | The active turn stopped without being reported as successful. |
| **Failed** | Startup or the active turn failed. The UI shows the actual error category. |

Pod deletion is not shown as a successful or cancelled conversation. The browser reports that the application disconnected and reconnects when the replacement pod becomes ready.

## Operations

### Show the deployed URL and resources

```bash
azd show
```

### Check the application

```bash
kubectl get deployment,pod,service -n squad-on-aks
kubectl wait --for=condition=available deployment/squad-on-aks \
  --namespace squad-on-aks \
  --timeout=180s
```

Expected:

```text
deployment.apps/squad-on-aks   1/1   1   1
pod/squad-on-aks-...           1/1   Running
service/squad-on-aks                  <public address>
```

### Read logs

```bash
kubectl logs deployment/squad-on-aks \
  --namespace squad-on-aks \
  --follow
```

Logs include application startup, workspace initialization, chat-run boundaries, specialist lifecycle events, and validation summaries. Credential values and request authorization headers are not logged.

### Inspect traces

Open the Application Insights resource printed by `azd show`, then query:

```kusto
dependencies
| where name startswith "squad.subagent"
| project timestamp, operation_Id, name, customDimensions
| order by timestamp asc
```

A successful acceptance run includes at least one `squad.subagent` span. A convincing final response without that span does not satisfy the Wave 1 delegation requirement.

### Prove the workspace is disposable

```bash
kubectl exec deployment/squad-on-aks -n squad-on-aks -- \
  git -C /workspace status --short

kubectl delete pod -n squad-on-aks \
  -l app.kubernetes.io/name=squad-on-aks

kubectl rollout status deployment/squad-on-aks \
  -n squad-on-aks

kubectl exec deployment/squad-on-aks -n squad-on-aks -- \
  git -C /workspace status --short
```

The first status shows the files changed during the conversation. The final status is clean because the replacement pod received a new `emptyDir`.

### Redeploy

```bash
azd up
```

The deployment rebuilds the image when source changed and reconciles the existing Azure resources and Kubernetes objects.

### Cleanup

```bash
azd down --purge
```

Review the resource group named by `azd` before confirming deletion.

## Troubleshooting

| Symptom | Check | Resolution |
|---|---|---|
| Readiness remains false | `kubectl logs deployment/squad-on-aks -n squad-on-aks` | Confirm the token is configured and the Squad files were copied into `/workspace`. |
| Authentication failed | `azd env get-values` and pod logs | Supply a Copilot-supported credential as `COPILOT_GITHUB_TOKEN`, then run `azd up` again. |
| The page loads but chat is disabled | Open `/health/ready` | Resolve the named agent or Copilot startup failure; the UI does not enable chat on degraded startup. |
| No specialist activity appears | Inspect Application Insights for `squad.subagent` | Confirm the sample uses the Squad agent definition and the prompt explicitly delegates to a specialist. |
| Changes disappear | Check the pod age | This is expected after pod replacement; Wave 1 uses an ephemeral workspace. |
| Conversation context disappears | Check whether the pod restarted | Conversation persistence is outside Wave 1. Start a new conversation. |
| Pod is `Pending` | `kubectl describe pod -n squad-on-aks -l app.kubernetes.io/name=squad-on-aks` | Check regional quota and node capacity. |
| Image cannot be pulled | `kubectl describe pod ...` | Verify the AKS managed identity has `AcrPull` on the registry. |
| Public endpoint is not ready | `azd show` and `kubectl get service -n squad-on-aks` | Wait for the `LoadBalancer` service to receive an external IP and inspect its events if the address remains pending. |

## Repository layout

```text
.
├── README.md
├── azure.yaml                         # Azure Developer CLI project
├── infra/
│   ├── main.bicep                     # AKS, ACR, identity, and monitoring
│   └── modules/
├── deploy/
│   └── kubernetes/
│       ├── namespace.yaml
│       ├── deployment.yaml
│       └── service.yaml
├── src/
│   └── SquadOnAks.Web/
│       ├── Components/                # Blazor chat, activity, and workspace views
│       ├── Agents/                    # Squad registration and AG-UI mapping
│       ├── Observability/             # Subagent activity and OpenTelemetry
│       ├── Workspace/                 # Initialization and evidence summaries
│       ├── Program.cs
│       └── Dockerfile
├── sample/
│   ├── repository/                    # Deterministic API fixture
│   └── squad/
│       ├── .github/agents/squad.agent.md
│       └── .squad/                    # Team and specialist charters
└── tests/
    ├── SquadOnAks.Web.Tests/
    └── acceptance/
```

## Architecture decisions

### Why AKS

The purpose of the sample is to make a Squad an observable, containerized Kubernetes workload. AKS provides the pod, workspace volume, health probes, resource boundaries, rollout behavior, logs, and deletion semantics the sample intends to teach. A serverless host could run the application with less infrastructure, but it would not demonstrate the AKS hosting model.

### Why `Squad.Agents.AI`

`Squad.Agents.AI` already exposes the existing Squad coordinator as a Microsoft Agent Framework `AIAgent`, including streaming and typed subagent telemetry. Using it keeps Squad unchanged and avoids inventing a second runtime or proprietary bridge.

### Why Blazor Agentic UI

The Blazor AI components provide a .NET-native surface for streaming messages, structured activity, and future interactive agent experiences. The Microsoft Agent Framework AG-UI hosting adapter lets the web application expose the same `SquadAgent` without translating it into a custom protocol.

The Blazor AI components and MAF AG-UI ASP.NET hosting integration are preview dependencies. The repository pins their versions, isolates their use behind small adapter classes, and treats package upgrades as deliberate changes.

### Why one application pod

One pod keeps the first sample understandable. The web application, agent host, runtime, Squad configuration, and workspace share one lifecycle. This is not a high-availability design, but it is the shortest path to proving the experience end to end.

### Why a plain LoadBalancer

Wave 1 exposes the Blazor application directly through an AKS `LoadBalancer` service. This avoids adding Front Door, DNS, certificate management, or an ingress controller to a sample whose purpose is to demonstrate Squad running in AKS.

The endpoint uses HTTP and is intended only for a temporary, single-user demonstration. The Copilot credential remains server-side and is never sent to the browser. HTTPS, custom domains, user authentication, and public multi-user exposure are later hardening work.

### Why an ephemeral workspace

An ephemeral fixture makes permissive agent execution bounded and makes cleanup obvious. It also keeps Wave 1 focused on hosting and interaction rather than repository credentials, persistence, reconciliation, and pull request automation.

## Delivery milestones

Wave 1 is delivered in four gated milestones. A milestone is complete only when every listed exit check passes. Work for a later milestone does not begin early to "prepare the architecture."

| Milestone | Goal | Measurable exit |
|---|---|---|
| **1. Local conversation** | Prove the selected .NET packages can host the sample Squad. | One local Blazor process accepts the acceptance prompt, streams text, dispatches the API specialist, records at least one typed subagent event, and accepts one contextual follow-up. |
| **2. Disposable workload** | Prove useful work and truthful lifecycle behavior inside one container. | The image starts with a clean fixture, reports ready without making a model call, edits the fixture, runs its known test command, shows changed files and the test result, cancels one active turn, and starts clean after container replacement. |
| **3. AKS deployment** | Prove the same image runs in the minimum Azure topology. | `azd up` provisions only the documented resources, deploys one replica, assigns a public IP, passes readiness, streams a real response, and emits a `squad.subagent` span to Application Insights. |
| **4. First-user acceptance** | Prove the documented experience from a fresh checkout. | A first-time user completes the three-step quickstart, runs the full acceptance conversation, verifies the clean replacement workspace, reads logs and traces, and removes the environment with `azd down --purge`. |

If a milestone fails, fix that vertical slice. Do not add queues, databases, controllers, persistence, identity systems, or alternate execution paths as a workaround.

## Units of work

Each implementation unit has one bounded result and one objective completion check.

| Unit | Deliverable | Completion check | Explicitly not part of the unit |
|---|---|---|---|
| **1. Pin the application stack** | Minimal Blazor project with exact .NET, Blazor AI, MAF, AG-UI, Copilot SDK, and `Squad.Agents.AI` versions. | A clean restore and build succeeds; the lock or central package file contains no floating versions. | Aspire orchestration, a component library, theming, authentication, or deployment code. |
| **2. Host the real Squad** | Register one `SquadAgent` against the bundled Squad definition. | A local smoke run proves the `squad` agent definition loaded; missing configuration fails explicitly instead of selecting a generic agent. | ACP bridges, a custom agent protocol, multiple Squads, dynamic team editing, or arbitrary repositories. |
| **3. Stream one conversation** | One chat page backed by the in-process AG-UI endpoint. | The first response renders incrementally and one follow-up uses the active conversation context. | Conversation lists, durable history, reconnect recovery, multiple concurrent conversations, or collaboration features. |
| **4. Show real delegation** | Activity list fed by `OnSubagentTrace` and OpenTelemetry. | The acceptance prompt names the API specialist in the UI and produces a matching `squad.subagent` activity. | A general workflow visualizer, custom dashboards, cost accounting, notifications, or SquadHub-style fleet views. |
| **5. Prepare the fixture workspace** | Deterministic sample API and fixed Squad files copied into `/workspace`. | Startup produces a clean Git worktree; the known health-endpoint task can modify it and `dotnet test` can validate it. | Cloning user repositories, GitHub credentials, branches, commits, pull requests, PVCs, or workspace recovery. |
| **6. Report bounded evidence** | Workspace panel showing `git status --short` and the known test command result. | The two expected changed paths and pass/fail counts appear after the acceptance run. | A generalized evidence store, artifact upload, SARIF pipeline, diff editor, or arbitrary command history. |
| **7. Implement lifecycle and health** | Starting, Ready, Working, Waiting, Cancelling, Cancelled, and Failed states plus health endpoints. | Automated tests cover every transition; readiness performs only local checks and never consumes model credits. | Durable state machines, distributed cancellation, rollback of file edits, approval workflows, or reconciliation. |
| **8. Build the image** | One image containing the web app, runtime, Squad definition, and fixture seed. | The container becomes ready, completes the acceptance run, and a replacement starts with a clean workspace. | Separate web and worker images, additional containers beyond the fixture init container, image variants, or runtime downloads on pod startup. |
| **9. Add minimum observability** | Structured console logs plus standard Azure Monitor OpenTelemetry export. | `kubectl logs` shows run boundaries and Application Insights contains the correlated `squad.subagent` span. | Custom collectors, Grafana, dashboards, alerts, cost models, or a telemetry data platform. |
| **10. Deploy the minimum topology** | `azd`, Bicep, and Kubernetes resources for AKS, ACR, monitoring, one Secret, one Deployment, and one LoadBalancer Service. | A fresh `azd up` reaches `1/1` ready and prints a working public HTTP URL. | Front Door, ingress, DNS, TLS, Key Vault, private networking, autoscaling, Helm, operators, KEDA, or multiple node pools. |
| **11. Prove and document acceptance** | Automated local checks and one opt-in live AKS smoke path matching this README. | The required end-to-end demonstration passes and `azd down --purge` removes the environment. | CI deployment on every change, load tests, chaos testing, performance tuning, or production certification. |

## Scope-control rules

These rules apply during planning, implementation, review, and Squad delegation:

1. **Every task names its acceptance criterion.** If work cannot point to an unchecked Wave 1 criterion or unit above, it is not Wave 1 work.
2. **One path only.** Implement the documented Blazor + AG-UI + `Squad.Agents.AI` path. Do not add fallback agents, alternate protocols, alternate deployment targets, or a second UI.
3. **No speculative abstractions.** Extract a reusable abstraction only when two Wave 1 call sites already need it. Future extensibility is not an acceptance criterion.
4. **No production hardening by implication.** Record a limitation instead of adding identity, approvals, persistence, private networking, TLS, scaling, reconciliation, or secret-management systems.
5. **The fixture is the repository boundary.** Wave 1 never accepts a repository URL, GitHub installation, branch, or pull request destination.
6. **One active conversation.** A second browser may view the page, but the application accepts only one active conversation and one active turn. It returns a clear busy response rather than creating a scheduler.
7. **Cancellation is local.** Stop cancels the current in-process run and reports the result. It does not undo file edits or promise distributed containment.
8. **Readiness is free.** Health probes check configuration, files, dependency resolution, and process readiness; they do not call a model or run the acceptance prompt.
9. **Telemetry stays standard.** Use built-in logging and OpenTelemetry exporters. Do not build dashboards or a second event store.
10. **Infrastructure has a fixed budget.** The baseline may contain only the Azure and Kubernetes resources named in Unit 10. Adding a resource requires changing the Wave 1 issue first and naming the existing criterion that cannot otherwise pass.
11. **Tests prove behavior, not prose.** Delegation requires a subagent event/span; useful work requires changed files and a passing command; ephemerality requires a clean replacement workspace.
12. **Stop when the criterion passes.** Refactoring, polishing, or extending an already-passing unit requires a demonstrated defect in another Wave 1 criterion.

During Squad execution, the coordinator must give each specialist one unit from the table, its completion check, and its explicit exclusions. Specialists must not create follow-on work or implement adjacent improvements without a failing Wave 1 criterion.

### Known scope-creep traps

| Tempting enhancement | What it would pull in | Wave 1 mitigation |
|---|---|---|
| "Secure the public app properly" | Login, OAuth, Front Door, TLS, DNS, Key Vault, authorization, and user isolation | Keep a temporary single-user HTTP endpoint, never send the server credential to the browser, document the limitation, and destroy the environment after use. |
| "Let users choose a repository" | GitHub identity, installation tokens, clone policy, branch management, persistence, and PR workflows | Ship exactly one fixture in the image. There is no repository picker or repository URL input. |
| "Make permissive tools production-safe" | Permission UX, policy engines, audit records, and reconciliation | Bound execution to the disposable fixture and explicitly prohibit untrusted or multi-user use. Approval work is a later wave. |
| "Resume after a restart" | Database-backed conversations, workspace persistence, session ownership, migrations, and recovery | A pod restart starts clean. The UI says the conversation was lost. |
| "Support a few simultaneous users" | Session routing, concurrency controls, quotas, scheduling, and isolation | Accept one active conversation and return `409 Conflict` with a visible busy message for another. |
| "Run each chat in its own Job" | A control plane, Job creation API, bidirectional routing, lifecycle reconciliation, and cleanup controllers | Keep the conversation and Squad process in the one application pod for Wave 1. |
| "Improve the operator experience" | Session lists, dashboards, notifications, filtering, history, and fleet management | Provide one page with chat, activity, workspace summary, health, and Stop—nothing else. |
| "Preserve better evidence" | Blob Storage, schemas, retention, checksums, immutable manifests, and authorization | Show only current `git status --short` and the known test result. Evidence disappears with the pod. |
| "Make telemetry production-grade" | Collectors, dashboards, alerts, sampling policy, cost attribution, and long-term storage | Export standard logs and traces to Application Insights and document one query. |
| "Make deployment flexible" | Helm, environment matrices, alternate clouds, existing-cluster modes, autoscaling, and multiple node pools | Support one `azd up` path, one region choice, one node pool, one replica, and one Kubernetes manifest set. |
| "Improve secret management" | Key Vault, CSI drivers, workload identity, rotation, and per-user tokens | Use one Kubernetes Secret populated by `azd`; document that it is sample-grade and short-lived. |
| "Future-proof the integration" | Custom protocols, adapters, plugin systems, interfaces, and alternate agent runtimes | Call `Squad.Agents.AI` directly. Add an abstraction only after two current Wave 1 call sites require it. |
| "Fully automate acceptance" | Persistent test subscriptions, CI credentials, recurring cloud cost, and flaky live-model gates | Keep deterministic local tests in normal CI and one explicit, opt-in live AKS smoke run. |

## Wave 1 boundaries

Wave 1 does not include:

- GitHub App or end-user identity architecture;
- pull request creation;
- durable conversations or durable workspaces;
- multiple concurrent users or horizontally scaled application replicas;
- per-session Kubernetes Jobs;
- multiple-Squad scheduling, fairness, quotas, or workload classes;
- SQL, Service Bus, Blob Storage, Redis, KEDA, APIM, or a durable control plane;
- remote ACP control, device management, approval inboxes, Teams, or push notifications;
- Front Door, ingress controllers, custom DNS, TLS certificates, or HTTPS termination;
- production-grade secret rotation, private networking, or enterprise hardening.

These are potential later waves only when the working sample demonstrates a specific need.

## Done means this works end to end

Wave 1 is complete when all of the following are continuously demonstrated:

- A first-time user can clone the repository, authenticate/configure, and submit deployment in three manual steps and under five minutes of human interaction.
- Azure provisioning can continue asynchronously and reports observable progress.
- The deployed application becomes available at one public HTTP URL.
- Readiness remains false until the workspace, Squad definition, `SquadAgent`, and Copilot runtime are usable.
- Chat uses a real `Squad.Agents.AI` agent and `.github/agents/squad.agent.md`, not a simulated or generic fallback.
- The first response streams into the Blazor UI.
- A follow-up message retains the active browser session's conversation context.
- The acceptance prompt dispatches at least one real specialist.
- The UI displays named specialist activity and Application Insights contains the matching `squad.subagent` span.
- Squad edits the sample repository and produces a changed-file list plus passing validation evidence.
- Missing credentials, agent startup failures, and validation failures remain visible failures.
- Deleting the pod removes the modified workspace.
- The replacement pod starts from a clean fixture and becomes ready.
- Logs, traces, redeployment, troubleshooting, and cleanup work as documented.

The concise finish line is:

> **One pod, one Squad, one web conversation, real delegation, real workspace work, observable and disposable on AKS.**

## References

- [Wave 1 requirements and acceptance criteria](https://github.com/bradygaster/squad-on-aks/issues/3)
- [Squad](https://github.com/bradygaster/squad)
- [`Squad.Agents.AI`](https://github.com/bradygaster/squad/tree/dev/src/Squad.Agents.AI)
- [Build agentic user interfaces with Blazor](https://devblogs.microsoft.com/dotnet/build-agentic-ui-blazor/)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [AG-UI protocol](https://docs.ag-ui.com/)
- [GitHub Copilot SDK authentication](https://github.com/github/copilot-sdk/blob/main/docs/auth/authenticate.md)
- [Azure Kubernetes Service](https://learn.microsoft.com/azure/aks/)
- [Kubernetes services in AKS](https://learn.microsoft.com/azure/aks/concepts-network-services#services)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable)
