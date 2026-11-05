using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class OnlineTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        ManifestChecks();
        await ValidationRunnerChecksAsync(fixtureSource);
        string root = Path.Combine(Path.GetTempPath(), "daydream-online-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, root, NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            DeploymentStateChecks(workspace);
            File.WriteAllText(Path.Combine(root, "index.html"), "<h1>One</h1>");
            File.WriteAllText(Path.Combine(root, ".env"), "never public");
            File.WriteAllText(Path.Combine(root, "app.cs"), "never public");
            Directory.CreateDirectory(Path.Combine(root, ".private"));
            File.WriteAllText(Path.Combine(root, ".private", "secret.json"), "{}");
            var artifacts = new WorkspaceArtifacts(workspace);
            Require(artifacts.ReadOnlineFiles().Files.Keys.All(StaticAppManifest.PublicPath), "Only public paths are snapshotted.");
            Require(!artifacts.ReadOnlineFiles().Files.ContainsKey(".env") &&
                !artifacts.ReadOnlineFiles().Files.ContainsKey("app.cs"), "Private and server files are not public bytes.");
            string outside = Path.Combine(Path.GetTempPath(), "online-outside-" + Guid.NewGuid().ToString("N") + ".js");
            try
            {
                File.WriteAllText(outside, "private");
                File.CreateSymbolicLink(Path.Combine(root, "linked.js"), outside);
                Require(!artifacts.ReadOnlineFiles().Files.ContainsKey("linked.js"), "Symlink targets must never be copied.");
            }

            finally { File.Delete(outside); }
            var cluster = new FakeCluster();
            AppOnlineService Create(bool available, string id = "first") => new(new(id), new(available), cluster,
                workspace, artifacts, NullLogger<AppOnlineService>.Instance);
            AppOnlineService local = Create(false);
            Require((await local.GetAsync(default)) is { Available: false, Online: false, Url: null }, "Local mode is unavailable.");
            await ThrowsAsync(() => local.PutAsync(default));
            await ThrowsAsync(() => local.RemoveAsync(default));
            Require(cluster.Reads == 0 && cluster.Writes == 0, "Local mode must never contact the API.");

            AppOnlineService online = Create(true);
            Require(!(await online.GetAsync(default)).Online, "No saved request is not online.");
            Require(!(await online.PutAsync(default)).Online, "Submission is not readiness.");
            Require(cluster.Objects.Count == 4, "One app creates exactly its four owned objects.");
            cluster.Ready("first");
            Require((await online.GetAsync(default)) is { Online: true, Url: "/online/first/" }, "Ready app gets a fixed proxy URL.");
            Require(JsonSerializer.Deserialize<AppOnlineState>(File.ReadAllText(workspace.StatePath("daydream-online.json")))?.Online == true,
                "Observed online status is persisted per app.");
            Require((await Create(true).GetAsync(default)).Online, "Online intent survives service recreation.");
            Require((await online.GetDestinationAsync(default))?.Host == "10.96.0.10", "Proxy uses the owned ClusterIP only.");

            var deployment = (V1Deployment)cluster.Objects[("Deployment", StaticAppManifest.Name("first"))];
            deployment.Status.Replicas = 2;
            Require(!(await online.GetAsync(default)).Online, "An old ready replica must not mask an unready update.");
            cluster.Ready("first");
            var service = (V1Service)cluster.Objects[("Service", StaticAppManifest.Name("first"))];
            service.Spec.Selector["another"] = "pod";
            Require(await online.GetDestinationAsync(default) is null, "Extra selectors cannot redirect the proxy.");
            service.Spec.Selector.Remove("another");
            service.Spec.Type = "ExternalName";
            Require(await online.GetDestinationAsync(default) is null, "ExternalName destinations are rejected.");
            service.Spec.Type = "ClusterIP";
            service.Spec.ClusterIP = "127.0.0.1";
            Require(await online.GetDestinationAsync(default) is null, "Loopback destinations are rejected.");
            service.Spec.ClusterIP = "10.96.0.10";

            await online.PutAsync(default);
            Require(cluster.Objects.Count == 4, "Reapplying updates instead of duplicating.");
            Require(cluster.Replacements == 4, "Updates carry resource versions.");
            string secondId = Guid.NewGuid().ToString("N");
            var secondManifest = StaticAppManifest.Create(secondId, cluster.Namespace, SmallFiles());
            await cluster.ApplyAsync(secondManifest.Service, default);
            var content = cluster.Objects[("ConfigMap", StaticAppManifest.Name("first"))];
            content.Metadata.Labels[StaticAppManifest.AppLabel] = secondId;
            int writes = cluster.Writes;
            await ThrowsAsync(() => online.PutAsync(default));
            await ThrowsAsync(() => online.RemoveAsync(default));
            Require(cluster.Writes == writes, "Ownership collisions reject the whole operation before mutation.");
            content.Metadata.Labels[StaticAppManifest.AppLabel] = "first";
            await online.RemoveAsync(default);
            Require(cluster.Objects.Count == 1 && cluster.Objects.Values.Single().Metadata.Name == StaticAppManifest.Name(secondId),
                "Removal never touches another app.");
            Require(!(await Create(true).GetAsync(default)).Online, "Removal persists offline intent.");
            await online.RemoveAsync(default);
            Require(cluster.Objects.Count == 1, "Removal is idempotent.");

            await HttpChecksAsync(workspace, artifacts, cluster, available: false);
            await HttpChecksAsync(workspace, artifacts, cluster, available: true);
            Console.WriteLine("PASS static online manifests, exact limits, mode gating, readiness, persistence, ownership, updates, removal and HTTP sandbox");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static async Task ValidationRunnerChecksAsync(string fixtureSource)
    {
        string root = Path.Combine(Path.GetTempPath(), "daydream-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                Path.Combine(root, "fixture", ".git", "daydream-apps", "first"),
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var client = new FakeValidationJobClient { Succeeded = true, Logs = "PASS validation\n" };
            var runner = new KubernetesFixtureValidationRunner(
                client,
                new(
                    "registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"),
                    "daydream-workspace",
                    null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            FixtureValidationResult result = await runner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(result.ExitCode == 0 && result.Output.Contains("PASS validation", StringComparison.Ordinal) &&
                client.Created?.Metadata.NamespaceProperty == "daydream" &&
                client.Created.Spec.Template.Spec.AutomountServiceAccountToken == false &&
                client.Deleted,
                "The Kubernetes runner creates a credential-free job, collects its result, and removes it.");

            var failed = new FakeValidationJobClient { Failed = true, Logs = "FAIL validation\n" };
            var failedRunner = new KubernetesFixtureValidationRunner(
                failed,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "daydream-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            FixtureValidationResult failedResult = await failedRunner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(failedResult.ExitCode != 0 && failedResult.Output.Contains("FAIL validation", StringComparison.Ordinal),
                "Worker test failures remain explicit validation failures.");

            var timedOut = new FakeValidationJobClient { Pending = true };
            var timedOutRunner = new KubernetesFixtureValidationRunner(
                timedOut,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "daydream-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            await ThrowsTypeAsync<TimeoutException>(() =>
                timedOutRunner.RunAsync(workspace, "first", TimeSpan.FromMilliseconds(50), CancellationToken.None));
            Require(timedOut.Deleted, "Timed-out validation jobs are removed.");

            var cancelled = new FakeValidationJobClient { Pending = true };
            var cancelledRunner = new KubernetesFixtureValidationRunner(
                cancelled,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "daydream-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            using var cancellation = new CancellationTokenSource();
            Task<FixtureValidationResult> cancellationTask = cancelledRunner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), cancellation.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            cancellation.Cancel();
            await ThrowsTypeAsync<OperationCanceledException>(() => cancellationTask);
            Require(cancelled.Deleted, "Cancelled validation jobs are removed.");
            Console.WriteLine("PASS isolated validation job lifecycle and result capture");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void ManifestChecks()
    {
        StaticAppManifest manifest = StaticAppManifest.Create("first", "test", SmallFiles());
        Require(manifest.Content.BinaryData.Values.Single().SequenceEqual(Encoding.UTF8.GetBytes("<h1>Hello</h1>")), "Manifest preserves bytes.");
        Require(manifest.Service.Spec.Type == "ClusterIP" && manifest.Service.Spec.Ports.Single().Port == 80, "No public Service or extra port.");
        Require(manifest.Deployment.Spec.Template.Spec.AutomountServiceAccountToken == false, "Untrusted app never receives credentials.");
        V1Container container = manifest.Deployment.Spec.Template.Spec.Containers.Single();
        Require(container.SecurityContext.ReadOnlyRootFilesystem == true &&
            container.SecurityContext.AllowPrivilegeEscalation == false &&
            manifest.Deployment.Spec.Template.Spec.SecurityContext.RunAsNonRoot == true, "Unprivileged read-only static server.");
        Require(manifest.NetworkPolicy.Spec.Egress.Count == 0 &&
            manifest.NetworkPolicy.Spec.Ingress.Single().FromProperty.Single().PodSelector.MatchLabels["daydream.dev/host"] == "true",
            "App network policy allows host ingress only and denies egress.");
        var exact = new StaticAppFiles(new Dictionary<string, byte[]>
        {
            ["index.html"] = new byte[StaticAppManifest.MaxFileBytes],
            ["main.js"] = new byte[StaticAppManifest.MaxFileBytes]
        });
        StaticAppManifest.Validate(exact);
        Throws(() => StaticAppManifest.Validate(new(new Dictionary<string, byte[]>(exact.Files) { ["more.js"] = [1] })));
        Throws(() => StaticAppManifest.Validate(new(new Dictionary<string, byte[]> { ["index.html"] = new byte[StaticAppManifest.MaxFileBytes + 1] })));
        var many = Enumerable.Range(1, StaticAppManifest.MaxFiles - 1).ToDictionary(i => $"{i}.js", _ => Array.Empty<byte>());
        many["index.html"] = [];
        StaticAppManifest.Validate(new(many));
        many["extra.js"] = [];
        Throws(() => StaticAppManifest.Validate(new(many)));
        foreach (string path in new[] { "../secret.html", "/index.html", ".git/config", "a//b.js", "a\\b.js", "%2e%2e/a.js", "a/.private.json" })
            Require(!StaticAppManifest.PublicPath(path), "Reject unsafe public path: " + path);
        Throws(() => StaticAppManifest.Create("../escape", "test", SmallFiles()));
        Require(StaticAppManifest.Name("first") != StaticAppManifest.Name(Guid.NewGuid().ToString("N")), "App names are isolated.");
        var changed = StaticAppManifest.Create("first", "test", new(new Dictionary<string, byte[]> { ["index.html"] = [1] }));
        Require(manifest.Deployment.Spec.Template.Metadata.Annotations["daydream.dev/revision"] !=
            changed.Deployment.Spec.Template.Metadata.Annotations["daydream.dev/revision"], "Content changes roll out the static server.");

        string firstNamespace = AppWorkerManifest.NamespaceName("first");
        AppWorkerManifests workers = AppWorkerManifest.Create(
            "first", "wave-001", "daydream", firstNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "daydream-workspace", "fixture/.git/daydream-apps/first", "fixture/.git/daydream-artifacts/first",
            "registry-pull");
        string secondId = Guid.NewGuid().ToString("N");
        string secondNamespace = AppWorkerManifest.NamespaceName(secondId);
        AppWorkerManifests anotherApp = AppWorkerManifest.Create(
            secondId, "wave-001", "daydream", secondNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "daydream-workspace", $"fixture/.git/daydream-apps/{secondId}", $"fixture/.git/daydream-artifacts/{secondId}");
        AppWorkerManifests nextWave = AppWorkerManifest.Create(
            "first", "wave-002", "daydream", firstNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "daydream-workspace", "fixture/.git/daydream-apps/first", "fixture/.git/daydream-artifacts/first");
        V1PodSpec buildPod = workers.BuildJob.Spec.Template.Spec;
        V1Container buildContainer = buildPod.Containers.Single();
        Require(workers.BuildJob.Metadata.NamespaceProperty == "daydream" &&
            workers.DeployJob.Metadata.NamespaceProperty == "daydream" &&
        workers.DeployRole.Metadata.NamespaceProperty == firstNamespace &&
        workers.DeployRoleBinding.Metadata.NamespaceProperty == firstNamespace &&
            workers.DeployServiceAccount.Metadata.NamespaceProperty == "daydream",
            "Worker jobs and shared artifacts stay in the coordinator namespace while deployment RBAC stays in the app namespace.");
        Require(anotherApp.DeployRole.Metadata.NamespaceProperty == secondNamespace &&
            anotherApp.DeployRoleBinding.Subjects.Single().NamespaceProperty == "daydream" &&
            workers.DeployRoleBinding.Subjects.Single().NamespaceProperty == "daydream" &&
            anotherApp.DeployServiceAccount.Metadata.Name != workers.DeployServiceAccount.Metadata.Name &&
        workers.DeployRoleBinding.Metadata.NamespaceProperty != secondNamespace,
            "One app's deployment identity cannot bind into another app's namespace.");
        Require(workers.BuildJob.Metadata.Name != nextWave.BuildJob.Metadata.Name &&
            workers.DeployJob.Metadata.Name != nextWave.DeployJob.Metadata.Name &&
            workers.DeployRoleBinding.Metadata.Name == nextWave.DeployRoleBinding.Metadata.Name &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().Args.SequenceEqual(
                nextWave.DeployJob.Spec.Template.Spec.Containers.Single().Args),
            "Each wave gets fresh Jobs while every wave upgrades the same app release with the same identity.");
        Require(buildPod.ServiceAccountName == "default" && buildPod.AutomountServiceAccountToken == false &&
            !buildPod.Containers.Single().Env.Any(variable => variable.Name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)),
            "The build job has no Kubernetes API credentials.");
        Require(buildPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.WorkspaceVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == true &&
            buildPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.ArtifactVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == false &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).ReadOnlyProperty == true &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).SubPath ==
                "fixture/.git/daydream-apps/first" &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).SubPath ==
                "fixture/.git/daydream-artifacts/first" &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).ReadOnlyProperty == true &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).SubPath ==
                "fixture/.git/daydream-artifacts/first",
            "Jobs exchange artifacts through isolated per-app subpaths on the persistent coordinator volume.");
        Require(workers.BuildNetworkPolicy.Spec.Egress.Count == 0 &&
            workers.BuildNetworkPolicy.Metadata.NamespaceProperty == "daydream" &&
            workers.BuildNetworkPolicy.Spec.PodSelector.MatchLabels[AppWorkerManifest.StageLabel] == "build" &&
            buildPod.Containers.Single().Name == "worker" &&
            workers.BuildJob.Metadata.Labels[AppWorkerManifest.WorkerRunLabel] == "wave-001",
            "The build job cannot make network egress requests in the coordinator namespace.");
        Require(workers.DeployRole.Rules.Single().Resources.Order().SequenceEqual(
                new[] { "configmaps", "deployments", "networkpolicies", "services" }) &&
            !workers.DeployRole.Rules.Single().Resources.Any(resource => resource is "namespaces" or "secrets" or "roles" or "rolebindings") &&
            workers.DeployRole.Rules.Single().ApiGroups.Order().SequenceEqual(new[] { "", "apps", "networking.k8s.io" }),
            "Deployment permissions exclude secrets, RBAC, and cluster-scoped resources.");
        Require(workers.DeployRoleBinding.Subjects.Single().Name == workers.DeployServiceAccount.Metadata.Name &&
            workers.DeployJob.Spec.Template.Spec.ServiceAccountName == workers.DeployServiceAccount.Metadata.Name &&
            workers.DeployJob.Spec.Template.Spec.AutomountServiceAccountToken == true,
            "Only the distinct deployment job receives an identity bound in its target app namespace.");
        Require(workers.BuildJob.Spec.BackoffLimit == 0 &&
            workers.BuildJob.Spec.ActiveDeadlineSeconds == 1800 &&
            workers.BuildJob.Spec.TtlSecondsAfterFinished == 3600 &&
            workers.DeployJob.Spec.BackoffLimit == 0,
            "Worker jobs are bounded and short-lived.");
        Require(buildPod.ImagePullSecrets.Single().Name == "registry-pull" &&
            !buildPod.Volumes.Any(volume => volume.Secret is not null) &&
            buildContainer.SecurityContext.RunAsNonRoot == true &&
            buildContainer.SecurityContext.AllowPrivilegeEscalation == false &&
            buildContainer.SecurityContext.ReadOnlyRootFilesystem == true,
            "Registry pull credentials stay kubelet-managed and the worker is unprivileged.");
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "daydream", "not/namespace",
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "daydream", firstNamespace,
            "worker@sha256:invalid", "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "daydream", firstNamespace,
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "../other-app", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "daydream", "daydream",
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-00000000000000000", "daydream", firstNamespace,
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));

        V1Job validationJob = AppWorkerManifest.CreateValidationJob(
            "daydream-validate-test",
            "first",
            "daydream",
            "registry.example/worker@sha256:" + new string('a', 64),
            "daydream-workspace",
            "fixture/.git/daydream-apps/first");
        V1PodSpec validationPod = validationJob.Spec.Template.Spec;
        Require(validationPod.AutomountServiceAccountToken == false &&
            validationPod.Containers.Single().Command.SequenceEqual(new[] { "sh" }) &&
            validationPod.Containers.Single().Args.SequenceEqual(new[] { "tests/run.sh" }),
            "Fixture tests execute only in a token-free validation job.");
        Require(validationPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.WorkspaceVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == true &&
            validationPod.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).SubPath ==
            "fixture/.git/daydream-apps/first" &&
            validationPod.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).ReadOnlyProperty == true &&
            validationPod.Containers.Single().SecurityContext.ReadOnlyRootFilesystem == true,
            "Validation receives only its read-only app workspace and bounded temporary storage.");
        ThrowsArgument(() => AppWorkerManifest.CreateValidationJob(
            "daydream-validate-test", "first", "daydream",
            "registry.example/worker@sha256:" + new string('a', 64),
            "daydream-workspace", "../other-app"));
    }

    private static void DeploymentStateChecks(DisposableFixtureWorkspace workspace)
    {
        var store = new AppDeploymentStateStore(workspace, new(AppLibrary.FirstAppId));
        AppDeploymentSnapshot empty = store.GetSnapshot();
        Require(empty.Namespace == AppWorkerManifest.NamespaceName(AppLibrary.FirstAppId) &&
            empty.ReleaseName == StaticAppManifest.Name(AppLibrary.FirstAppId) &&
            empty.Revisions.Count == 0,
            "Deployment history is owned by a deterministic app namespace and release.");

        store.Begin("wave-001");
        ThrowsArgument(() => store.RecordBuilt("wave-001", "sha256:invalid"));
        Require(store.GetSnapshot().Revisions.Single().Phase == AppDeploymentPhase.Building,
            "Invalid artifact digests cannot advance deployment state.");
        Throws(() => store.Begin("wave-002"));
        string digest = "sha256:" + new string('a', 64);
        store.RecordBuilt("wave-001", digest);
        store.BeginDeploy("wave-001");
        store.Complete("wave-001");

        var restarted = new AppDeploymentStateStore(workspace, new(AppLibrary.FirstAppId));
        AppDeploymentSnapshot recovered = restarted.GetSnapshot();
        Require(recovered.Revisions.Single() is
            { RunId: "wave-001", Phase: AppDeploymentPhase.Deployed, ArtifactDigest: var savedDigest } &&
            savedDigest == digest,
            "A web-process replacement recovers the completed app deployment revision.");

        restarted.Begin("wave-002");
        restarted.RecordBuilt("wave-002", "sha256:" + new string('b', 64));
        var nextProcess = new AppDeploymentStateStore(workspace, new(AppLibrary.FirstAppId));
        AppDeploymentSnapshot updated = nextProcess.GetSnapshot();
        Require(updated.Revisions.Count == 2 &&
            updated.Revisions[0].Phase == AppDeploymentPhase.Deployed &&
            updated.Revisions[1] is { RunId: "wave-002", Phase: AppDeploymentPhase.Built } &&
            updated.Namespace == recovered.Namespace && updated.ReleaseName == recovered.ReleaseName,
            "Second-wave history survives restart while retaining app namespace and Helm release ownership.");
        Console.WriteLine("PASS durable per-app deployment history and second-wave recovery");
    }

    private static async Task HttpChecksAsync(DisposableFixtureWorkspace workspace, WorkspaceArtifacts artifacts, FakeCluster cluster, bool available)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(workspace);
        builder.Services.AddSingleton(artifacts);
        builder.Services.AddSingleton(new AppIdentity("first"));
        builder.Services.AddSingleton(new OnlineHostingSettings(available));
        builder.Services.AddSingleton<IOnlineCluster>(cluster);
        builder.Services.AddSingleton<AppOnlineService>();
        builder.Services.AddSingleton<WorkLifecycle>();
        builder.Services.AddSingleton(services => new AppLibrary(services, ServiceDescriptor.Singleton(new object())));
        var handler = new StaticHandler();
        builder.Services.AddHttpClient(OnlineEndpoints.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using WebApplication app = builder.Build();
        app.MapOnlineEndpoints();
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new(address) };
        try
        {
            WorkLifecycle lifecycle = app.Services.GetRequiredService<WorkLifecycle>();
            lifecycle.SetCastingState(SquadLifecycleStates.NeedsCast);
            if (!available)
            {
                int reads = cluster.Reads, writes = cluster.Writes;
                AppOnlineStatus? local = await client.GetFromJsonAsync<AppOnlineStatus>("/workspace/online");
                using HttpResponseMessage localPut = await client.PostAsync("/workspace/online", null);
                using HttpResponseMessage localDelete = await client.DeleteAsync("/workspace/online");
                Require(local is { Available: false, Online: false, Url: null } &&
                    (int)localPut.StatusCode == 422 && (int)localDelete.StatusCode == 422 &&
                    await localPut.Content.ReadAsStringAsync() == AppOnlineService.LocalMessage,
                    "Local HTTP actions fail plainly while status explains why.");
                Require(cluster.Reads == reads && cluster.Writes == writes &&
                    lifecycle.GetSnapshot().State == SquadLifecycleStates.NeedsCast, "Local HTTP gating has no app/API side effects.");
                return;
            }
            WorkLifecycle.WorkLease busy = lifecycle.TryBegin("work")!;
            int before = cluster.Writes;
            using HttpResponseMessage busyPut = await client.PostAsync("/workspace/online", null);
            using HttpResponseMessage busyDelete = await client.DeleteAsync("/workspace/online");
            Require(busyPut.StatusCode == HttpStatusCode.Conflict && busyDelete.StatusCode == HttpStatusCode.Conflict &&
                cluster.Writes == before, "Server admission blocks online changes during app work.");
            busy.Complete(WorkTerminalResult.Completed, "Done");
            lifecycle.SetCastingState(SquadLifecycleStates.NeedsCast);
            using var opaqueOrigin = new HttpRequestMessage(HttpMethod.Post, "/workspace/online");
            opaqueOrigin.Headers.Add("Origin", "null");
            using HttpResponseMessage forbidden = await client.SendAsync(opaqueOrigin);
            Require(forbidden.StatusCode == HttpStatusCode.Forbidden && cluster.Writes == before,
                "Sandboxed apps cannot change online state through cross-origin form requests.");
            using HttpResponseMessage put = await client.PostAsync("/workspace/online", null);
            Require(put.IsSuccessStatusCode && (await put.Content.ReadFromJsonAsync<AppOnlineStatus>())?.Online == false, "HTTP submission returns pending.");
            Require(lifecycle.GetSnapshot().State == SquadLifecycleStates.NeedsCast,
                "Online actions preserve casting state instead of claiming the app is cast.");
            cluster.Ready("first");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/online/first/index.html");
            request.Headers.Add("Authorization", "Bearer never-forward");
            request.Headers.Add("Cookie", "session=never-forward");
            using HttpResponseMessage response = await client.SendAsync(request);
            Require(await response.Content.ReadAsStringAsync() == "<h1>Online</h1>", "Proxy returns public app bytes.");
            Require(response.Headers.GetValues("Content-Security-Policy").Single() == OnlineEndpoints.SandboxPolicy &&
                !OnlineEndpoints.SandboxPolicy.Contains("allow-same-origin") &&
                response.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "Top-level apps are sandboxed.");
            Require(!response.Headers.Contains("Set-Cookie") && !handler.ForwardedCredential && handler.LastUri?.Host == "10.96.0.10",
                "Neither direction forwards credentials or upstream cookies.");
            int requests = handler.Requests;
            using HttpResponseMessage unsafePath = await client.GetAsync("/online/first/a%252fb.js");
            Require(unsafePath.StatusCode == HttpStatusCode.NotFound && handler.Requests == requests, "Encoded path escape never reaches upstream.");
            handler.Status = HttpStatusCode.Found;
            using HttpResponseMessage redirect = await client.GetAsync("/online/first/");
            Require(redirect.StatusCode == HttpStatusCode.BadGateway && !redirect.Headers.Contains("Location"), "Upstream redirects are never exposed.");
            handler.Status = HttpStatusCode.OK;
            handler.Oversized = true;
            using HttpResponseMessage oversized = await client.GetAsync("/online/first/");
            Require(oversized.StatusCode == HttpStatusCode.ServiceUnavailable, "Oversized upstream responses fail closed.");
            cluster.ReadFailure = new HttpRequestException("private upstream error");
            using HttpResponseMessage apiFailure = await client.GetAsync("/workspace/online");
            Require(apiFailure.StatusCode == HttpStatusCode.ServiceUnavailable &&
                await apiFailure.Content.ReadAsStringAsync() == "Your app could not be reached. Please try again.",
                "API failures return a plain non-success response without leaking upstream details.");
            cluster.ReadFailure = null;
        }
        finally { await app.StopAsync(); }
    }

    private static StaticAppFiles SmallFiles() => new(new Dictionary<string, byte[]> { ["index.html"] = Encoding.UTF8.GetBytes("<h1>Hello</h1>") });
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected rejection.");
    }
    private static void ThrowsArgument(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected argument rejection.");
    }
    private static async Task ThrowsAsync(Func<Task> action)
    {
        try { await action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected rejection.");
    }
    private static async Task ThrowsTypeAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); } catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        public int Requests;
        public Uri? LastUri;
        public bool ForwardedCredential;
        public bool Oversized;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastUri = request.RequestUri;
            ForwardedCredential |= request.Headers.Contains("Authorization") || request.Headers.Contains("Cookie");
            var response = new HttpResponseMessage(Status)
            {
                Content = new StringContent(Oversized ? new string('x', StaticAppManifest.MaxFileBytes + 1) : "<h1>Online</h1>")
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "upstream=not-allowed");
            response.Headers.Location = new("http://127.0.0.1/private");
            return Task.FromResult(response);
        }
    }

    private sealed class FakeCluster : IOnlineCluster
    {
        public string Namespace => "tests";
        public Dictionary<(string Kind, string Name), IKubernetesObject<V1ObjectMeta>> Objects { get; } = [];
        public int Reads, Writes, Replacements;
        public Exception? ReadFailure;
        public Task<IKubernetesObject<V1ObjectMeta>?> ReadAsync(string kind, string name, CancellationToken cancellationToken)
        {
            Reads++;
            if (ReadFailure is not null) return Task.FromException<IKubernetesObject<V1ObjectMeta>?>(ReadFailure);
            return Task.FromResult(Objects.GetValueOrDefault((kind, name)));
        }
        public Task ApplyAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken)
        {
            Writes++;
            if (resource.Metadata.ResourceVersion is not null) Replacements++;
            resource.Metadata.ResourceVersion = Writes.ToString();
            resource.Metadata.Uid = resource.Metadata.Name;
            resource.Metadata.Generation = Writes;
            if (resource is V1Service service) service.Spec.ClusterIP ??= "10.96.0.10";
            Objects[(resource.Kind, resource.Metadata.Name)] = resource;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken)
        {
            Writes++;
            Objects.Remove((resource.Kind, resource.Metadata.Name));
            return Task.CompletedTask;
        }
        public void Ready(string id)
        {
            var deployment = (V1Deployment)Objects[("Deployment", StaticAppManifest.Name(id))];
            deployment.Status = new() { AvailableReplicas = 1, UpdatedReplicas = 1, Replicas = 1,
                ObservedGeneration = deployment.Metadata.Generation };
        }
    }

    private sealed class FakeValidationJobClient : IFixtureValidationJobClient
    {
        public string Namespace => "daydream";
        public V1Job? Created { get; private set; }
        public bool Succeeded { get; init; }
        public bool Failed { get; init; }
        public bool Pending { get; init; }
        public bool Deleted { get; private set; }
        public string Logs { get; init; } = "";

        public Task CreateAsync(V1Job job, CancellationToken cancellationToken)
        {
            Created = job;
            return Task.CompletedTask;
        }

        public async Task<V1Job?> ReadAsync(string name, CancellationToken cancellationToken)
        {
            if (Pending) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new V1Job
            {
                Status = new V1JobStatus { Succeeded = Succeeded ? 1 : 0, Failed = Failed ? 1 : 0 }
            };
        }

        public Task<string> ReadLogsAsync(string jobName, CancellationToken cancellationToken) =>
            Task.FromResult(Logs);

        public Task DeleteAsync(string name, CancellationToken cancellationToken)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }
}
