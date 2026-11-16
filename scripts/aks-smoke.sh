#!/usr/bin/env bash
set -u

namespace="default"
deployment_target="${Parameters__deployment_target:-aks}"
fail() {
  printf 'FAIL %s\n' "$1" >&2
  exit 1
}

pass() {
  printf 'PASS %s\n' "$1"
}

for tool in kubectl curl; do
  command -v "$tool" >/dev/null 2>&1 || fail "$tool is required and must be on PATH"
done

case "$deployment_target" in
  aks)
    dashboard_service="aks-dashboard-service"
    dashboard_deployment="aks-dashboard-deployment"
    ;;
  docker-desktop)
    dashboard_service="docker-desktop-dashboard-service"
    dashboard_deployment="docker-desktop-dashboard-deployment"
    ;;
  *)
    fail "unsupported Parameters__deployment_target '$deployment_target' (expected aks or docker-desktop)"
    ;;
esac
# Squad has no sign-in and holds a GitHub token, so it is private on every target: reach it through a port-forward.
app_service_type="ClusterIP"
app_url="${SQUAD_APP_URL:-}"
[[ -n "$app_url" ]] || fail "SQUAD_APP_URL is required; port-forward app-service and set its URL before running the smoke check"

service_rows="$(kubectl get services --namespace "$namespace" --output 'jsonpath={range .items[*]}{.metadata.name}{"|"}{.spec.type}{"|"}{.spec.externalIPs[*]}{"\n"}{end}')" \
  || fail "could not list Services in namespace $namespace"
app_service_found=false
dashboard_service_found=false
load_balancer_count=0

while IFS='|' read -r name type external_ips; do
  [[ -n "$name" ]] || continue
  case "$type" in
    LoadBalancer)
      fail "unexpected public LoadBalancer Service: $name"
      ;;
    NodePort)
      fail "unexpected public NodePort Service: $name"
      ;;
    ClusterIP)
      [[ -z "$external_ips" ]] || fail "$name declares unexpected spec.externalIPs: $external_ips"
      if [[ "$name" == "$dashboard_service" ]]; then
        dashboard_service_found=true
      elif [[ "$name" == "app-service" && "$app_service_type" == "ClusterIP" ]]; then
        app_service_found=true
      fi
      ;;
    *)
      fail "unexpected Service type '$type' on $name"
      ;;
  esac
done <<< "$service_rows"

[[ "$app_service_found" == true ]] || fail "app-service is not a $app_service_type"
[[ "$dashboard_service_found" == true ]] || fail "$dashboard_service is not an internal ClusterIP"
[[ "$load_balancer_count" -eq 0 ]] || fail "expected no public LoadBalancer Services; found $load_balancer_count"
pass "app-service and the Aspire dashboard Service are internal ClusterIP Services"

for deployment in app-deployment "$dashboard_deployment"; do
  replicas="$(kubectl get deployment "$deployment" --namespace "$namespace" --output 'jsonpath={.spec.replicas}{" "}{.status.readyReplicas}')" \
    || fail "could not read deployment/$deployment"
  read -r desired ready <<< "$replicas"
  [[ "$desired" == "1" && "$ready" == "1" ]] \
    || fail "deployment/$deployment must have exactly one ready replica (found ${desired:-0} desired, ${ready:-0} ready)"
done
pass "application and dashboard each have exactly one ready replica"

for resource in ingresses horizontalpodautoscalers persistentvolumeclaims; do
  objects="$(kubectl get "$resource" --namespace "$namespace" --output name)" \
    || fail "could not check $resource in namespace $namespace"
  # The one volume Squad owns is its workspace (apps, waves and team state).
  objects="$(printf '%s\n' "$objects" | grep -v '^persistentvolumeclaim/soaks-workspace$' || true)"
  [[ -z "$objects" ]] || fail "forbidden $resource found: $objects"
done

for api_group in gateway.networking.k8s.io cert-manager.io acme.cert-manager.io; do
  resources="$(kubectl api-resources --api-group="$api_group" --output=name)" \
    || fail "could not inspect installed resources in API group $api_group"
  while IFS= read -r resource; do
    [[ -n "$resource" ]] || continue
    objects="$(kubectl get "$resource" --namespace "$namespace" --output name)" \
      || fail "could not check $resource in namespace $namespace"
    [[ -z "$objects" ]] || fail "forbidden $api_group resource found: $objects"
  done <<< "$resources"
done
pass "no ingress, gateway, certificate or autoscaling resources, and no volume besides the workspace"

printf 'Application URL: %s\n' "$app_url"

curl --fail --silent --show-error --max-time 10 "$app_url/health/live" >/dev/null \
  || fail "liveness endpoint failed at $app_url/health/live"
curl --fail --silent --show-error --max-time 10 "$app_url/health/ready" >/dev/null \
  || fail "readiness endpoint failed at $app_url/health/ready"
pass "liveness and readiness endpoints return HTTP success"

cast_status="$(curl --fail --silent --show-error --max-time 10 "$app_url/casting/status")" \
  || fail "could not read initial cast status"
printf '%s' "$cast_status" | grep -Eq '"state"[[:space:]]*:[[:space:]]*"Needs cast"' \
  || fail "initial cast state is not Needs cast"
printf '%s' "$cast_status" | grep -Eq '"hasCast"[[:space:]]*:[[:space:]]*false' \
  || fail "initial cast state unexpectedly has a cast"
printf '%s' "$cast_status" | grep -Eq '"canTakeRequests"[[:space:]]*:[[:space:]]*false' \
  || fail "initial cast state unexpectedly accepts requests"
pass "initial application state is Needs cast"

if [[ "$deployment_target" == "aks" ]]; then
  printf '\nAKS smoke checks passed.\n'
else
  printf '\ndocker-desktop smoke checks passed.\n'
fi
