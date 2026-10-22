#!/usr/bin/env bash
set -euo pipefail

artifact_root="${1:-artifacts/aspire}"
main_bicep="$artifact_root/main.bicep"
aks_bicep="$artifact_root/aks/aks.bicep"
acr_bicep="$artifact_root/aks-acr/aks-acr.bicep"
app_service="$artifact_root/templates/app/service.yaml"
app_deployment="$artifact_root/templates/app/deployment.yaml"
dashboard_service="$artifact_root/templates/aks-dashboard/service.yaml"

for file in \
  "$main_bicep" \
  "$aks_bicep" \
  "$acr_bicep" \
  "$app_service" \
  "$app_deployment" \
  "$dashboard_service"; do
  test -f "$file"
done

grep -Fq "module aks_acr 'aks-acr/aks-acr.bicep'" "$main_bicep"
grep -Fq "module aks 'aks/aks.bicep'" "$main_bicep"
if grep -RqiE 'Microsoft\.Cdn|front[ -]?door|azure-dns-label-name' "$artifact_root"; then
  printf 'unexpected Front Door or Front Door-only DNS content in %s\n' "$artifact_root" >&2
  exit 1
fi

grep -Fq 'type: "LoadBalancer"' "$app_service"
grep -Fq 'port: 80' "$app_service"
grep -Fq 'targetPort: 8080' "$app_service"
grep -Fq 'replicas: 1' "$app_deployment"
grep -Fq 'agentpool: "system"' "$app_deployment"
grep -Fq 'path: "/health/live"' "$app_deployment"
grep -Fq 'path: "/health/ready"' "$app_deployment"
grep -Fq 'terminationGracePeriodSeconds: 30' "$app_deployment"
grep -Fq 'runAsNonRoot: true' "$app_deployment"
grep -Fq 'runAsUser: 1654' "$app_deployment"
grep -Fq 'runAsGroup: 1654' "$app_deployment"
grep -Fq 'fsGroup: 1654' "$app_deployment"
grep -Fq 'fsGroupChangePolicy: "OnRootMismatch"' "$app_deployment"
grep -Fq 'allowPrivilegeEscalation: false' "$app_deployment"
grep -Fq 'readOnlyRootFilesystem: true' "$app_deployment"
grep -Fq 'privileged: false' "$app_deployment"
grep -Fq -- '- "ALL"' "$app_deployment"
grep -Fq 'type: "RuntimeDefault"' "$app_deployment"
grep -Fq 'name: "fixture-init"' "$app_deployment"
grep -Fq -- '- "--initialize-workspace"' "$app_deployment"
grep -Fq 'mountPath: "/workspace"' "$app_deployment"
test "$(grep -Fc 'mountPath: "/workspace"' "$app_deployment")" -eq 2
test "$(grep -Fc 'secretRef:' "$app_deployment")" -eq 1
if grep -A40 -F 'name: "fixture-init"' "$app_deployment" | grep -Fq 'secretRef:'; then
  printf 'fixture init container must not receive the Copilot secret\n' >&2
  exit 1
fi
grep -Fq 'type: "ClusterIP"' "$dashboard_service"

printf 'deployment artifacts validated: %s\n' "$artifact_root"
