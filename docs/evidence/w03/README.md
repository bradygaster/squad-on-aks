# W03 Aspire-to-AKS deployment evidence

Issue: #6  
Parent criterion: #3 requires one Aspire-owned AKS cluster with one fixed,
non-autoscaling system node and no additional node pools.

## Result

The pinned Aspire 13.4.3 deployment path satisfies the W03 acceptance
contract. The AppHost is the source of truth for Azure infrastructure, image
build and push, Kubernetes resources, Helm deployment, and teardown.

The live validation in the BradyG Private Test subscription proved:

- one Free-tier AKS cluster in `westus2`;
- one fixed `Standard_D2s_v5` system node with autoscaling disabled;
- no user or workload node pool;
- one Basic ACR;
- one application replica scheduled to `agentpool: system`;
- a public application `LoadBalancer` Service;
- an internal-only Aspire dashboard `ClusterIP` Service;
- an Opaque Secret for the Copilot parameter;
- an `emptyDir` workspace populated by an init container;
- readiness and liveness probes;
- explicit CPU, memory, and ephemeral-storage requests and limits;
- a successful success-to-success Aspire redeploy; and
- a successful Aspire-owned Helm and Azure destroy.

The checked-in `generated/` directory is a reproducible evidence snapshot. It
is not a second deployment source.

## Resolving the generated node-pool defaults

`WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 1)` alone does
not produce the required topology in Aspire 13.4.3. The pinned integration
enables autoscaling on that pool, automatically adds a `workload` user pool,
and schedules the application to that user pool.

The supported solution uses two official Aspire customization surfaces:

1. `ConfigureInfrastructure` replaces the generated AKS agent-pool collection
   with one fixed system pool before Bicep is emitted.
2. `WithNodePool` assigns the application to a system-pool resource so the
   Kubernetes Deployment selects `agentpool: system`.

The resulting Bicep contains exactly:

```bicep
agentPoolProfiles: [
  {
    name: 'system'
    count: 1
    vmSize: 'Standard_D2s_v5'
    osType: 'Linux'
    enableAutoScaling: false
    mode: 'System'
  }
]
```

Authoritative references:

- [Azure Kubernetes Service integration](https://aspire.dev/integrations/cloud/azure/aks/)
- [`AddAzureKubernetesEnvironment`](https://aspire.dev/reference/api/csharp/aspire.hosting.azure.kubernetes/azurekubernetesenvironmentextensions/methods/#addazurekubernetesenvironment)
- [`WithSystemNodePool`](https://aspire.dev/reference/api/csharp/aspire.hosting.azure.kubernetes/azurekubernetesenvironmentextensions/methods/#withsystemnodepool)
- [`PublishAsKubernetesService`](https://aspire.dev/reference/api/csharp/aspire.hosting.kubernetes/kubernetesserviceextensions/methods/#publishaskubernetesservice)
- [`ConfigureInfrastructure`](https://aspire.dev/reference/api/csharp/aspire.hosting.azure/azureprovisioningresourcebuilderextensions/configureinfrastructure/)
- [Pinned infrastructure generation source](https://github.com/microsoft/aspire/blob/f4c27f2d43ddc1cacd0dd083b30d1fea1cee7a62/src/Aspire.Hosting.Azure.Kubernetes/AzureKubernetesEnvironmentExtensions.cs#L551-L574)
- [Pinned AKS preparation source](https://github.com/microsoft/aspire/blob/f4c27f2d43ddc1cacd0dd083b30d1fea1cee7a62/src/Aspire.Hosting.Azure.Kubernetes/AzureKubernetesEnvironmentResource.AksPipeline.cs#L151-L178)

## Workload model

The application Helm template proves:

- `replicas: 1`;
- `nodeSelector.agentpool: system`;
- `/health/live` and `/health/ready` HTTP probes;
- an `emptyDir` workspace limited to `2Gi`;
- an MCR-hosted Azure Linux init image;
- a fixture initialized at `/workspace/fixture/README.md`;
- application requests of `50m` CPU, `512Mi` memory, and `1Gi` ephemeral
  storage; and
- application limits of `500m` CPU, `1Gi` memory, and `2Gi` ephemeral
  storage.

The small application CPU request is intentional for this one-node proof. The
AKS system workload and Aspire dashboard reserved most of the node's 1900
millicores; the original `250m` request could not schedule on the required
single node.

The snapshot contains no AGC, Gateway API, cert-manager, VNet/subnet, Front
Door, ingress, Application Insights, SQL, queues, persistent volumes, or
persistent volume claims.

## Generated artifact inventory

| Artifact | Evidence |
|---|---|
| `main.bicep` | One resource group with ACR and AKS modules only |
| `aks-acr/aks-acr.bicep` | One Basic ACR |
| `aks/aks.bicep` | Free AKS, one fixed system pool, managed identities, and ACR pull assignment |
| `Chart.yaml` | One Aspire-generated Helm chart |
| `templates/app/deployment.yaml` | One system-pool application replica, init container, workspace, probes, and budgets |
| `templates/app/service.yaml` | Public application `LoadBalancer` |
| `templates/app/secrets.yaml` | Opaque Secret with the Copilot parameter placeholder |
| `templates/aks-dashboard/deployment.yaml` | One dashboard replica |
| `templates/aks-dashboard/service.yaml` | Internal dashboard `ClusterIP` only |

## Live deployment and redeployment

The disposable environment used:

- subscription: BradyG Private Test Sub;
- region: `westus2`;
- resource group: `rg-squad-on-aks-w03`;
- AKS: `aks-z3yj4ce4ov27y`; and
- ACR: `aksacrz3yj4ce4ov27y.azurecr.io`.

`aspire deploy --non-interactive` completed all 34 steps. Kubernetes and Azure
inspection confirmed the exact one-node topology, one application pod and one
dashboard pod on that node, a ready application endpoint, the public load
balancer rule, and the Internet-to-8080 NSG rule.

The deployed probe returned:

```text
GET /              -> Squad on AKS deployment probe
GET /fixture       -> # Remediation fixture
GET /health/ready  -> 200
```

Those responses were verified through Kubernetes port forwarding. The local
workstation could not connect directly to the public IP on port 8080 even
though Kubernetes and Azure both reported a ready backend and the expected
load-balancer and NSG rules. This is recorded as a local-network verification
limitation, not as evidence that the endpoint was reachable from this machine.

A second Aspire deploy against the healthy environment completed all 34 steps
in 19 seconds and reused the Azure deployments, proving success-to-success
redeployment.

## Deployment environment lessons

- The normal macOS Docker configuration uses the Docker Desktop credential
  store. Aspire's registry login stalled with that configuration, so the live
  proof used an isolated `DOCKER_CONFIG`.
- The subscription policy rejected a Docker Hub BusyBox init image. The final
  model uses `mcr.microsoft.com/azurelinux/base/core:3.0`.
- Recreating the deterministic ACR name briefly exposed a negative local DNS
  cache entry. Once the resolver refreshed, the same Aspire deployment
  completed successfully.

These are deployment-environment constraints; they do not introduce a second
infrastructure or application deployment path.

## Teardown

The first teardown attempt omitted the explicit kubeconfig used for cluster
verification. Aspire started Azure resource-group deletion, but the parallel
Helm uninstall exited with code 1. The release was removed through direct Helm
recovery while the cluster remained reachable, and Azure deletion completed
with zero leaked resources.

The acceptance path was then repeated from a fresh disposable deployment with
`KUBECONFIG` explicitly supplied to Aspire:

```text
destroy-helm-aks          -> Helm release production uninstalled
destroy-azure-azuref1451  -> resource group deletion started
destroy                   -> deployment state cleared
11/11 steps succeeded
```

Final verification:

- `rg-squad-on-aks-w03` exists: `false`;
- remaining resources in that group: `0`; and
- the separate capacity-test group `rg-squad-on-aks-test` exists: `true`.

## Validation

- locked solution restore: passed;
- Release solution build: passed with zero warnings and errors;
- `dotnet format --verify-no-changes`: passed;
- `aspire publish --output-path docs/evidence/w03/generated --non-interactive`:
  13/13 steps passed;
- generated-output reproducibility comparison: passed;
- `az bicep build`: passed;
- `helm lint`: passed;
- live deploy: 34/34 steps passed;
- success-to-success redeploy: 34/34 steps passed;
- live fixture, readiness, topology, Service, Secret, ACR, and resource-budget
  checks: passed; and
- Aspire destroy with explicit kubeconfig: 11/11 steps passed, followed by
  confirmed Azure resource-group deletion.
