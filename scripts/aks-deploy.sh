#!/usr/bin/env bash
# Deploys Squad to AKS, privately, end to end, and can be rerun at any time (it reuses what exists).
#
#   Azure__SubscriptionId=<id> scripts/aks-deploy.sh
#
# Optional: Azure__ResourceGroup (rg-squad-on-aks), Azure__Location (westus2), SOAKS_WORKER_IMAGE (an already pushed
# worker image; otherwise the worker is built for arm64 and pushed to the cluster's registry), SOAKS_NUGET_SOURCE and
# SOAKS_NPM_REGISTRY (package feeds for image builds), SOAKS_AKS_AUTHORIZED_IPS (CIDRs allowed to reach the cluster API;
# defaults to this machine's public address). No credential is printed.
set -euo pipefail
cd "$(dirname "$0")/.."

: "${Azure__SubscriptionId:?set Azure__SubscriptionId to the subscription to deploy into}"
export Azure__SubscriptionId
export Azure__ResourceGroup="${Azure__ResourceGroup:-rg-squad-on-aks}"
export Azure__Location="${Azure__Location:-westus2}"
export Parameters__deployment_target=aks
export Parameters__workspace_template="${Parameters__workspace_template:-starter}"
export SOAKS_NUGET_SOURCE="${SOAKS_NUGET_SOURCE:-}"
npm_registry="${SOAKS_NPM_REGISTRY:-}"
export SOAKS_AKS_AUTHORIZED_IPS="${SOAKS_AKS_AUTHORIZED_IPS:-$(curl -fsS --max-time 10 https://api.ipify.org)/32}"
if [[ -z "${Parameters__copilot_token:-}" ]]; then
  Parameters__copilot_token="$(gh auth token)"
fi
export Parameters__copilot_token

[[ "$(az account show --query id -o tsv)" == "$Azure__SubscriptionId" ]] \
  || { echo "The active Azure subscription is not $Azure__SubscriptionId; run az account set first." >&2; exit 1; }

# Aspire logs in to the registry itself; an isolated Docker config avoids the desktop credential helper, and needs
# the buildx plugin linked in.
export DOCKER_CONFIG="${DOCKER_CONFIG:-${TMPDIR:-/tmp}/squad-on-aks-docker}"
mkdir -p "$DOCKER_CONFIG/cli-plugins"
[[ -f "$DOCKER_CONFIG/config.json" ]] || printf '{"auths":{}}\n' > "$DOCKER_CONFIG/config.json"
for plugin in /Applications/Docker.app/Contents/Resources/cli-plugins/docker-buildx "$HOME/.docker/cli-plugins/docker-buildx"; do
  [[ -e "$plugin" ]] && { ln -sf "$plugin" "$DOCKER_CONFIG/cli-plugins/docker-buildx"; break; }
done

kubeconfig="${SOAKS_AKS_KUBECONFIG:-${TMPDIR:-/tmp}/squad-on-aks-kubeconfig}"

deploy() {
  # Aspire uses its own temporary kubeconfig while deploying.
  (unset KUBECONFIG; dotnet aspire deploy --non-interactive)
}

registry_name() { az acr list -g "$Azure__ResourceGroup" --query '[0].name' -o tsv 2>/dev/null || true; }

# 1. The cluster and its registry. The first run provisions them (the host comes up before a worker image exists).
acr="$(registry_name)"
if [[ -z "$acr" ]]; then
  echo "Provisioning AKS and its registry (first run; several minutes)..."
  export Parameters__worker_image="pending-worker-image"
  export SOAKS_REGISTRY_ENDPOINT=""
  deploy || true
  acr="$(registry_name)"
  [[ -n "$acr" ]] || { echo "Provisioning did not create a container registry." >&2; exit 1; }
fi
export SOAKS_REGISTRY_ENDPOINT="$acr.azurecr.io"
cluster="$(az aks list -g "$Azure__ResourceGroup" --query '[0].name' -o tsv)"
az aks get-credentials -g "$Azure__ResourceGroup" -n "$cluster" --file "$kubeconfig" --overwrite-existing -o none

# 2. The worker image (arm64, like the nodes), in the cluster's registry, referenced by digest.
if [[ -z "${SOAKS_WORKER_IMAGE:-}" ]]; then
  echo "Building and pushing the worker image..."
  build_args=()
  [[ -n "$SOAKS_NUGET_SOURCE" ]] && build_args+=(--build-arg "NUGET_SOURCE=$SOAKS_NUGET_SOURCE")
  [[ -n "$npm_registry" ]] && build_args+=(--build-arg "NPM_REGISTRY=$npm_registry")
  docker build --quiet --platform linux/arm64 -f src/SquadOnAks.Worker/Dockerfile "${build_args[@]}" \
    -t "$SOAKS_REGISTRY_ENDPOINT/soaks-worker:latest" . >/dev/null
  az acr login -n "$acr" >/dev/null
  docker push --quiet "$SOAKS_REGISTRY_ENDPOINT/soaks-worker:latest" >/dev/null
  digest="$(docker inspect --format '{{index .RepoDigests 0}}' "$SOAKS_REGISTRY_ENDPOINT/soaks-worker:latest")"
  SOAKS_WORKER_IMAGE="$SOAKS_REGISTRY_ENDPOINT/soaks-worker@${digest##*@}"
fi
export Parameters__worker_image="$SOAKS_WORKER_IMAGE"

# 3. A push-only registry token for the deploy worker, stored only in the cluster (Secret soaks-registry-push).
az acr token show -r "$acr" -n soaks-push -o none 2>/dev/null \
  || az acr token create -r "$acr" -n soaks-push --scope-map _repositories_push --no-passwords -o none
if ! KUBECONFIG="$kubeconfig" kubectl -n default get secret soaks-registry-push -o name >/dev/null 2>&1; then
  password="$(az acr token credential generate -r "$acr" -n soaks-push --password1 --query 'passwords[0].value' -o tsv)"
  KUBECONFIG="$kubeconfig" kubectl -n default create secret generic soaks-registry-push \
    --from-literal=username=soaks-push --from-literal=password="$password" >/dev/null
  unset password
fi

# 4. Squad itself.
echo "Deploying Squad..."
deploy
KUBECONFIG="$kubeconfig" kubectl -n default rollout status deploy/app-deployment --timeout=240s

cat <<EOF

Squad is deployed privately to $cluster. Open it through a port-forward:

  KUBECONFIG=$kubeconfig kubectl -n default port-forward --address 127.0.0.1 service/app-service 58090:80

then browse to http://127.0.0.1:58090/ (each online app is at http://<app-id>.localhost:58090/).
Worker image: $Parameters__worker_image
EOF
