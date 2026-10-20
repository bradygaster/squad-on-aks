# W03 Aspire-to-AKS artifact probe

Issue: #6  
Parent criterion: #3 requires one Aspire-owned AKS cluster with one fixed, non-autoscaling system node and no additional node pools.

## Result

The official Aspire 13.4.3 APIs can generate the required public application `LoadBalancer` Service, one application replica, an internal-only dashboard Service, a Secret-backed Copilot parameter, Free-tier AKS, Basic ACR, managed identities, image build/push steps, Helm deployment, and Azure/Helm destroy steps.

The generated AKS infrastructure does **not** match the fixed node-pool budget:

- `WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 1)` emits `enableAutoScaling: true`.
- Aspire automatically adds a second `workload` user pool with `minCount: 1`, `maxCount: 3`, and `enableAutoScaling: true`.
- The generated application Deployment is pinned to that extra pool with `nodeSelector.agentpool: workload`.

This triggers the W03 stop condition. No cloud deployment was attempted because it would provision a topology explicitly excluded by #6 and #3.

## Probe model

The AppHost uses only official APIs:

```csharp
builder.AddAzureKubernetesEnvironment("aks")
    .WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 1);

builder.AddProject<Projects.SquadOnAks_DeploymentProbe>("app")
    .WithEnvironment("COPILOT_TOKEN", copilotToken)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .PublishAsKubernetesService(resource =>
    {
        resource.Service.Spec.Type = "LoadBalancer";

        if (resource.Workload is Deployment deployment)
        {
            deployment.Spec.Replicas = 1;
        }
    });
```

Authoritative references:

- [Azure Kubernetes Service integration](https://aspire.dev/integrations/cloud/azure/aks/)
- [`AddAzureKubernetesEnvironment`](https://aspire.dev/reference/api/csharp/aspire.hosting.azure.kubernetes/azurekubernetesenvironmentextensions/methods/#addazurekubernetesenvironment)
- [`WithSystemNodePool`](https://aspire.dev/reference/api/csharp/aspire.hosting.azure.kubernetes/azurekubernetesenvironmentextensions/methods/#withsystemnodepool)
- [`PublishAsKubernetesService`](https://aspire.dev/reference/api/csharp/aspire.hosting.kubernetes/kubernetesserviceextensions/methods/#publishaskubernetesservice)
- [Pinned infrastructure generation source](https://github.com/microsoft/aspire/blob/f4c27f2d43ddc1cacd0dd083b30d1fea1cee7a62/src/Aspire.Hosting.Azure.Kubernetes/AzureKubernetesEnvironmentExtensions.cs#L551-L574), which hard-codes autoscaling for every node pool.
- [Pinned AKS preparation source](https://github.com/microsoft/aspire/blob/f4c27f2d43ddc1cacd0dd083b30d1fea1cee7a62/src/Aspire.Hosting.Azure.Kubernetes/AzureKubernetesEnvironmentResource.AksPipeline.cs#L151-L178), which creates the `workload` user pool whenever no user pool exists.

## Generated artifact inventory

The checked-in `generated/` directory is an evidence snapshot, not a parallel deployment source of truth.

| Artifact | Observed resources |
|---|---|
| `main.bicep` | One resource group; modules for ACR and AKS only |
| `aks-acr/aks-acr.bicep` | One Basic ACR |
| `aks/aks.bicep` | Free-tier AKS, system-assigned control-plane identity, kubelet identity output, ACR pull role assignment, two node pools |
| `Chart.yaml` | One Aspire Helm chart |
| `templates/app/deployment.yaml` | One application Deployment, one replica, scheduled to `workload` pool |
| `templates/app/service.yaml` | One application Service with `type: LoadBalancer` |
| `templates/app/secrets.yaml` | One Opaque Secret containing the Copilot parameter placeholder |
| `templates/app/config.yaml` | Application and OpenTelemetry configuration |
| `templates/aks-dashboard/deployment.yaml` | One internal telemetry Deployment, one replica |
| `templates/aks-dashboard/service.yaml` | Dashboard `ClusterIP` Service only |
| `values.yaml` | Image, secret placeholder, and non-secret configuration values |

The snapshot contains no AGC, Gateway API, cert-manager, VNet/subnet, Front Door, ingress, Application Insights, SQL, queues, persistent volumes, or persistent volume claims.

## Pipeline ownership

`aspire deploy --list-steps` includes:

- Azure login and provisioning context
- ACR provisioning
- AKS provisioning
- application image build
- ACR login and image push
- Helm preparation and deployment
- application and dashboard summaries

`aspire destroy --list-steps --yes` includes both:

- `destroy-helm-aks`
- `destroy-azure-azuref1451`

The destroy pipeline therefore owns the generated Helm release and Azure resource group path without relying on ambient Kubernetes context.

## Validation

- `dotnet build SquadOnAks.slnx --no-restore --nologo`: passed with zero warnings and errors.
- `aspire publish --output-path docs/evidence/w03/generated --non-interactive`: 13/13 steps passed.
- `az bicep build --file docs/evidence/w03/generated/main.bicep --stdout`: passed; generated `main.bicep` has one unused `principalId` parameter warning.
- `helm lint`: not run because Helm is not installed locally. Aspire's `validate-kubernetes` publish step passed before artifact generation.

## Exact blocker

The pinned official integration constructs every node pool with autoscaling enabled and automatically prepares a user workload pool for application compute. The resulting Bicep proves that setting equal minimum and maximum counts does not disable autoscaling and does not prevent the additional pool:

```bicep
agentPoolProfiles: [
  {
    name: 'system'
    count: 1
    maxCount: 1
    minCount: 1
    enableAutoScaling: true
    mode: 'System'
  }
  {
    name: 'workload'
    count: 1
    maxCount: 3
    minCount: 1
    enableAutoScaling: true
    mode: 'User'
  }
]
```

W03 cannot proceed to a disposable deployment without either an upstream Aspire API/change that supports the required single fixed pool or an explicit revision to #3/#6. Hand-authored Bicep, existing-cluster mode, and post-deploy patches remain excluded.
