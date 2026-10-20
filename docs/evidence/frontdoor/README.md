# Azure Front Door deployment evidence

Issue: #21

The checked-in `generated/` directory is a deterministic snapshot of the
Aspire-owned Azure and Kubernetes infrastructure for the persistent Squad on
AKS environment. It is evidence, not a second deployment source.

The live endpoint produced by this model is:

<https://app-endpoint-bz5jgi5kxok3u-gzhsaxg9aqapd8cg.b02.azurefd.net>

Aspire 13.4.3's Front Door integration resolves origin hostnames through the
compute environment. The pinned AKS integration does not implement that host
resolver, and its LoadBalancer address is created later by Helm. The AppHost
therefore uses the official `AzureFrontDoorResource` and `WithOrigin(app)`
model with an AppHost-owned provisioning callback, while the Helm Service owns
the stable `squad-on-aks-live.westus2.cloudapp.azure.com` DNS label. This keeps
both the edge and origin identity in the Aspire source of truth.

The snapshot must contain:

- the existing one-node AKS and Basic ACR infrastructure;
- the existing Helm resources, including one app replica, the public
  `LoadBalancer` Service on port 80 to target port 8080, internal dashboard,
  workspace initialization, probes, budgets, and Secret;
- an Azure Front Door Standard profile, endpoint, origin group, HTTP origin,
  explicitly enabled endpoint/origin/route on the stable `2024-09-01` API,
  30-second `/health/ready` HTTP health probe, HTTPS redirecting route, and
  HTTPS endpoint output.

Regenerate and validate the snapshot from the repository root:

```bash
export Parameters__copilot_token="deployment-probe-not-used"
export Parameters__workspace_path="/workspace"

rm -rf docs/evidence/frontdoor/generated
aspire publish \
  --output-path docs/evidence/frontdoor/generated \
  --non-interactive

./scripts/validate-deployment-artifacts.sh
az bicep build \
  --file docs/evidence/frontdoor/generated/main.bicep \
  --stdout >/dev/null
helm lint docs/evidence/frontdoor/generated
```

Publishing twice into separate temporary directories and comparing them,
excluding image version timestamps if present, proves reproducibility.
