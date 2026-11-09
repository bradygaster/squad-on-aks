# AKS deployment and operations

This runbook deploys the Squad on AKS platform. Generic production hosting
for generated apps remains separate work in
[#51](https://github.com/bradygaster/squad-on-aks/issues/51).
The recorded topology is a deployment proof, not a production ingress or
authentication design. Run commands from the repository root.

For an existing local cluster, use [Local Kubernetes](local-kubernetes.md).

## Current architecture

```mermaid
flowchart LR
    DEV["Developer"] -->|"aspire deploy"| APPHOST["Aspire AppHost"]
    APPHOST --> AZURE["Azure resource group"]

    subgraph AZURE
        ACR["Basic Azure Container Registry"]

        subgraph AKS["Free-tier AKS"]
            NODE["1 x Standard_D2s_v5 system node"]
            APP["Blazor + Squad AG-UI<br/>1 replica"]
            DASH["Aspire dashboard<br/>internal only"]
            NODE --> APP
            NODE --> DASH
        end

        ACR -->|"managed-identity image pull"| APP
    end

    USER["User"] -->|"HTTP :80"| LB["Public LoadBalancer"]
    LB --> APP
```

## Deploy from a fresh clone

The commands below describe the platform's recorded Azure deployment path.
The first deployment creates billable Azure resources.

### Prerequisites

Install and configure:

- [Git](https://git-scm.com/);
- access to this GitHub repository and Git credentials that can clone
  it;
- [.NET SDK 10.0.301](https://dotnet.microsoft.com/download), pinned by
  [`global.json`](../global.json);
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli);
- Docker Desktop or another Docker-compatible container runtime;
- [`kubectl`](https://kubernetes.io/docs/tasks/tools/);
- [Helm](https://helm.sh/docs/intro/install/) 4.2.0 or later (validated with
  Helm 4.3.1); and
- an Azure subscription where you can create a resource group, AKS cluster,
  ACR, managed identities, and role assignments.

The repository pins Aspire CLI 13.4.3 as a local .NET tool. You do not need a
separate global Aspire installation.

Before deploying, confirm that the target region has quota for one
`Standard_D2s_v5` VM and supports AKS.

### 1. Clone and restore the pinned tools

```bash
git clone https://github.com/bradygaster/squad-on-aks.git
cd squad-on-aks

dotnet --version
dotnet tool restore
dotnet aspire ls
```

Expected:

```text
10.0.301
1 AppHost found
src/SquadOnAks.AppHost/SquadOnAks.AppHost.csproj
```

If `dotnet --version` fails because 10.0.301 is unavailable, install that SDK;
the repository disables SDK roll-forward intentionally.

### 2. Authenticate and set the deployment inputs

Choose the Azure subscription, region, and resource-group name before running
the deployment:

```bash
az login
az account set --subscription "<subscription-name-or-id>"

export Azure__SubscriptionId="$(az account show --query id -o tsv)"
export Azure__Location="westus2"
export Azure__ResourceGroup="rg-squad-on-aks"

# Supply a Copilot-supported token to the deployed Squad host.
export Parameters__copilot_token="<copilot-supported-token>"
```

The AppHost parameter is named `copilot-token`. Aspire maps the dash to an
underscore in the environment variable, so the correct variable is
`Parameters__copilot_token`.

On macOS, Docker Desktop commonly configures a desktop credential store. The
validated Aspire deployment used an isolated Docker configuration so registry
login could complete without waiting on that credential helper:

```bash
export DOCKER_CONFIG="${TMPDIR:-/tmp}/squad-on-aks-docker"
mkdir -p "$DOCKER_CONFIG"
printf '{"auths":{}}\n' > "$DOCKER_CONFIG/config.json"
```

This file is temporary and outside the repository. Aspire logs in to the ACR
it provisions and writes only that temporary Docker configuration.

Verify the non-secret Azure context:

```bash
az account show \
  --query "{subscription:name, id:id, tenant:tenantId}" \
  --output table

printf 'region=%s\nresource-group=%s\n' \
  "$Azure__Location" \
  "$Azure__ResourceGroup"
```

Do not print a real Copilot credential if you replace the placeholder in a
future application version.

### 3. Deploy

```bash
./scripts/aks-preflight.sh && dotnet aspire deploy --non-interactive
```

For the default AKS target, the preflight verifies the repository-pinned .NET
SDK and Aspire CLI versions, Azure login and selected subscription, the
required deployment inputs, the container runtime, kubectl, Helm, and presence
of the Copilot credential. It never prints the credential. Resolve any
reported failure before deploying.
Aspire then performs the complete deployment and prints each active pipeline
stage:

1. validates Azure, Helm, Docker, and Kubernetes prerequisites;
2. creates or reuses the resource group;
3. provisions Basic ACR;
4. provisions Free-tier AKS with one fixed `Standard_D2s_v5` system node;
5. configures managed identity and ACR pull access;
6. builds the application image;
7. logs in to ACR and pushes the image;
8. obtains temporary AKS credentials;
9. generates the Helm chart;
10. installs the `production` release into the `default` namespace; and
11. prints the public application URL.

A successful run ends with:

```text
✓ 34/34 steps succeeded

☁️ Target: Azure
📦 Resource Group: <your-resource-group>
☸ AKS Cluster: <generated-cluster-name>
📊 Dashboard: kubectl port-forward ...
📋 Release status: helm status production -n default
app: http://<public-ip>
```

The validated first deployment required several minutes of unattended Azure
provisioning. Regional capacity and subscription policy can make AKS
provisioning take longer. Aspire continuously prints the active pipeline
steps; no separate Bicep, Helm, or `az deployment` command is required.

## Connect to the deployed cluster

Aspire uses a temporary kubeconfig while deploying. Create a kubeconfig you
can reuse for inspection and, importantly, for `aspire destroy`:

```bash
export AKS_NAME="$(
  az aks list \
    --resource-group "$Azure__ResourceGroup" \
    --query '[0].name' \
    --output tsv
)"

export KUBECONFIG="${TMPDIR:-/tmp}/squad-on-aks-kubeconfig"

az aks get-credentials \
  --resource-group "$Azure__ResourceGroup" \
  --name "$AKS_NAME" \
  --file "$KUBECONFIG" \
  --overwrite-existing
```

Confirm the topology:

```bash
kubectl get nodes \
  -o custom-columns='NAME:.metadata.name,POOL:.metadata.labels.agentpool,CPU:.status.capacity.cpu'

kubectl get deployments,pods,services --namespace default

helm status production --namespace default
```

Expected characteristics:

```text
POOL
system

app-deployment             1/1
aks-dashboard-deployment   1/1
app-service                LoadBalancer
aks-dashboard-service      ClusterIP
```

## Open and verify the application

Run the smoke check after the rollout. It discovers the public address from
the generated `app-service` and verifies health, the initial **Needs cast**
state, one ready replica for each deployment, exactly one public Service, the
internal dashboard Service, and the absence of forbidden ingress, gateway,
certificate, autoscaling, and persistent-volume resources.

```bash
./scripts/aks-smoke.sh
```

The smoke check prints the discovered application URL. The AG-UI endpoint is
a streaming `POST` API; see the scripted client checks in
[Testing and acceptance](testing.md).

If the public IP times out while Kubernetes reports a ready endpoint, verify
the application through a port-forward:

```bash
kubectl port-forward service/app-service 18080:80
```

In another terminal using the same repository and environment:

```bash
curl --fail http://127.0.0.1:18080/
curl --fail http://127.0.0.1:18080/health/live
curl --fail http://127.0.0.1:18080/health/ready
```

The public Service is plain HTTP with no authentication or TLS. It is a
deployment proof, not a production ingress design.

## Open the internal Aspire dashboard

The dashboard intentionally has no public IP:

```bash
kubectl port-forward \
  --namespace default \
  service/aks-dashboard-service \
  18888:18888
```

Open <http://127.0.0.1:18888>. If the dashboard asks for a token, retrieve the
startup output:

```bash
kubectl logs \
  --namespace default \
  --selector app.kubernetes.io/component=aks-dashboard \
  --tail 50
```

Do not publish the dashboard Service or its login token.

## Diagnose the application

For a local AppHost, use Aspire for resource state, console output, structured
logs, and traces:

```bash
dotnet aspire ps
dotnet aspire describe app
dotnet aspire logs app
dotnet aspire otel logs app
dotnet aspire otel traces app
```

Aspire's local backchannel is scoped to the running AppHost; it does not
inspect a deployed AKS cluster. In AKS, inspect application state and
structured container output with Kubernetes, and use the internal dashboard
above for traces:

```bash
kubectl get deployments,pods,services --namespace default
kubectl logs \
  --namespace default \
  deployment/app-deployment \
  --all-containers=true \
  --tail=100
```

To query the dashboard telemetry API with Aspire CLI instead of its UI, keep
the dashboard port-forward running and set `DASHBOARD_URL` to its dashboard
base or login URL without printing the login token:

```bash
dotnet aspire otel logs --dashboard-url "$DASHBOARD_URL"
dotnet aspire otel traces --dashboard-url "$DASHBOARD_URL"
```

## What the AppHost provisions

| Resource | Current configuration |
|---|---|
| Resource group | Name supplied through `Azure__ResourceGroup` |
| AKS control plane | Free tier |
| AKS node pool | One `Standard_D2s_v5` system node, fixed count 1, autoscaling disabled |
| ACR | Basic SKU |
| Application | One replica scheduled to `agentpool: system` |
| Application Service | Public `LoadBalancer`, TCP 80 mapped to container port 8080 |
| Dashboard Service | Internal `ClusterIP` |
| Workspace | `emptyDir`, size limit `2Gi` |
| Init container | The same pinned application image, running the deterministic workspace initializer |
| Runtime boundary | UID/GID `1654`, read-only root filesystem, no privilege escalation, all Linux capabilities dropped, runtime-default seccomp |
| Application requests | `50m` CPU, `512Mi` memory, `1Gi` ephemeral storage |
| Application limits | `500m` CPU, `1Gi` memory, `2Gi` ephemeral storage |
| Init requests/limits | `25m`/`250m` CPU, `128Mi`/`256Mi` memory, `256Mi`/`512Mi` ephemeral storage |
| Secret | Opaque Kubernetes Secret containing `copilot-token` |

The deployment does **not** create ingress, TLS, DNS, Application Gateway for
Containers, a VNet customization, SQL, Service Bus, persistent volumes, KEDA,
Application Insights, or an additional node pool.

## Preview the generated deployment

Generate the Aspire-owned Bicep and Helm artifacts without creating Azure
resources:

```bash
export Parameters__copilot_token="<copilot-supported-token>"
dotnet aspire publish --output-path ./artifacts/aspire --non-interactive
```

The output includes:

```text
artifacts/aspire/
├── main.bicep
├── aks/
│   └── aks.bicep
├── aks-acr/
│   └── aks-acr.bicep
├── Chart.yaml
├── values.yaml
└── templates/
```

Published artifacts are for review or handoff. `aspire deploy` resolves and
applies the AppHost model directly; it does not deploy from that output
directory.

## Redeploy

Keep the Azure variables and isolated `DOCKER_CONFIG` set, then run:

```bash
dotnet aspire deploy --non-interactive
```

The validated success-to-success redeployment reused the Azure resources,
pushed a new image tag, upgraded the Helm release, and completed all 34 steps
in about 19 seconds.

## Clean up

Make sure `KUBECONFIG` still points to credentials for the Aspire-owned AKS
cluster:

```bash
kubectl cluster-info
helm status production --namespace default
```

Then run the Aspire destroy pipeline:

```bash
dotnet aspire destroy --non-interactive --yes
```

Expected:

```text
✓ Helm release production uninstalled
✓ Resource group deletion in progress
✓ Deployment state cleared
✓ 11/11 steps succeeded
```

Azure resource-group deletion continues asynchronously. Wait for completion:

```bash
az group wait \
  --name "$Azure__ResourceGroup" \
  --deleted \
  --interval 15 \
  --timeout 1200

az group exists --name "$Azure__ResourceGroup"
```

The final command should print `false`.
Treat the destroy pipeline's explicit Helm-release-uninstalled result and the
absent resource group as the cleanup pass criteria; do not substitute
out-of-band resource deletion for Aspire teardown.

> [!CAUTION]
> `aspire destroy --yes` deletes the configured Aspire-owned resource group.
> Confirm `Azure__SubscriptionId`, `Azure__ResourceGroup`, and your active Azure
> account before running it.

## Troubleshooting

| Symptom | Cause or next check |
|---|---|
| `aspire ls` finds no AppHost | Run the command from the repository root and run `dotnet tool restore`. |
| SDK resolution fails | Install exactly .NET SDK 10.0.301; `global.json` disables roll-forward. |
| AKS provisioning fails | Check regional VM-family quota, total regional vCPU quota, subscription policy, and `Standard_D2s_v5` availability. |
| ACR login hangs on macOS | Use the isolated `DOCKER_CONFIG` shown above instead of the Docker Desktop credential store. |
| ACR hostname briefly returns `NXDOMAIN` after recreation | Wait for DNS propagation and retry `aspire deploy`; a local negative DNS cache may also need to expire. |
| The application pod is `Pending` | Run `kubectl describe pod`; the one-node design has limited allocatable CPU after AKS system pods and the dashboard. |
| The public IP times out | Confirm the pod, EndpointSlice, Service, Azure load-balancer rule, and NSG rule; use port-forward to separate application health from local outbound filtering. |
| `aspire destroy` cannot uninstall Helm | Refresh AKS credentials, export the resulting `KUBECONFIG`, confirm `helm status production -n default`, and rerun the destroy command. |
| A resource group was deleted outside Aspire and deploy reuses stale outputs | Prefer `aspire destroy`. Out-of-band deletion can leave local state under `~/.aspire/deployments`; inspect the Aspire log before removing the state file for this AppHost/environment. |

## References

Aspire behavior in this repository is based on:

- [Aspire AppHost](https://aspire.dev/get-started/app-host/)
- [Deploy Aspire apps to AKS](https://aspire.dev/deployment/kubernetes/aks/)
- [Aspire Kubernetes integration](https://aspire.dev/integrations/compute/kubernetes/)
- [`aspire deploy`](https://aspire.dev/reference/cli/commands/aspire-deploy/)
- [Official `microsoft/aspire` repository](https://github.com/microsoft/aspire)

Project evidence:

- [Pinned stack compatibility](stack-compatibility.md)
- [Native casting probe](native-casting-probe.md)
- [Aspire-to-AKS deployment evidence](evidence/w03/README.md)
- [Deterministic Playwright layers](../test/SquadOnAks.AcceptanceTests/README.md)
