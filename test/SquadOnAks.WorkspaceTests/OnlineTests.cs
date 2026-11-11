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
using SquadOnAks.NamespaceProvisioner;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class OnlineTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        ManifestChecks();
        NamespaceProvisionerChecks();
        await ValidationRunnerChecksAsync(fixtureSource);
        string root = Path.Combine(Path.GetTempPath(), "soaks-online-" + Guid.NewGuid().ToString("N"));
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
            var readinessProbe = new FakeReadinessProbe();
            AppOnlineService Create(bool available, string id = "first")
            {
                var identity = new AppIdentity(id);
                var deployments = new AppDeploymentStateStore(workspace, identity);
                var jobs = new FakeAppDeploymentJobClient(workspace.StatePath(Path.Combine("soaks-artifacts", id)));
                var settings = new AppDeploymentSettings(
                    "soaks",
                    "registry.example/worker@sha256:" + new string('a', 64),
                    Path.GetDirectoryName(root)!,
                    "soaks-workspace",
                    null);
                var runner = new KubernetesAppDeploymentRunner(
                    identity,
                    workspace,
                    deployments,
                    settings,
                    jobs,
                    new FakeAppNamespaceProvisioner(),
                    NullLogger<KubernetesAppDeploymentRunner>.Instance);
                return new(identity, new(available), cluster, workspace, artifacts, deployments, runner,
                    readinessProbe, NullLogger<AppOnlineService>.Instance);
            }
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
            Require(JsonSerializer.Deserialize<AppOnlineState>(File.ReadAllText(workspace.StatePath("soaks-online.json")))?.Online == true,
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
            await AspireReadinessChecksAsync(Path.Combine(Path.GetDirectoryName(fixtureSource)!, DisposableFixtureWorkspace.StarterTemplateName));
            await DeploymentRunnerChecksAsync(fixtureSource);
            Console.WriteLine("PASS static online manifests, exact limits, mode gating, readiness, persistence, ownership, updates, removal and HTTP sandbox");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    /// <summary>Writes the contract a generated Aspire app (front end + API + PostgreSQL) commits.</summary>
    private static void WriteAspireContract(DisposableFixtureWorkspace workspace)
    {
        File.WriteAllText(Path.Combine(workspace.RootPath, AppBuildManifest.FileName),
            AppBuildManifest.Example("aspire", "dotnet", "postgres"));
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "tests"));
        File.WriteAllText(Path.Combine(workspace.RootPath, "tests", "serve.sh"), "#!/bin/sh\n");
    }

    private static async Task AspireReadinessChecksAsync(string fixtureSource)
    {
        string root = Path.Combine(Path.GetTempPath(), "squad-aspire-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                root,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            WriteAspireContract(workspace);
            var artifacts = new WorkspaceArtifacts(workspace);
            Require(artifacts.IsAspireApp(), "A committed aspire contract marks the app as a deployable Aspire app.");
            var cluster = new FakeCluster();
            var readinessProbe = new FakeReadinessProbe();
            var identity = new AppIdentity(AppLibrary.FirstAppId);
            var deployments = new AppDeploymentStateStore(workspace, identity);
            var jobs = new FakeAppDeploymentJobClient(
                workspace.StatePath(Path.Combine("soaks-artifacts", identity.Id)));
            var settings = new AppDeploymentSettings(
                "soaks",
                "registry.example/worker@sha256:" + new string('a', 64),
                Path.GetDirectoryName(root)!,
                "soaks-workspace",
                null);
            var runner = new KubernetesAppDeploymentRunner(
                identity,
                workspace,
                deployments,
                settings,
                jobs,
                new FakeAppNamespaceProvisioner(),
                NullLogger<KubernetesAppDeploymentRunner>.Instance);
            readinessProbe.IsReady = false;
            var online = new AppOnlineService(
                identity,
                new(true),
                cluster,
                workspace,
                artifacts,
                deployments,
                runner,
                readinessProbe,
                NullLogger<AppOnlineService>.Instance);

            Require(!(await online.PutAsync(default)).Online && readinessProbe.Calls == 1,
                "An Aspire app stays unavailable while its deployed front end or health check fails.");
            Require(deployments.GetSnapshot().Revisions.Single().Phase == AppDeploymentPhase.Deployed &&
                jobs.Stages.SequenceEqual(["build", "check", "journeys", "deploy"]) && cluster.Writes == 0 &&
                readinessProbe.LastOrigin is { Host: var readinessHost, Port: 8080 } &&
                readinessHost == $"api-service.{AppWorkerManifest.NamespaceName(identity.Id)}.svc.cluster.local" &&
                readinessProbe.LastHealthPaths?.SequenceEqual(["/health/ready"]) == true,
                "Going online checks the journeys on a throwaway copy, deploys the app-owned Aspire release into its namespace and probes its declared health paths.");

            readinessProbe.IsReady = true;
            Require((await online.GetAsync(default)).Online && readinessProbe.Calls == 2,
                "An Aspire app becomes available only after its front end and health check succeed.");
            Require((await online.PutAsync(default)).Online &&
                deployments.GetSnapshot().Revisions.Count == 2 &&
                jobs.Stages.SequenceEqual(["build", "check", "journeys", "deploy", "build", "check", "journeys", "deploy"]),
                "An update after deployment rebuilds the workspace into the same owned release.");
            jobs.HoldBuild = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<AppOnlineStatus> updating = online.PutAsync(default);
            AppOnlineStatus during = await online.GetAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
            Require(during.Online && !updating.IsCompleted,
                "Status reads answer at once while an update deploys, and the running revision stays online.");
            jobs.HoldBuild.SetResult();
            jobs.HoldBuild = null;
            Require((await updating).Online, "The update finishes online.");
            Require((await online.GetDestinationAsync(default))?.Host == readinessProbe.LastOrigin?.Host,
                "Opening the app proxies the same Aspire API Service used by readiness.");
            await ThrowsTypeAsync<InvalidOperationException>(() => online.RemoveAsync(default));

            File.WriteAllText(Path.Combine(root, AppBuildManifest.FileName),
                AppBuildManifest.Example("server", "dotnet", "postgres"));
            await ThrowsTypeAsync<InvalidOperationException>(() => online.PutAsync(default));

            await DeployedReadinessProbeChecksAsync();
            await AspireProxyChecksAsync(fixtureSource);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task DeployedReadinessProbeChecksAsync()
    {
        using var handler = new ReadinessHandler();
        using var clients = new ReadinessClientFactory(handler);
        var probe = new DeployedAppReadinessProbe(clients);
        var origin = new Uri("http://10.96.0.10/");
        string[] paths = ["/health/ready", "/health/db"];

        Require(await probe.IsReadyAsync(origin, paths, default), "An HTML front end and passing health paths pass deployed readiness.");
        Require(handler.Paths.SequenceEqual(["/", "/health/ready", "/health/db"]), "Readiness checks the front end and every declared health path.");

        handler.FrontendStatus = HttpStatusCode.ServiceUnavailable;
        Require(!await probe.IsReadyAsync(origin, paths, default), "An unavailable front end fails deployed readiness.");
        handler.FrontendStatus = HttpStatusCode.OK;

        handler.FrontendContentType = "application/json";
        Require(!await probe.IsReadyAsync(origin, paths, default), "A non-HTML front end fails deployed readiness.");
        handler.FrontendContentType = "text/html";

        handler.HealthStatus = HttpStatusCode.ServiceUnavailable;
        Require(!await probe.IsReadyAsync(origin, paths, default), "A failing health path (for example, an unreachable database) fails deployed readiness.");
        handler.HealthStatus = HttpStatusCode.OK;

        handler.Failure = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        Require(!await probe.IsReadyAsync(origin, paths, default), "A just-deployed pod that times out is not ready yet, not an error.");
        handler.Failure = new HttpRequestException("Connection refused");
        Require(!await probe.IsReadyAsync(origin, paths, default), "A refused connection is not ready yet, not an error.");
        handler.Failure = null;
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            handler.Failure = new TaskCanceledException("cancelled", null, cancelled.Token);
            await ThrowsTypeAsync<OperationCanceledException>(() => probe.IsReadyAsync(origin, paths, cancelled.Token));
            handler.Failure = null;
        }

        Require(!await probe.IsReadyAsync(origin, [], default), "An app without declared health paths is never ready.");
        Require(!await probe.IsReadyAsync(origin, ["//elsewhere"], default), "Health paths cannot leave the app origin.");
    }

    private static async Task DeploymentRunnerChecksAsync(string fixtureSource)
    {
        string parent = Path.Combine(Path.GetTempPath(), "soaks-deployment-runner-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(parent, "fixture");
        try
        {
            var workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                root,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var identity = new AppIdentity(AppLibrary.FirstAppId);
            var state = new AppDeploymentStateStore(workspace, identity);
            var jobs = new FakeAppDeploymentJobClient(workspace.StatePath("soaks-artifacts/first"))
            {
                CancelNextDeploy = true
            };
            var settings = new AppDeploymentSettings(
                "soaks",
                "registry.example/worker@sha256:" + new string('a', 64),
                parent,
                "soaks-workspace",
                null);
            var runner = new KubernetesAppDeploymentRunner(
                identity,
                workspace,
                state,
                settings,
                jobs,
                new FakeAppNamespaceProvisioner(),
                NullLogger<KubernetesAppDeploymentRunner>.Instance);

            try
            {
                await runner.RunAsync(default);
                throw new InvalidOperationException("Expected deployment cancellation.");
            }
            catch (OperationCanceledException)
            {
            }

            Require(state.GetSnapshot().Revisions.Single().Phase == AppDeploymentPhase.Deploying &&
                jobs.Stages.SequenceEqual(["build", "deploy"]),
                "An interrupted deployment keeps its durable phase and does not report success.");

            var recovered = new KubernetesAppDeploymentRunner(
                identity,
                workspace,
                new AppDeploymentStateStore(workspace, identity),
                settings,
                jobs,
                new FakeAppNamespaceProvisioner(),
                NullLogger<KubernetesAppDeploymentRunner>.Instance);
            AppDeploymentSnapshot first = await recovered.RunAsync(default);
            Require(first.Revisions.Single().Phase == AppDeploymentPhase.Deployed &&
                jobs.Stages.SequenceEqual(["build", "deploy", "deploy"]),
                "A replacement runner resumes the same release from its persisted deploy phase.");

            AppDeploymentSnapshot second = await recovered.RunAsync(default);
            Require(second.Revisions.Count == 2 &&
                second.Revisions.All(revision => revision.Phase == AppDeploymentPhase.Deployed) &&
                second.Revisions[0].RunId != second.Revisions[1].RunId &&
                second.Namespace == first.Namespace && second.ReleaseName == first.ReleaseName &&
                jobs.Stages.SequenceEqual(["build", "deploy", "deploy", "build", "deploy"]),
                "A second wave creates fresh worker jobs while preserving app namespace and release ownership.");

            jobs.FailNextDeploy = true;
            try
            {
                await recovered.RunAsync(default);
                throw new InvalidOperationException("Expected worker failure.");
            }
            catch (AppDeploymentJobFailedException)
            {
            }

            AppDeploymentSnapshot failed = state.GetSnapshot();
            Require(failed.Revisions[^1] is { Phase: AppDeploymentPhase.Failed, Reason: AppDeploymentFailure.NotReady },
                "A confirmed failed worker is persisted as a failed deployment revision with its reason code.");
            Require(AppDeploymentFailure.Classify("build", "x\nBlocked: the app is missing frontend/package-lock.json (AppHost + Api + frontend Aspire layout).\n") == "missing:frontend/package-lock.json" &&
                AppDeploymentFailure.Classify("build", "error NU1101: Unable to find package Foo.Bar") == AppDeploymentFailure.PackagesUnavailable &&
                AppDeploymentFailure.Classify("build", "npm error code ENOTCACHED") == AppDeploymentFailure.PackagesUnavailable &&
                AppDeploymentFailure.Classify("build", "Program.cs(3,1): error CS1002: ; expected") == AppDeploymentFailure.BuildFailed &&
                AppDeploymentFailure.Classify("deploy", "Error: UPGRADE FAILED: context deadline exceeded") == AppDeploymentFailure.NotReady &&
                AppDeploymentFailure.Classify("build", "something unexpected secret=abc") is null &&
                AppDeploymentFailure.Classify("build", "Blocked: the app is missing ../../etc/passwd (x)") is null,
                "Worker logs map only to fixed reason codes; unknown output yields no reason.");
            Require(AppDeploymentFailure.Message("missing:frontend/package-lock.json")!.Contains("frontend/package-lock.json", StringComparison.Ordinal) &&
                AppDeploymentFailure.Message("missing:../../etc/passwd") is null && AppDeploymentFailure.Message(null) is null &&
                !new AppDeploymentStateStore(workspace, identity).GetSnapshot().Revisions.Any(revision =>
                    revision.Reason is { } reason && !AppDeploymentFailure.IsKnown(reason)),
                "Only known reason codes become user-facing next steps.");
            AppDeploymentSnapshot retried = await recovered.RunAsync(default);
            Require(retried.Revisions.Count == 4 &&
                retried.Revisions[^1].Phase == AppDeploymentPhase.Deployed &&
                retried.Revisions[^1].RunId != failed.Revisions[^1].RunId &&
                jobs.Stages.SequenceEqual([
                    "build", "deploy", "deploy", "build", "deploy", "build", "deploy", "build", "deploy"
                ]),
                "A new deployment wave can proceed after a prior worker has definitively failed.");

            // An app that declares journeys proves them on a throwaway copy before the real release changes.
            File.WriteAllText(Path.Combine(root, AppBuildManifest.FileName), AppBuildManifest.Example("aspire", "dotnet", "postgres"));
            var namespaces = new FakeAppNamespaceProvisioner();
            var checkedRunner = new KubernetesAppDeploymentRunner(identity, workspace, state, settings, jobs, namespaces,
                NullLogger<KubernetesAppDeploymentRunner>.Instance);
            jobs.Stages.Clear();
            jobs.Runs.Clear();
            AppDeploymentSnapshot checkedSnapshot = await checkedRunner.RunAsync(default);
            Require(checkedSnapshot.Revisions[^1].Phase == AppDeploymentPhase.Deployed &&
                jobs.Stages.SequenceEqual(["build", "check", "journeys", "deploy"]) &&
                namespaces.ChecksEnsured.Count == 1 && namespaces.ChecksDeleted.SequenceEqual(namespaces.ChecksEnsured) &&
                namespaces.AppIds.Count == 1,
                "A revision with journeys is deployed to a throwaway namespace and checked before the real release, and the copy is removed.");
            string checkNamespace = AppWorkerManifest.CheckNamespaceName(namespaces.ChecksEnsured[0]);
            V1Job checkDeploy = jobs.Runs[1].Job;
            (V1Job journeyJob, V1NetworkPolicy? journeyPolicy) = jobs.Runs[2];
            V1PodSpec journeyPod = journeyJob.Spec.Template.Spec;
            V1Container journeyContainer = journeyPod.Containers.Single();
            string Env(string name) => journeyContainer.Env.Single(item => item.Name == name).Value;
            Require(checkDeploy.Spec.Template.Spec.Containers.Single().Args.SequenceEqual(
                    ["upgrade", "--install", StaticAppManifest.Name("first"), "/artifacts/package.tgz", "--namespace", checkNamespace,
                        "--wait", "--atomic", "--timeout", "10m"]) &&
                jobs.Runs[3].Job.Spec.Template.Spec.Containers.Single().Args[5] == AppWorkerManifest.NamespaceName("first"),
                "The check deploys the same package into the check namespace; only the real deploy targets the app namespace.");
            Require(journeyJob.Metadata.NamespaceProperty == "soaks" &&
                journeyPod.AutomountServiceAccountToken == false && journeyPod.ServiceAccountName == "default" &&
                journeyPod.Volumes.All(volume => volume.PersistentVolumeClaim is null && volume.Secret is null) &&
                journeyContainer.SecurityContext is { RunAsNonRoot: true, ReadOnlyRootFilesystem: true, AllowPrivilegeEscalation: false } &&
                journeyContainer.Env.All(item => item.ValueFrom is null) &&
                !journeyContainer.Env.Any(item => item.Name.Contains("TOKEN", StringComparison.Ordinal)) &&
                Env("SOAKS_CHECK_ORIGIN") == $"http://api-service.{checkNamespace}.svc.cluster.local:8080/" &&
                Env("SOAKS_CHECK_JOURNEYS").Contains("Acceptance proof", StringComparison.Ordinal) &&
                Env("SOAKS_CHECK_JOURNEYS").Contains("restartJourneys", StringComparison.Ordinal),
                "The journey runner gets only the journeys and the check copy's address: no token, workspace, secrets or credentials.");
            Require(journeyPolicy is { Spec.PolicyTypes: var types, Spec.Egress: { Count: 2 } egress } && types.SequenceEqual(["Egress"]) &&
                egress[0].To.Single().NamespaceSelector.MatchLabels["kubernetes.io/metadata.name"] == checkNamespace &&
                egress[0].Ports.Single().Port.Value == "8080" &&
                egress[1].Ports.All(port => port.Port.Value == "53") &&
                journeyPolicy.Spec.PodSelector.MatchLabels[AppWorkerManifest.StageLabel] == AppWorkerManifest.JourneyStage,
                "The journey runner can reach only the check copy's API port and cluster DNS.");

            jobs.FailNextJourneys = "Browser journey 1, step 3 (text) failed.\n- POST /api/tools answered 404";
            jobs.Stages.Clear();
            try
            {
                await checkedRunner.RunAsync(default);
                throw new InvalidOperationException("Expected a journey failure.");
            }
            catch (AppDeploymentJobFailedException)
            {
            }

            AppDeploymentSnapshot journeyFailed = state.GetSnapshot();
            var redactor = new SquadOnAks.Web.Security.SecretRedactor("unused-secret");
            Require(journeyFailed.Revisions[^1] is { Phase: AppDeploymentPhase.Failed, Reason: AppDeploymentFailure.JourneysFailed } &&
                journeyFailed.Revisions[^2].Phase == AppDeploymentPhase.Deployed &&
                jobs.Stages.SequenceEqual(["build", "check", "journeys"]) &&
                namespaces.ChecksDeleted.Count == 2 && namespaces.ChecksEnsured[1] != namespaces.ChecksEnsured[0] &&
                state.LatestCheckFailure() is { } recorded && recorded.Contains("POST /api/tools answered 404", StringComparison.Ordinal) &&
                AppDeploymentFailure.Message(AppDeploymentFailure.JourneysFailed) is { } next && !next.Contains("404", StringComparison.Ordinal) &&
                SquadOnAks.Web.Work.WorkLifecycleAgent.PreviousCheckFailure(state, redactor) is { } context &&
                context.Contains("POST /api/tools answered 404", StringComparison.Ordinal),
                "Failed journeys never reach the real release; the running revision stays, the copy is removed, " +
                "people get a fixed next step and only the team's next wave sees what the browser found.");

            AppDeploymentSnapshot fixedSnapshot = await checkedRunner.RunAsync(default);
            Require(fixedSnapshot.Revisions[^1].Phase == AppDeploymentPhase.Deployed && state.LatestCheckFailure() is null &&
                SquadOnAks.Web.Work.WorkLifecycleAgent.PreviousCheckFailure(state, redactor) is null,
                "Once a revision passes, the earlier check findings are no longer passed along.");

            string runnerLog = "starting\n" + AppDeploymentFailure.JourneyFailureMarker +
                "{\"message\":\"Browser journey 1, step 4 (click) failed.\",\"details\":[\"POST /api/tools answered 404\"]}\n";
            Require(AppDeploymentFailure.Classify(AppWorkerManifest.JourneyStage, runnerLog) == AppDeploymentFailure.JourneysFailed &&
                AppDeploymentFailure.JourneyDetail(runnerLog) == "Browser journey 1, step 4 (click) failed.\n- POST /api/tools answered 404" &&
                AppDeploymentFailure.Classify(AppWorkerManifest.JourneyStage, "SOAKS-CHECK-ERROR: PlaywrightException") is null &&
                AppDeploymentFailure.JourneyDetail(AppDeploymentFailure.JourneyFailureMarker + "not json") is null &&
                AppDeploymentFailure.Classify(AppWorkerManifest.CheckStage, "Error: INSTALLATION FAILED: context deadline exceeded") == AppDeploymentFailure.NotReady,
                "Only the runner's own result line becomes a journey failure; a browser that could not start is not the app's fault.");
            Console.WriteLine("PASS deployment jobs, interrupted-run recovery, failure recording, second-wave release ownership and journey checks");
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private static void NamespaceProvisionerChecks()
    {
        AppNamespaceResources first = AppNamespaceManifest.Create("first", "soaks");
        string secondId = Guid.NewGuid().ToString("N");
        AppNamespaceResources second = AppNamespaceManifest.Create(secondId, "soaks");
        Require(first.Namespace.Metadata.Name == AppWorkerManifest.NamespaceName("first") &&
            second.Namespace.Metadata.Name == AppWorkerManifest.NamespaceName(secondId) &&
            first.Namespace.Metadata.Labels[AppNamespaceManifest.AppLabel] == "first",
            "The trusted provisioner derives separately owned app namespaces from validated app IDs.");
        Require(first.Namespace.Metadata.Labels["pod-security.kubernetes.io/enforce"] == "restricted",
            "Generated app namespaces must enforce restricted workload admission.");
        Require(first.DefaultServiceAccount.AutomountServiceAccountToken == false &&
            first.DefaultServiceAccount.Metadata.NamespaceProperty == first.Namespace.Metadata.Name &&
            first.DefaultServiceAccount.Metadata.Labels[AppNamespaceManifest.OwnerLabel] == AppNamespaceManifest.Owner &&
            first.DefaultServiceAccount.Metadata.Labels[AppNamespaceManifest.AppLabel] == "first",
            "Generated app pods receive no automatically mounted Kubernetes service-account token.");
        Require(first.DeployerRole.Rules.First() is
        { ApiGroups: var groups, Resources: var resources, Verbs: var verbs } &&
            groups.Order().SequenceEqual(new[] { "", "apps" }) &&
            resources.Order().SequenceEqual(new[] { "configmaps", "deployments", "persistentvolumeclaims", "secrets", "services", "statefulsets" }) &&
            verbs.Order().SequenceEqual(new[] { "create", "delete", "get", "list", "patch", "update", "watch" }) &&
            first.DeployerRole.Metadata.Name == AppNamespaceManifest.DeployerRoleName,
            "App deployment identity can manage only this namespace's API, database and their config, secrets and storage.");
        Require(first.DeployerRole.Rules.Count == 2 &&
            first.DeployerRole.Rules[1].Resources.Order().SequenceEqual(new[] { "pods", "replicasets" }) &&
            first.DeployerRole.Rules[1].Verbs.Order().SequenceEqual(new[] { "get", "list", "watch" }),
            "Helm readiness may observe pods and ReplicaSets but may not modify them directly.");
        Require(first.DeployerRoleBinding.Subjects.Single() is
        { Kind: "ServiceAccount", Name: "app-deployer-first", NamespaceProperty: "soaks" } &&
            first.DeployerRoleBinding.RoleRef.Name == AppNamespaceManifest.DeployerRoleName &&
            second.DeployerRoleBinding.Subjects.Single().Name == $"app-deployer-{secondId}",
            "Each app namespace binds a distinct coordinator-namespace deploy identity.");
        Require(first.ObserverRole.Metadata is { Name: AppNamespaceManifest.ObserverRoleName, NamespaceProperty: "soaks-app-first" } &&
            first.ObserverRole.Rules.SelectMany(rule => rule.Verbs).Distinct().Order().SequenceEqual(new[] { "get", "list", "watch" }) &&
            first.ObserverRole.Rules.SelectMany(rule => rule.Resources).Order()
                .SequenceEqual(new[] { "deployments", "pods", "statefulsets" }),
            "Squad's live-status role reads only workloads and pods: no logs, secrets, config or writes.");
        Require(first.ObserverRoleBinding.Subjects.Single() is
        { Kind: "ServiceAccount", Name: AppNamespaceManifest.HostServiceAccount, NamespaceProperty: "soaks" } &&
            first.ObserverRoleBinding.RoleRef.Name == AppNamespaceManifest.ObserverRoleName &&
            first.ObserverRoleBinding.Metadata.NamespaceProperty == "soaks-app-first",
            "Only the Squad host account is granted live-status access, and only inside the app's namespace.");
        V1NetworkPolicy boundary = first.Boundary;
        IList<V1NetworkPolicyPeer> callers = boundary.Spec.Ingress.Single().FromProperty;
        V1NetworkPolicyPeer allowedCaller = callers[0];
        Require(boundary.Spec.PolicyTypes.Order().SequenceEqual(new[] { "Egress", "Ingress" }) &&
            callers.Count == 2 &&
            allowedCaller.NamespaceSelector.MatchLabels["kubernetes.io/metadata.name"] == "soaks" &&
            allowedCaller.PodSelector.MatchLabels["soaks.dev/host"] == "true" &&
            callers[1] is { NamespaceSelector: null, PodSelector.MatchLabels: null or { Count: 0 } },
            "App traffic accepts ingress only from Squad host pods and the app's own pods.");
        Require(boundary.Spec.Egress.Count == 2 &&
            boundary.Spec.Egress[0].To.Single() is { NamespaceSelector: null, PodSelector.MatchLabels: null or { Count: 0 } } &&
            boundary.Spec.Egress[0].Ports is null or { Count: 0 } &&
            boundary.Spec.Egress[1].To.Single().PodSelector.MatchLabels["k8s-app"] == "kube-dns" &&
            boundary.Spec.Egress[1].Ports.All(port => port.Port.Value == "53"),
            "App egress reaches only its own namespace (API to database) and cluster DNS.");
        ThrowsArgument(() => AppNamespaceManifest.Create("../first", "soaks"));
        ThrowsArgument(() => AppNamespaceManifest.Create("first", "Not_A_Namespace"));
    }

    private static async Task ValidationRunnerChecksAsync(string fixtureSource)
    {
        string root = Path.Combine(Path.GetTempPath(), "soaks-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                Path.Combine(root, "fixture", ".git", "soaks-apps", "first"),
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var client = new FakeValidationJobClient { Succeeded = true, Logs = "PASS validation\n" };
            var runner = new KubernetesFixtureValidationRunner(
                client,
                new(
                    "registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"),
                    "soaks-workspace",
                    null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            FixtureValidationResult result = await runner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(result.ExitCode == 0 && result.Output.Contains("PASS validation", StringComparison.Ordinal) &&
                client.Created?.Metadata.NamespaceProperty == "soaks" &&
                client.Created.Spec.Template.Spec.AutomountServiceAccountToken == false &&
                client.Deleted,
                "The Kubernetes runner creates a credential-free job, collects its result, and removes it.");

            var failed = new FakeValidationJobClient { Failed = true, Logs = "FAIL validation\n" };
            var failedRunner = new KubernetesFixtureValidationRunner(
                failed,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "soaks-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            FixtureValidationResult failedResult = await failedRunner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(failedResult.ExitCode != 0 && failedResult.Output.Contains("FAIL validation", StringComparison.Ordinal),
                "Worker test failures remain explicit validation failures.");

            var timedOut = new FakeValidationJobClient { Pending = true };
            var timedOutRunner = new KubernetesFixtureValidationRunner(
                timedOut,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "soaks-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            await ThrowsTypeAsync<TimeoutException>(() =>
                timedOutRunner.RunAsync(workspace, "first", TimeSpan.FromMilliseconds(50), CancellationToken.None));
            Require(timedOut.Deleted, "Timed-out validation jobs are removed.");

            var cancelled = new FakeValidationJobClient { Pending = true };
            var cancelledRunner = new KubernetesFixtureValidationRunner(
                cancelled,
                new("registry.example/worker@sha256:" + new string('a', 64),
                    Path.Combine(root, "fixture"), "soaks-workspace", null),
                NullLogger<KubernetesFixtureValidationRunner>.Instance);
            using var cancellation = new CancellationTokenSource();
            Task<FixtureValidationResult> cancellationTask = cancelledRunner.RunAsync(
                workspace, "first", TimeSpan.FromSeconds(5), cancellation.Token);
            // Cancel once the job exists and is being watched, not after a fixed delay that can beat its creation.
            await cancelled.Watching.Task.WaitAsync(TimeSpan.FromSeconds(5));
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

    private static async Task AspireProxyChecksAsync(string fixtureSource)
    {
        string parent = Path.Combine(Path.GetTempPath(), "squad-aspire-proxy-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(parent, "fixture");
        try
        {
            var workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                root,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            WriteAspireContract(workspace);
            var artifacts = new WorkspaceArtifacts(workspace);
            var identity = new AppIdentity(AppLibrary.FirstAppId);
            var deployments = new AppDeploymentStateStore(workspace, identity);
            var jobs = new FakeAppDeploymentJobClient(
                workspace.StatePath(Path.Combine("soaks-artifacts", identity.Id)));
            var settings = new AppDeploymentSettings(
                "soaks",
                "registry.example/worker@sha256:" + new string('a', 64),
                parent,
                "soaks-workspace",
                null);
            var cluster = new FakeCluster();
            var handler = new StaticHandler();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(workspace);
            builder.Services.AddSingleton(artifacts);
            builder.Services.AddSingleton(identity);
            builder.Services.AddSingleton(new OnlineHostingSettings(true));
            builder.Services.AddSingleton<IOnlineCluster>(cluster);
            builder.Services.AddSingleton<IDeployedAppReadinessProbe, FakeReadinessProbe>();
            builder.Services.AddSingleton(deployments);
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton<IAppDeploymentJobClient>(jobs);
            builder.Services.AddSingleton<SquadOnAks.Web.Workspace.IAppNamespaceProvisioner>(
                new FakeAppNamespaceProvisioner());
            builder.Services.AddSingleton<KubernetesAppDeploymentRunner>();
            builder.Services.AddSingleton<AppOnlineService>();
            builder.Services.AddSingleton<WorkLifecycle>();
            builder.Services.AddSingleton(services =>
                new AppLibrary(services, ServiceDescriptor.Singleton(new object())));
            builder.Services.AddHttpClient(OnlineEndpoints.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            await using WebApplication app = builder.Build();
            app.MapOnlineEndpoints();
            await app.StartAsync();
            string address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new(address) };
            try
            {
                using HttpResponseMessage put = await client.PostAsync("/workspace/online", null);
                Require(put.IsSuccessStatusCode &&
                    (await put.Content.ReadFromJsonAsync<AppOnlineStatus>())?.Online == true,
                    "Putting an Aspire app online waits for its deployed readiness check.");

                using HttpResponseMessage frontend = await client.GetAsync("/online/first/");
                Require(frontend.IsSuccessStatusCode && handler.LastUri?.AbsolutePath == "/index.html",
                    "The front end is proxied from the app-owned Aspire API Service.");
                string apiHost = $"api-service.{AppWorkerManifest.NamespaceName(identity.Id)}.svc.cluster.local";
                using HttpResponseMessage list = await client.GetAsync("/online/first/api/items?done=false");
                Require(list.IsSuccessStatusCode && handler.LastUri?.PathAndQuery == "/api/items?done=false" &&
                    handler.LastUri.Host == apiHost,
                    "Extensionless API routes and their query strings reach the deployed API Service.");

                using var create = new StringContent("""{"title":"Borrow a ladder"}""", Encoding.UTF8, "application/json");
                using HttpResponseMessage created = await client.PostAsync("/online/first/api/items", create);
                Require(created.IsSuccessStatusCode && handler.LastMethod == "POST" &&
                    handler.LastBody == """{"title":"Borrow a ladder"}""" &&
                    handler.LastContentType == "application/json" && !handler.ForwardedCredential,
                    "Writes forward only their bounded body and content type to the app API.");

                using var preflight = new HttpRequestMessage(HttpMethod.Options, "/online/first/api/items");
                using HttpResponseMessage allowed = await client.SendAsync(preflight);
                Require(allowed.StatusCode == HttpStatusCode.NoContent &&
                    allowed.Headers.GetValues("Access-Control-Allow-Headers").Single() == "content-type",
                    "The sandboxed app origin gets a preflight answer for JSON writes.");

                handler.Status = HttpStatusCode.Conflict;
                using HttpResponseMessage conflict = await client.DeleteAsync("/online/first/api/items/1");
                Require(conflict.StatusCode == HttpStatusCode.Conflict && handler.LastMethod == "DELETE",
                    "App validation results pass through to the browser.");
                handler.Status = HttpStatusCode.InternalServerError;
                using HttpResponseMessage failed = await client.GetAsync("/online/first/api/items");
                Require(failed.StatusCode == HttpStatusCode.BadGateway, "App server errors stay opaque.");
                handler.Status = HttpStatusCode.OK;

                int requests = handler.Requests;
                using HttpResponseMessage oversized = await client.PostAsync("/online/first/api/items",
                    new ByteArrayContent(new byte[OnlineEndpoints.MaxRequestBytes + 1]));
                using HttpResponseMessage hidden = await client.GetAsync("/online/first/.env");
                Require(oversized.StatusCode == HttpStatusCode.RequestEntityTooLarge &&
                    hidden.StatusCode == HttpStatusCode.NotFound && handler.Requests == requests,
                    "Oversized bodies and dot-segments never reach the app.");

                // The page that started an update reloads or closes while the build runs.
                jobs.HoldBuild = new(TaskCreationOptions.RunContinuationsAsynchronously);
                int revisions = deployments.GetSnapshot().Revisions.Count;
                using (var abandoned = new CancellationTokenSource())
                {
                    Task<HttpResponseMessage> update = client.PostAsync("/workspace/online", null, abandoned.Token);
                    await PollAsync(() => deployments.GetSnapshot().Revisions.Count > revisions);
                    abandoned.Cancel();
                    try { (await update).Dispose(); } catch (OperationCanceledException) { }
                }
                await Task.Delay(200);
                jobs.HoldBuild.SetResult();
                jobs.HoldBuild = null;
                await PollAsync(() => deployments.GetSnapshot().Revisions[^1].Phase == AppDeploymentPhase.Deployed);
                Require(deployments.GetSnapshot().Revisions.Count == revisions + 1,
                    "An update keeps deploying after the page that started it goes away.");
            }
            finally
            {
                await app.StopAsync();
            }
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
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
            manifest.NetworkPolicy.Spec.Ingress.Single().FromProperty.Single().PodSelector.MatchLabels["soaks.dev/host"] == "true",
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
        Require(manifest.Deployment.Spec.Template.Metadata.Annotations["soaks.dev/revision"] !=
            changed.Deployment.Spec.Template.Metadata.Annotations["soaks.dev/revision"], "Content changes roll out the static server.");

        string firstNamespace = AppWorkerManifest.NamespaceName("first");
        AppWorkerManifests workers = AppWorkerManifest.Create(
            "first", "wave-001", "soaks", firstNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "soaks-workspace", "fixture/.git/soaks-apps/first", "fixture/.git/soaks-artifacts/first",
            "registry-pull");
        string secondId = Guid.NewGuid().ToString("N");
        string secondNamespace = AppWorkerManifest.NamespaceName(secondId);
        AppWorkerManifests anotherApp = AppWorkerManifest.Create(
            secondId, "wave-001", "soaks", secondNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "soaks-workspace", $"fixture/.git/soaks-apps/{secondId}", $"fixture/.git/soaks-artifacts/{secondId}");
        AppWorkerManifests nextWave = AppWorkerManifest.Create(
            "first", "wave-002", "soaks", firstNamespace, "registry.example/worker@sha256:" + new string('a', 64),
            "soaks-workspace", "fixture/.git/soaks-apps/first", "fixture/.git/soaks-artifacts/first");
        V1PodSpec buildPod = workers.BuildJob.Spec.Template.Spec;
        V1Container buildContainer = buildPod.Containers.Single();
        Require(workers.BuildJob.Metadata.NamespaceProperty == "soaks" &&
            workers.DeployJob.Metadata.NamespaceProperty == "soaks" &&
            workers.DeployServiceAccount.Metadata.NamespaceProperty == "soaks",
            "Worker jobs, shared artifacts, and per-app deploy identities stay in the coordinator namespace.");
        Require(anotherApp.DeployServiceAccount.Metadata.Name != workers.DeployServiceAccount.Metadata.Name &&
            workers.DeployServiceAccount.Metadata.Name == nextWave.DeployServiceAccount.Metadata.Name &&
            secondNamespace != firstNamespace,
            "Each app has a distinct deploy identity, reused only across its own deployment waves.");
        Require(workers.BuildJob.Metadata.Name != nextWave.BuildJob.Metadata.Name &&
            workers.DeployJob.Metadata.Name != nextWave.DeployJob.Metadata.Name &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().Args.SequenceEqual(
                nextWave.DeployJob.Spec.Template.Spec.Containers.Single().Args),
            "Each wave gets fresh Jobs while every wave upgrades the same app release with the same identity.");
        Require(buildPod.ServiceAccountName == "default" && buildPod.AutomountServiceAccountToken == false &&
            !buildPod.Containers.Single().Env.Any(variable => variable.Name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)),
            "The build job has no Kubernetes API credentials.");
        Require(buildPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.WorkspaceVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == false &&
            buildPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.ArtifactVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == false &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).ReadOnlyProperty == true &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).SubPath ==
                "fixture/.git/soaks-apps/first" &&
            buildContainer.VolumeMounts.Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).SubPath ==
                "fixture/.git/soaks-artifacts/first" &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).ReadOnlyProperty == true &&
            workers.DeployJob.Spec.Template.Spec.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.ArtifactVolume).SubPath ==
                "fixture/.git/soaks-artifacts/first",
            "Jobs exchange artifacts through isolated per-app subpaths on the persistent coordinator volume.");
        Require(workers.BuildNetworkPolicy.Spec.Egress.Count == 0 &&
            workers.BuildNetworkPolicy.Metadata.NamespaceProperty == "soaks" &&
            workers.BuildNetworkPolicy.Spec.PodSelector.MatchLabels[AppWorkerManifest.StageLabel] == "build" &&
            buildPod.Containers.Single().Name == "worker" &&
            workers.BuildJob.Metadata.Labels[AppWorkerManifest.WorkerRunLabel] == "wave-001",
            "The build job cannot make network egress requests in the coordinator namespace.");
        Require(workers.DeployJob.Spec.Template.Spec.ServiceAccountName == workers.DeployServiceAccount.Metadata.Name &&
            workers.DeployJob.Spec.Template.Spec.AutomountServiceAccountToken == true,
            "Only the distinct deployment job receives an identity that the trusted provisioner binds in its target app namespace.");
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
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "soaks", "not/namespace",
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "soaks", firstNamespace,
            "worker@sha256:invalid", "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "soaks", firstNamespace,
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "../other-app", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-001", "soaks", "soaks",
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));
        ThrowsArgument(() => AppWorkerManifest.Create("first", "wave-00000000000000000", "soaks", firstNamespace,
            "registry.example/worker@sha256:" + new string('a', 64), "workspace", "workspace/first", "artifacts/first"));

        V1Job validationJob = AppWorkerManifest.CreateValidationJob(
            "soaks-validate-test",
            "first",
            "soaks",
            "registry.example/worker@sha256:" + new string('a', 64),
            "soaks-workspace",
            "fixture/.git/soaks-apps/first");
        V1PodSpec validationPod = validationJob.Spec.Template.Spec;
        Require(validationPod.AutomountServiceAccountToken == false &&
            validationPod.Containers.Single().Command.SequenceEqual(new[] { "sh" }) &&
            validationPod.Containers.Single().Args[0] == "-c" &&
            validationPod.Containers.Single().Args[1].Contains("cp -R /workspace/. /tmp/source/") &&
            validationPod.Containers.Single().Args[1].EndsWith("exec sh tests/run.sh") &&
            validationPod.Containers.Single().WorkingDir == "/tmp",
            "Fixture tests execute only in a token-free validation job.");
        Require(validationPod.Containers.Single().Args[1].Contains("cp -R /worker/npm-cache /tmp/npm-cache") &&
            validationPod.Containers.Single().Env.Any(env => env.Name == "npm_config_cache" && env.Value == "/tmp/npm-cache"),
            "The app's offline checks get the worker's npm package cache, like the build job.");
        Require(validationPod.Volumes.Single(volume => volume.Name == AppWorkerManifest.WorkspaceVolume)
                .PersistentVolumeClaim.ReadOnlyProperty == true &&
            validationPod.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).SubPath ==
            "fixture/.git/soaks-apps/first" &&
            validationPod.Containers.Single().VolumeMounts
                .Single(mount => mount.Name == AppWorkerManifest.WorkspaceVolume).ReadOnlyProperty == true &&
            validationPod.Containers.Single().SecurityContext.ReadOnlyRootFilesystem == true,
            "Validation receives only its read-only app workspace and bounded temporary storage.");
        ThrowsArgument(() => AppWorkerManifest.CreateValidationJob(
            "soaks-validate-test", "first", "soaks",
            "registry.example/worker@sha256:" + new string('a', 64),
            "soaks-workspace", "../other-app"));
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
        builder.Services.AddSingleton<IDeployedAppReadinessProbe, FakeReadinessProbe>();
        builder.Services.AddSingleton<AppDeploymentStateStore>();
        builder.Services.AddSingleton<AppTopologyService>();
        builder.Services.AddSingleton(new AppDeploymentSettings(
            "soaks",
            "registry.example/worker@sha256:" + new string('a', 64),
            Path.GetDirectoryName(workspace.RootPath)!,
            "soaks-workspace",
            null));
        builder.Services.AddSingleton<IAppDeploymentJobClient>(
            new FakeAppDeploymentJobClient(workspace.StatePath("soaks-artifacts/first")));
        builder.Services.AddSingleton<SquadOnAks.Web.Workspace.IAppNamespaceProvisioner>(
            new FakeAppNamespaceProvisioner());
        builder.Services.AddSingleton<KubernetesAppDeploymentRunner>();
        builder.Services.AddSingleton<AppOnlineService>();
        builder.Services.AddSingleton<WorkLifecycle>();
        builder.Services.AddSingleton(services => new AppLibrary(services, ServiceDescriptor.Singleton(new object())));
        var handler = new StaticHandler();
        builder.Services.AddHttpClient(OnlineEndpoints.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using WebApplication app = builder.Build();
        app.MapOnlineEndpoints();
        app.MapAppDeploymentEndpoints();
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new(address) };
        try
        {
            WorkLifecycle lifecycle = app.Services.GetRequiredService<WorkLifecycle>();
            lifecycle.SetCastingState(SquadLifecycleStates.NeedsCast);
            AppDeploymentSnapshot expectedHistory =
                app.Services.GetRequiredService<AppDeploymentStateStore>().GetSnapshot();
            AppDeploymentSnapshot? deploymentHistory =
                await client.GetFromJsonAsync<AppDeploymentSnapshot>("/workspace/deployments");
            Require(deploymentHistory is not null &&
                deploymentHistory.AppId == expectedHistory.AppId &&
                deploymentHistory.Namespace == expectedHistory.Namespace &&
                deploymentHistory.ReleaseName == expectedHistory.ReleaseName &&
                deploymentHistory.Revisions.SequenceEqual(expectedHistory.Revisions),
                "The per-app deployment endpoint exposes only its persisted deployment history.");
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
            Require(response.Headers.GetValues("Access-Control-Allow-Origin").Single() == "*" &&
                !response.Headers.Contains("Access-Control-Allow-Credentials"),
                "Opaque-origin sandbox modules can read only the public, credential-free app proxy.");
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
        public string? LastMethod;
        public string? LastBody;
        public string? LastContentType;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastUri = request.RequestUri;
            LastMethod = request.Method.Method;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            ForwardedCredential |= request.Headers.Contains("Authorization") || request.Headers.Contains("Cookie");
            var response = new HttpResponseMessage(Status)
            {
                Content = new StringContent(Oversized ? new string('x', StaticAppManifest.MaxFileBytes + 1) : "<h1>Online</h1>")
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "upstream=not-allowed");
            response.Headers.Location = new("http://127.0.0.1/private");
            return response;
        }

    }

    private sealed class FakeReadinessProbe : IDeployedAppReadinessProbe
    {
        public bool IsReady { get; set; } = true;
        public int Calls { get; private set; }
        public Uri? LastOrigin { get; private set; }
        public IReadOnlyList<string>? LastHealthPaths { get; private set; }

        public Task<bool> IsReadyAsync(Uri origin, IReadOnlyList<string> healthPaths, CancellationToken cancellationToken)
        {
            Calls++;
            LastOrigin = origin;
            LastHealthPaths = healthPaths;
            return Task.FromResult(IsReady);
        }
    }

    private sealed class ReadinessHandler : HttpMessageHandler
    {
        public HttpStatusCode FrontendStatus { get; set; } = HttpStatusCode.OK;
        public string FrontendContentType { get; set; } = "text/html";
        public HttpStatusCode HealthStatus { get; set; } = HttpStatusCode.OK;
        public List<string> Paths { get; } = [];
        public Exception? Failure { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            Paths.Add(path);
            if (Failure is not null) return Task.FromException<HttpResponseMessage>(Failure);
            bool frontend = path == "/";
            var response = new HttpResponseMessage(frontend ? FrontendStatus : HealthStatus)
            {
                Content = new StringContent(
                    frontend ? "<html>App</html>" : "Healthy",
                    Encoding.UTF8,
                    frontend ? FrontendContentType : "text/plain")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ReadinessClientFactory(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        private readonly List<HttpClient> clients = [];

        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(handler, disposeHandler: false);
            clients.Add(client);
            return client;
        }

        public void Dispose()
        {
            foreach (HttpClient client in clients)
            {
                client.Dispose();
            }
        }
    }

    private static async Task PollAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(50);
        Require(condition(), "Timed out waiting for a deployment state change.");
    }

    private sealed class FakeAppNamespaceProvisioner : SquadOnAks.Web.Workspace.IAppNamespaceProvisioner
    {
        public List<string> AppIds { get; } = [];

        public Task EnsureAsync(string appId, CancellationToken cancellationToken)
        {
            AppIds.Add(appId);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string appId, CancellationToken cancellationToken) => Task.CompletedTask;

        public List<string> ChecksEnsured { get; } = [];
        public List<string> ChecksDeleted { get; } = [];

        public Task EnsureCheckAsync(string appId, string runId, CancellationToken cancellationToken)
        {
            ChecksEnsured.Add(runId);
            return Task.CompletedTask;
        }

        public Task DeleteCheckAsync(string appId, string runId, CancellationToken cancellationToken)
        {
            ChecksDeleted.Add(runId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppDeploymentJobClient(string artifactDirectory) : IAppDeploymentJobClient
    {
        public List<string> Stages { get; } = [];
        public List<(V1Job Job, V1NetworkPolicy? Policy)> Runs { get; } = [];
        public bool CancelNextDeploy { get; set; }
        public bool FailNextDeploy { get; set; }
        public string? FailNextJourneys { get; set; }
        public TaskCompletionSource? HoldBuild { get; set; }

        public Task EnsureServiceAccountAsync(V1ServiceAccount serviceAccount, CancellationToken cancellationToken)
        {
            Require(serviceAccount.AutomountServiceAccountToken == true,
                "The deploy identity is created only for the scoped deploy worker.");
            return Task.CompletedTask;
        }

        public async Task RunAsync(V1Job job, V1NetworkPolicy? networkPolicy, CancellationToken cancellationToken)
        {
            if (job.Metadata.Labels[AppWorkerManifest.StageLabel] == "build" && HoldBuild is { } hold)
                await hold.Task.WaitAsync(cancellationToken);
            string stage = job.Metadata.Labels[AppWorkerManifest.StageLabel];
            Stages.Add(stage);
            Runs.Add((job, networkPolicy));
            if (stage == AppWorkerManifest.JourneyStage)
            {
                if (FailNextJourneys is { } detail)
                {
                    FailNextJourneys = null;
                    throw new AppDeploymentJobFailedException("Simulated journey failure.", AppDeploymentFailure.JourneysFailed, detail);
                }
                return;
            }
            if (stage == AppWorkerManifest.CheckStage) return;
            Require(job.Metadata.NamespaceProperty == "soaks" &&
                (stage != "build" || networkPolicy?.Spec.Egress.Count == 0),
                "Worker jobs stay in the coordinator namespace and build egress is denied.");

            if (stage == "build")
            {
                Directory.CreateDirectory(artifactDirectory);
                File.WriteAllBytes(Path.Combine(artifactDirectory, "package.tgz"), [1, 2, 3]);
            }
            else if (CancelNextDeploy)
            {
                CancelNextDeploy = false;
                throw new OperationCanceledException("Simulated worker interruption.");
            }
            else if (FailNextDeploy)
            {
                FailNextDeploy = false;
                throw new AppDeploymentJobFailedException("Simulated worker failure.", AppDeploymentFailure.NotReady);
            }
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
            deployment.Status = new()
            {
                AvailableReplicas = 1,
                UpdatedReplicas = 1,
                Replicas = 1,
                ObservedGeneration = deployment.Metadata.Generation
            };
        }
    }

    private sealed class FakeValidationJobClient : IFixtureValidationJobClient
    {
        public string Namespace => "soaks";
        public V1Job? Created { get; private set; }
        public bool Succeeded { get; init; }
        public bool Failed { get; init; }
        public bool Pending { get; init; }
        public bool Deleted { get; private set; }
        public string Logs { get; init; } = "";
        public TaskCompletionSource Watching { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task CreateAsync(V1Job job, CancellationToken cancellationToken)
        {
            Created = job;
            return Task.CompletedTask;
        }

        public async Task<V1Job?> ReadAsync(string name, CancellationToken cancellationToken)
        {
            Watching.TrySetResult();
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
