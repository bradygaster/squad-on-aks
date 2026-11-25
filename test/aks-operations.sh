#!/usr/bin/env bash
set -euo pipefail

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/squad-aks-operations.XXXXXX")"
trap 'rm -rf -- "$temp_dir"' EXIT
fake_bin="$temp_dir/bin"
mkdir -p "$fake_bin"

cat > "$fake_bin/dotnet" <<'STUB'
#!/usr/bin/env bash
if [[ "$*" == "--version" ]]; then
  printf '10.0.301\n'
elif [[ "$*" == "tool run aspire --version" ]]; then
  printf '13.6.5+test\n'
else
  exit 2
fi
STUB

cat > "$fake_bin/az" <<'STUB'
#!/usr/bin/env bash
[[ "$*" == "account show --query id --output tsv" ]] || exit 2
printf '%s\n' "${AZURE_TEST_SUBSCRIPTION_ID:-subscription-test}"
STUB

cat > "$fake_bin/docker" <<'STUB'
#!/usr/bin/env bash
[[ "${PREFLIGHT_DOCKER_DOWN:-false}" != "true" ]] && [[ "$*" == "info" ]]
STUB

cat > "$fake_bin/kubectl" <<'STUB'
#!/usr/bin/env bash
if [[ "$1" == "version" ]]; then
  printf '{"clientVersion":{"gitVersion":"v1.34.1"}}\n'
  exit 0
fi

if [[ "$1" == "config" && "$2" == "current-context" ]]; then
  printf '%s\n' "${KUBE_TEST_CONTEXT:-other-context}"
  exit 0
fi

if [[ "$1" == "api-resources" ]]; then
  exit 0
fi

if [[ "$1" == "get" && "$2" == "services" ]]; then
  if [[ "${Parameters__deployment_target:-aks}" == "docker-desktop" ]]; then
    if [[ "${AKS_SMOKE_FAILURE:-}" == "public-service" ]]; then
      printf 'app-service|LoadBalancer|\ndocker-desktop-dashboard-service|ClusterIP|\n'
    else
      printf 'app-service|ClusterIP|\ndocker-desktop-dashboard-service|ClusterIP|\n'
    fi
  elif [[ "${AKS_SMOKE_FAILURE:-}" == "public-service" ]]; then
    printf 'app-service|LoadBalancer|\naks-dashboard-service|LoadBalancer|\n'
  else
    printf 'app-service|LoadBalancer|\naks-dashboard-service|ClusterIP|\n'
  fi
  exit 0
fi

if [[ "$1" == "get" && "$2" == "deployment" ]]; then
  if [[ "${AKS_SMOKE_FAILURE:-}" == "replicas" && "$3" == "app-deployment" ]]; then
    printf '2 2'
  else
    printf '1 1'
  fi
  exit 0
fi

if [[ "$1" == "get" && "$2" == "service" && "$3" == "app-service" ]]; then
  printf 'LoadBalancer|203.0.113.10|'
  exit 0
fi

if [[ "$1" == "get" && "$2" == "ingresses" && "${AKS_SMOKE_FAILURE:-}" == "forbidden-resource" ]]; then
  printf 'ingress/example\n'
  exit 0
fi

if [[ "$1" == "get" ]]; then
  exit 0
fi

exit 2
STUB

cat > "$fake_bin/helm" <<'STUB'
#!/usr/bin/env bash
[[ "$*" == "version --short" ]] || exit 2
printf '%s\n' "${HELM_TEST_VERSION:-v4.3.1+test}"
STUB

cat > "$fake_bin/curl" <<'STUB'
#!/usr/bin/env bash
url=""
for argument in "$@"; do
  url="$argument"
done

case "$url" in
  */health/ready)
    [[ "${AKS_SMOKE_FAILURE:-}" != "readiness" ]] || exit 22
    ;;
  */health/live)
    ;;
  */casting/status)
    if [[ "${AKS_SMOKE_FAILURE:-}" == "cast-state" ]]; then
      printf '{"state":"Ready","hasCast":true,"canTakeRequests":true}\n'
    else
      printf '{"state":"Needs cast","hasCast":false,"canTakeRequests":false}\n'
    fi
    ;;
  *)
    exit 2
    ;;
esac
STUB

chmod +x "$fake_bin"/*

assert_contains() {
  local content="$1"
  local expected="$2"
  if [[ "$content" != *"$expected"* ]]; then
    printf 'FAIL expected output to contain: %s\n%s\n' "$expected" "$content" >&2
    exit 1
  fi
}

token="test-only-copilot-token"
preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    Azure__SubscriptionId=subscription-test \
    Azure__Location=westus2 \
    Azure__ResourceGroup=rg-test \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"
assert_contains "$preflight_output" "Preflight passed for aks."
if [[ "$preflight_output" == *"$token"* ]]; then
  printf 'FAIL preflight output contained the Copilot credential\n' >&2
  exit 1
fi
printf 'PASS preflight succeeds without printing the Copilot credential\n'

if preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    AZURE_TEST_SUBSCRIPTION_ID=unexpected-subscription \
    Azure__SubscriptionId=subscription-test \
    Azure__Location=westus2 \
    Azure__ResourceGroup=rg-test \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL preflight accepted a mismatched Azure subscription\n' >&2
  exit 1
fi
assert_contains "$preflight_output" "Active Azure subscription does not match Azure__SubscriptionId"
printf 'PASS preflight rejects an unexpected Azure subscription\n'

if preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    HELM_TEST_VERSION=v4.1.0 \
    Azure__SubscriptionId=subscription-test \
    Azure__Location=westus2 \
    Azure__ResourceGroup=rg-test \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL preflight accepted an unsupported Helm version\n' >&2
  exit 1
fi
assert_contains "$preflight_output" "Helm 4.2.0 or later is required"
printf 'PASS preflight rejects Helm earlier than 4.2.0\n'

if preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    PREFLIGHT_DOCKER_DOWN=true \
    Azure__SubscriptionId=subscription-test \
    Azure__Location=westus2 \
    Azure__ResourceGroup=rg-test \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL preflight accepted an unreachable container runtime\n' >&2
  exit 1
fi
assert_contains "$preflight_output" "container runtime is not reachable"
printf 'PASS preflight rejects an unreachable container runtime\n'

if preflight_output="$(
  env -u Parameters__copilot_token \
    PATH="$fake_bin:/usr/bin:/bin" \
    Azure__SubscriptionId=subscription-test \
    Azure__Location=westus2 \
    Azure__ResourceGroup=rg-test \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL preflight accepted a missing Copilot credential\n' >&2
  exit 1
fi
assert_contains "$preflight_output" "Parameters__copilot_token is missing"
if [[ "$preflight_output" == *"$token"* ]]; then
  printf 'FAIL preflight output contained the Copilot credential\n' >&2
  exit 1
fi
printf 'PASS preflight rejects a missing credential without exposing it\n'

smoke_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    "$repo_root/scripts/aks-smoke.sh" 2>&1
)"
assert_contains "$smoke_output" "Application URL: http://203.0.113.10"
assert_contains "$smoke_output" "AKS smoke checks passed."
printf 'PASS smoke accepts the expected deployment and discovers its URL\n'

local_preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    KUBE_TEST_CONTEXT=docker-desktop \
    Parameters__deployment_target=docker-desktop \
    Parameters__registry_endpoint=host.docker.internal:5000 \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"
assert_contains "$local_preflight_output" "kubectl is using the docker-desktop context"
assert_contains "$local_preflight_output" "Preflight passed for docker-desktop."
printf 'PASS local preflight accepts Docker Desktop without Azure inputs\n'

if local_preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    KUBE_TEST_CONTEXT=another-context \
    Parameters__deployment_target=docker-desktop \
    Parameters__registry_endpoint=host.docker.internal:5000 \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL local preflight accepted a non-Docker-Desktop context\n' >&2
  exit 1
fi
assert_contains "$local_preflight_output" "kubectl context must be docker-desktop"
printf 'PASS local preflight rejects a different kubectl context\n'

if local_preflight_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    KUBE_TEST_CONTEXT=docker-desktop \
    Parameters__deployment_target=docker-desktop \
    Parameters__copilot_token="$token" \
    "$repo_root/scripts/aks-preflight.sh" 2>&1
)"; then
  printf 'FAIL local preflight accepted a missing registry endpoint\n' >&2
  exit 1
fi
assert_contains "$local_preflight_output" "Parameters__registry_endpoint is missing"
printf 'PASS local preflight requires a registry endpoint\n'

local_smoke_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    Parameters__deployment_target=docker-desktop \
    SQUAD_APP_URL=http://127.0.0.1:8080 \
    "$repo_root/scripts/aks-smoke.sh" 2>&1
)"
assert_contains "$local_smoke_output" "app-service and the Aspire dashboard Service are internal ClusterIP Services"
assert_contains "$local_smoke_output" "Application URL: http://127.0.0.1:8080"
assert_contains "$local_smoke_output" "docker-desktop smoke checks passed."
printf 'PASS local smoke accepts internal Services and a port-forward URL\n'

if local_smoke_output="$(
  env PATH="$fake_bin:/usr/bin:/bin" \
    Parameters__deployment_target=docker-desktop \
    SQUAD_APP_URL=http://127.0.0.1:8080 \
    AKS_SMOKE_FAILURE=public-service \
    "$repo_root/scripts/aks-smoke.sh" 2>&1
)"; then
  printf 'FAIL local smoke accepted public Service exposure\n' >&2
  exit 1
fi
assert_contains "$local_smoke_output" "unexpected public LoadBalancer Service"
printf 'PASS local smoke rejects public Service exposure\n'

for failure in public-service replicas forbidden-resource readiness cast-state; do
  if smoke_output="$(
    env PATH="$fake_bin:/usr/bin:/bin" \
      AKS_SMOKE_FAILURE="$failure" \
      "$repo_root/scripts/aks-smoke.sh" 2>&1
  )"; then
    printf 'FAIL smoke accepted fixture failure: %s\n' "$failure" >&2
    exit 1
  fi
  printf 'PASS smoke rejects %s\n' "$failure"
done

printf '\nAKS operations checks passed.\n'
