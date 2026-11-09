# Local Kubernetes

Deploy the platform to Docker Desktop Kubernetes, then optionally reproduce
the fixed eight-ball worker proof. Run commands from the repository root.
These local proofs are not generic generated-app hosting or AKS acceptance.

## Deploy to Docker Desktop Kubernetes (opt-in)

The AppHost can target an existing Docker Desktop Kubernetes cluster for local
deployment. This is opt-in; omitting the target selector continues to use the
existing Azure AKS deployment. The local target does not provision Azure
resources. Aspire publishes the same application image to the configured
registry, and Kubernetes pulls it from that registry. The Docker Desktop
image build matches the local ARM64 or x64 host; the AKS image build is unchanged.

Enable Kubernetes from Docker Desktop's **Kubernetes** view by selecting
**Create cluster** ([Docker Desktop Kubernetes instructions](https://docs.docker.com/desktop/use-desktop/kubernetes/)).
Aspire uses its [existing-cluster Kubernetes target](https://aspire.dev/deployment/kubernetes/clusters/).
Install or provide `kubectl` and Helm 4.2.0 or later (Docker Desktop may
install `kubectl` on macOS, but the CLI must be on `PATH`). Before deploying,
confirm the cluster is ready and the active context is exactly `docker-desktop`:

```bash
kubectl config current-context
kubectl get nodes
```

If needed, select the Docker Desktop context explicitly, then recheck it:

```bash
kubectl config use-context docker-desktop
kubectl config current-context
```

Provide a container registry endpoint reachable from both the host (for image
pushes) and Docker Desktop Kubernetes nodes (for image pulls). For Docker
Desktop's **kind** provisioner, the verified local setup follows the
[kind local-registry guidance](https://kind.sigs.k8s.io/docs/user/local-registry/).
It adds one registry-specific containerd host mapping to the existing node,
without recreating the cluster or changing global Docker daemon configuration:

```bash
docker run --detach --name squad-on-aks-registry-w13 \
  --publish 127.0.0.1:5057:5000 --network kind registry:3
curl --fail http://127.0.0.1:5057/v2/

node="$(kubectl get nodes -o jsonpath='{.items[0].metadata.name}')"
docker exec "$node" mkdir -p /etc/containerd/certs.d/localhost:5057
printf '[host."http://squad-on-aks-registry-w13:5000"]\n  capabilities = ["pull", "resolve"]\n' \
  | docker exec --interactive "$node" sh -c \
    'cat > /etc/containerd/certs.d/localhost:5057/hosts.toml'

docker tag registry:3 localhost:5057/registry-probe:w13
docker push localhost:5057/registry-probe:w13
docker exec "$node" crictl pull localhost:5057/registry-probe:w13
```

Use an unused host port and registry name, and do not overwrite an existing
node registry mapping. Both the push and node pull must pass before deploying.
This setup is specific to Docker Desktop's kind provisioner, not kubeadm.
The host name alone is insufficient: an HTTP registry at
`host.docker.internal:5000` failed the default HTTPS image push in the live test.

Set the opt-in target and registry endpoint, along with the Copilot token:

```bash
export Parameters__deployment_target="docker-desktop"
export Parameters__registry_endpoint="localhost:5057"
export Parameters__copilot_token="<copilot-supported-token>"
```

If your local network blocks NuGet, optionally supply an approved source for
the Docker Desktop image build:

```bash
export DAYDREAM_NUGET_SOURCE="https://packagefeedproxy.microsoft.io/nuget/v3/index.json"
```

This passes an optional `NUGET_SOURCE` build argument to the locked restore.
It is not set by default and is ignored for the AKS target. Leave it unset in CI.

Azure subscription, region, resource-group, and Azure CLI login inputs are not
required for this target. The preflight requires Docker, `kubectl`, Helm 4.2.0
or later, the `docker-desktop` context, and both parameters:

```bash
./scripts/aks-preflight.sh && dotnet aspire deploy \
  --environment LocalKubernetes --non-interactive
```

`dotnet aspire` uses the repository-pinned 13.4.3 tool rather than a global
installation. Use a distinct environment for the local target: Aspire may
reuse previously saved parameter values within an environment.

The chart uses fixed resource names in the `default` namespace, so only one
release can occupy it at a time; a different Aspire environment or release name
does not isolate a second deployment. Destroy the existing release through its
original Aspire environment before deploying another.

The generated application Service is `ClusterIP`; the Aspire dashboard Service
also remains internal. Access the app only through a deliberate local
port-forward:

```bash
kubectl port-forward --namespace default service/app-service 8080:80
```

In another terminal, run the smoke check against that forwarded URL:

```bash
export Parameters__deployment_target="docker-desktop"
export SQUAD_APP_URL="http://127.0.0.1:8080"
./scripts/aks-smoke.sh
```

The smoke check verifies both Services are `ClusterIP`, no public
`LoadBalancer` Service exists, both deployments have one ready replica, and
the application health endpoints and initial **Needs cast** state pass.

Run the same `dotnet aspire deploy --environment LocalKubernetes --non-interactive`
command to upgrade the release. A rollout replaces the app pod; workspace retention depends on the selected
template and storage. The eight-ball proof below uses a retained PVC. Restart
the port-forward after pod replacement. To remove only this
local deployment, verify `kubectl config current-context` is still
`docker-desktop`, then run:

```bash
dotnet aspire destroy --environment LocalKubernetes --yes --non-interactive
helm list --namespace default --filter '^localkubernetes$'
kubectl get deployments,services,pods,secrets,configmaps --namespace default \
  -l app.kubernetes.io/instance=localkubernetes
```

Both inspections should show no owned release/resources. The cluster and
external registry remain. If the first deployment fails before Aspire records
Helm state, `aspire destroy` can report nothing to destroy while the failed
release remains. Inspect that exact release with `helm status`, then remove
only that owned failed release with `helm uninstall <release> --namespace default`.
This is failure recovery, not an additional normal deployment step.

> The following proof records a specific local registry and node mapping.
> A reset cluster must be prepared again; do not assume those resources exist.

## Local Kubernetes eight-ball proof

The bounded local target is Docker Desktop Kubernetes, not Azure. Daydream's
workspace uses a retained PVC. The original workspace remains at
`/workspace/fixture`; the eight-ball template uses `/workspace/eight-ball`.
Each generated app has its own Squad and namespace. Production uses **one API
image containing the built React assets**, not two production services.

The worker image in `src/SquadOnAks.Worker` contains pinned .NET, Node, Aspire,
Helm, and runtime-image inputs plus offline dependency caches. Build it from
the repository root:

```bash
npm config set registry "https://packagefeedproxy.microsoft.io/npm/"
test "$(npm config get registry)" = 'https://packagefeedproxy.microsoft.io/npm/'
docker build -f src/SquadOnAks.Worker/Dockerfile \
  --build-arg NUGET_SOURCE=https://packagefeedproxy.microsoft.io/nuget/v3/index.json \
  --build-arg NPM_REGISTRY=https://packagefeedproxy.microsoft.io/npm/ \
  -t localhost:5057/daydream-worker:local-proof .
docker push localhost:5057/daydream-worker:local-proof
```

The existing local proof registry is `daydream-proof-registry`, published only
on `127.0.0.1:5057`, and attached to the node's Docker `kind` network. This
installation's node maps `localhost:5057` pulls to Docker network alias
`squad-on-aks-registry-w13:5000` through containerd's registry hosts configuration.
That node-side mapping is a prerequisite; `localhost` inside a node or pod is
not the Mac's loopback interface. The registry is unauthenticated and is for
this disposable local proof only.

Deploy through the pinned repository Aspire CLI. Select the registry-qualified
digest printed by the push, not an unqualified local image name:

```bash
export Parameters__deployment_target=docker-desktop
export Parameters__workspace_template=eight-ball
export Parameters__registry_endpoint=localhost:5057
export Parameters__worker_image="localhost:5057/daydream-worker@sha256:<digest-from-push>"
export Parameters__copilot_token="$(gh auth token)"
export DAYDREAM_REGISTRY_PUSH="$(docker inspect daydream-proof-registry \
  --format '{{(index .NetworkSettings.Networks "kind").IPAddress}}'):5000"
export DAYDREAM_NUGET_SOURCE=https://packagefeedproxy.microsoft.io/nuget/v3/index.json
dotnet aspire deploy --environment LocalKubernetes --clear-cache --non-interactive
kubectl --context docker-desktop -n default port-forward \
  --address 127.0.0.1 service/app-service 58080:80
```

Open **http://127.0.0.1:58080/**. `--clear-cache` is intentional when changing
deployment inputs: this Aspire version otherwise reuses saved parameter values,
including an older worker digest. It does not erase the PVC or app history.
Port-forward again after replacing the Daydream pod. The Aspire dashboard
stays internal and requires a separate, manually requested port-forward.

Cast the Squad, submit a small wave, then use **Put it online** or **Update your
online app**. An explicit update builds a new revision of the same app-owned
release; a readiness GET never starts work. The eight-ball app is available at
`/online/first/` only after frontend and API probes pass.
`GET /workspace/deployments` retains failed and successful revisions.

Build and validation jobs run without a Kubernetes token and with read-only
source mounts. Deployment jobs receive only an app-scoped deploy identity.
The separate trusted provisioner owns namespace/RBAC creation; Kubernetes
requires unscoped `create`/`escalate` authorization to create a Role, while
read/update/bind operations stay name-restricted. Generated namespaces enforce
restricted Pod Security. Their declared NetworkPolicies are **not a verified
hostile-code boundary on Docker Desktop's kindnet**. A separate
`docker --network none` build proves offline operation, not CNI enforcement.

`sh tests/run.sh` in the packaged eight-ball workspace builds React, builds the
API, and checks its readiness and answer contract in the tokenless worker.
The native Squad harness is shared from the packaged starter template.
AppHost dependency restore unlocks the OS-specific implicit Aspire packages
when moving the macOS lock file to Linux; top-level package versions remain
pinned. This worker supports this fixed fixture and local registry topology,
not arbitrary generated architectures or production registry authentication.

Recorded local acceptance, including both real Squad waves and build-stage
restart/retry recovery, is in
[the acceptance evidence](evidence/local-kubernetes-acceptance.json).
