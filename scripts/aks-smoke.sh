#!/usr/bin/env bash
set -u

namespace="default"
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

service_rows="$(kubectl get services --namespace "$namespace" --output 'jsonpath={range .items[*]}{.metadata.name}{"|"}{.spec.type}{"|"}{.spec.externalIPs[*]}{"\n"}{end}')" \
  || fail "could not list Services in namespace $namespace"
app_service_found=false
dashboard_service_found=false
load_balancer_count=0

while IFS='|' read -r name type external_ips; do
  [[ -n "$name" ]] || continue
  case "$type" in
    LoadBalancer)
      load_balancer_count=$((load_balancer_count + 1))
      [[ "$name" == "app-service" ]] || fail "unexpected public LoadBalancer Service: $name"
      [[ -z "$external_ips" ]] || fail "$name declares unexpected spec.externalIPs: $external_ips"
      app_service_found=true
      ;;
    NodePort)
      fail "unexpected public NodePort Service: $name"
      ;;
    ClusterIP)
      [[ -z "$external_ips" ]] || fail "$name declares unexpected spec.externalIPs: $external_ips"
      if [[ "$name" == "aks-dashboard-service" ]]; then
        dashboard_service_found=true
      fi
      ;;
    *)
      fail "unexpected Service type '$type' on $name"
      ;;
  esac
done <<< "$service_rows"

[[ "$app_service_found" == true ]] || fail "app-service is not a LoadBalancer"
[[ "$dashboard_service_found" == true ]] || fail "aks-dashboard-service is not an internal ClusterIP"
[[ "$load_balancer_count" -eq 1 ]] || fail "expected exactly one LoadBalancer Service; found $load_balancer_count"
pass "only app-service is public; the Aspire dashboard Service is internal"

for deployment in app-deployment aks-dashboard-deployment; do
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
pass "no ingress, gateway, certificate, autoscaling, or persistent-volume resources are present"

service_details="$(kubectl get service app-service --namespace "$namespace" --output 'jsonpath={.spec.type}{"|"}{.status.loadBalancer.ingress[0].ip}{"|"}{.status.loadBalancer.ingress[0].hostname}')" \
  || fail "could not read app-service"
IFS='|' read -r service_type service_ip service_hostname <<< "$service_details"
[[ "$service_type" == "LoadBalancer" ]] || fail "app-service is not type LoadBalancer"
endpoint="${service_ip:-$service_hostname}"
[[ -n "$endpoint" ]] || fail "app-service has no public load-balancer address yet"
app_url="http://${endpoint}"
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

printf '\nAKS smoke checks passed.\n'
