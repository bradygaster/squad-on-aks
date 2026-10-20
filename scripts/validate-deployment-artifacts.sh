#!/usr/bin/env bash
set -euo pipefail

artifact_root="${1:-docs/evidence/frontdoor/generated}"
main_bicep="$artifact_root/main.bicep"
frontdoor_bicep="$artifact_root/frontdoor/frontdoor.bicep"
app_service="$artifact_root/templates/app/service.yaml"
app_deployment="$artifact_root/templates/app/deployment.yaml"
dashboard_service="$artifact_root/templates/aks-dashboard/service.yaml"

for file in \
  "$main_bicep" \
  "$frontdoor_bicep" \
  "$app_service" \
  "$app_deployment" \
  "$dashboard_service"; do
  test -f "$file"
done

grep -Fq "module frontdoor 'frontdoor/frontdoor.bicep'" "$main_bicep"
grep -Fq "Microsoft.Cdn/profiles@2024-09-01" "$frontdoor_bicep"
grep -Fq "Microsoft.Cdn/profiles/afdEndpoints@2024-09-01" "$frontdoor_bicep"
grep -Fq "Microsoft.Cdn/profiles/originGroups@2024-09-01" "$frontdoor_bicep"
grep -Fq "Microsoft.Cdn/profiles/originGroups/origins@2024-09-01" "$frontdoor_bicep"
grep -Fq "Microsoft.Cdn/profiles/afdEndpoints/routes@2024-09-01" "$frontdoor_bicep"
test "$(grep -Fc "enabledState: 'Enabled'" "$frontdoor_bicep")" -eq 3
grep -Fq "probePath: '/health/ready'" "$frontdoor_bicep"
grep -Fq "probeProtocol: 'Http'" "$frontdoor_bicep"
grep -Fq 'probeIntervalInSeconds: 30' "$frontdoor_bicep"
grep -Fq "forwardingProtocol: 'HttpOnly'" "$frontdoor_bicep"
grep -Fq "httpsRedirect: 'Enabled'" "$frontdoor_bicep"
grep -Fq "output app_endpointUrl string = 'https://" "$frontdoor_bicep"

grep -Fq 'type: "LoadBalancer"' "$app_service"
grep -Fq 'port: 80' "$app_service"
grep -Fq 'targetPort: 8080' "$app_service"
grep -Fq 'replicas: 1' "$app_deployment"
grep -Fq 'agentpool: "system"' "$app_deployment"
grep -Fq 'path: "/health/live"' "$app_deployment"
grep -Fq 'path: "/health/ready"' "$app_deployment"
grep -Fq 'type: "ClusterIP"' "$dashboard_service"

printf 'deployment artifacts validated: %s\n' "$artifact_root"
