using System.Net;
using System.Security.Cryptography;
using k8s;
using k8s.Autorest;
using k8s.Models;
using SquadOnAks.Web.Security;

namespace SquadOnAks.Web.Workspace;

public sealed record AppDeploymentSettings(
    string CoordinatorNamespace,
    string WorkerImage,
    string WorkspaceMountRoot,
    string WorkspaceClaim,
    string? ImagePullSecret,
    string? RegistryPushEndpoint = null,
    string? RegistryPullEndpoint = null,
    string? RegistrySecret = null);

public interface IAppDeploymentJobClient
{
    Task EnsureServiceAccountAsync(V1ServiceAccount serviceAccount, CancellationToken cancellationToken);

    /// <summary>Runs a worker Job; for the journey stage, returns which journeys its result line says passed (names only).</summary>
    Task<AppJourneyOutcome?> RunAsync(V1Job job, V1NetworkPolicy? networkPolicy, CancellationToken cancellationToken);
}

/// <summary>What the journey runner's result line reported: the journeys that passed in order, and the one that broke.</summary>
public sealed record AppJourneyOutcome(IReadOnlyList<string> Passed, string? Broken);

public sealed class AppDeploymentJobFailedException(
    string message, string? reason = null, string? detail = null, AppJourneyOutcome? journeys = null) : InvalidOperationException(message)
{
    /// <summary>Which journeys a failed check kept and which one broke (names only); null when the runner didn't say.</summary>
    public AppJourneyOutcome? Journeys { get; } = journeys;

    /// <summary>A fixed reason code from <see cref="AppDeploymentFailure"/>, never log text.</summary>
    public string? Reason { get; } = reason;

    /// <summary>What a failed journey check found (steps, selectors, request statuses), for the team only.</summary>
    public string? Detail { get; } = detail;
}

/// <summary>
/// Turns a failed worker's log into a fixed reason code, so people get a useful next step without
/// ever seeing logs or command output.
/// </summary>
public static class AppDeploymentFailure
{
    public const string PackagesUnavailable = "packages-unavailable";
    public const string BuildFailed = "build-failed";
    public const string NotReady = "not-ready";
    public const string JourneysFailed = "journeys-failed";
    /// <summary>The cluster had no room to schedule a worker or the app's pods (a capacity problem, not the app's).</summary>
    public const string ClusterFull = "cluster-full";
    public const string MissingPrefix = "missing:";
    /// <summary>The journey runner's single result line for a failed check.</summary>
    public const string JourneyFailureMarker = "SOAKS-CHECK-FAILED: ";
    /// <summary>The journey runner's single result line for a passed check.</summary>
    public const string JourneyPassedMarker = "SOAKS-CHECK-PASSED: ";

    public static bool IsKnown(string? reason) =>
        reason is PackagesUnavailable or BuildFailed or NotReady or JourneysFailed or ClusterFull ||
        reason is not null && reason.StartsWith(MissingPrefix, StringComparison.Ordinal) &&
            Work.AppBuildPolicy.AspireLayout.Contains(reason[MissingPrefix.Length..], StringComparer.Ordinal);

    public static string? Classify(string stage, string log)
    {
        foreach (string required in Work.AppBuildPolicy.AspireLayout)
            if (log.Contains($"Blocked: the app is missing {required} ", StringComparison.Ordinal)) return MissingPrefix + required;
        if (stage == "build" && Matches(log, @"\bNU110[12]\b|ENOTCACHED|Unable to find package|npm (?:ERR!|error) code ETARGET"))
            return PackagesUnavailable;
        if (stage == "build" && Matches(log, @"\berror (?:CS|MSB|TS)\d+|Build FAILED|\[vite\][^\n]*error|error during build"))
            return BuildFailed;
        if (stage is "deploy" or AppWorkerManifest.CheckStage &&
            Matches(log, @"timed out waiting for the condition|context deadline exceeded|UPGRADE FAILED|INSTALLATION FAILED"))
            return NotReady;
        if (stage == AppWorkerManifest.JourneyStage && JourneyDetail(log) is not null) return JourneysFailed;
        return null;
    }

    /// <summary>The bounded notes from the journey runner's result line, one per line; null when it reported none.</summary>
    public static string? JourneyDetail(string log)
    {
        string? line = log.Split('\n').Select(item => item.TrimEnd('\r'))
            .LastOrDefault(item => item.StartsWith(JourneyFailureMarker, StringComparison.Ordinal));
        if (line is null) return null;
        try
        {
            using var result = System.Text.Json.JsonDocument.Parse(line[JourneyFailureMarker.Length..]);
            var lines = new List<string>();
            if (result.RootElement.TryGetProperty("message", out var message) && message.GetString() is { } text) lines.Add(text);
            if (result.RootElement.TryGetProperty("details", out var details) && details.ValueKind == System.Text.Json.JsonValueKind.Array)
                lines.AddRange(details.EnumerateArray().Select(item => item.GetString()).OfType<string>().Take(16).Select(item => "- " + item));
            string joined = string.Join('\n', lines);
            return joined.Length == 0 ? null : joined.Length > 6000 ? joined[..6000] : joined;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Which journeys the runner's last result line (passed or failed) reports as passed and broken, names only;
    /// null when there is no readable result line.
    /// </summary>
    public static AppJourneyOutcome? JourneyOutcome(string log)
    {
        string? line = log.Split('\n').Select(item => item.TrimEnd('\r'))
            .LastOrDefault(item => item.StartsWith(JourneyPassedMarker, StringComparison.Ordinal) ||
                item.StartsWith(JourneyFailureMarker, StringComparison.Ordinal));
        if (line is null) return null;
        bool passed = line.StartsWith(JourneyPassedMarker, StringComparison.Ordinal);
        try
        {
            using var result = System.Text.Json.JsonDocument.Parse(line[(passed ? JourneyPassedMarker : JourneyFailureMarker).Length..]);
            if (result.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            string? broken = !passed && result.RootElement.TryGetProperty("journey", out var journey) &&
                journey.ValueKind == System.Text.Json.JsonValueKind.String ? journey.GetString() : null;
            var names = new List<string>();
            if (result.RootElement.TryGetProperty(passed ? "journeys" : "passed", out var list) &&
                list.ValueKind == System.Text.Json.JsonValueKind.Array)
                names.AddRange(list.EnumerateArray().Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(item => item.GetString()!).Take(AppJourneyPromise.MaxCount));
            return new(names, broken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>A plain next step for a reason code; null when the failure was not recognized.</summary>
    public static string? Message(string? reason) => reason switch
    {
        PackagesUnavailable => "The app asks for packages the offline build doesn't have. Ask your team in a wave to keep the provided package versions, then put it online again.",
        BuildFailed => "The app's code didn't build. Ask your team in a wave to fix the build, then put it online again.",
        NotReady => "The new revision didn't become ready, so the previous one keeps running. Ask your team in a wave to check the app's health checks.",
        ClusterFull => "Your cluster doesn't have room to run this right now, so nothing was changed. Take apps you aren't using offline or delete them, then put it online again.",
        JourneysFailed => "The new revision failed its own user journeys in a throwaway test copy, so it was not put online. Ask your team in a wave to fix what the test found, then put it online again.",
        _ when IsKnown(reason) => $"The app is missing {reason![MissingPrefix.Length..]}. Ask your team in a wave to add it, then put it online again.",
        _ => null
    };

    private static bool Matches(string text, string pattern) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
public sealed class AppDeploymentReconciliationException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

public sealed class KubernetesAppDeploymentRunner(
    AppIdentity identity,
    DisposableFixtureWorkspace workspace,
    AppDeploymentStateStore state,
    AppDeploymentSettings settings,
    IAppDeploymentJobClient jobs,
    IAppNamespaceProvisioner namespaces,
    ILogger<KubernetesAppDeploymentRunner> logger,
    IAppWorkloadStatusReader? live = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(10);

    public async Task<AppDeploymentSnapshot> RunAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            AppDeploymentSnapshot snapshot = state.GetSnapshot();
            AppDeploymentRevision? revision = snapshot.Revisions.LastOrDefault(item =>
                item.Phase is AppDeploymentPhase.Building or AppDeploymentPhase.Built or AppDeploymentPhase.Checking or
                    AppDeploymentPhase.Deploying);
            if (revision is null)
            {
                string runId = Guid.NewGuid().ToString("N")[..16];
                state.Begin(runId);
                revision = state.GetSnapshot().Revisions[^1];
            }

            string workspaceSubPath = RelativeSubPath(settings.WorkspaceMountRoot, workspace.RootPath);
            string artifactSubPath = $"{workspaceSubPath}/.git/soaks-artifacts/{identity.Id}";
            string artifactDirectory = workspace.StatePath(Path.Combine("soaks-artifacts", identity.Id));
            Directory.CreateDirectory(artifactDirectory);
            AppWorkerManifests manifests = AppWorkerManifest.Create(
                identity.Id,
                revision.RunId,
                settings.CoordinatorNamespace,
                AppWorkerManifest.NamespaceName(identity.Id),
                settings.WorkerImage,
                settings.WorkspaceClaim,
                workspaceSubPath,
                artifactSubPath,
                settings.ImagePullSecret,
                settings.RegistryPushEndpoint,
                settings.RegistryPullEndpoint,
                DeclaredComponents(),
                settings.RegistrySecret);

            try
            {
                if (revision.Phase == AppDeploymentPhase.Building)
                {
                    await jobs.RunAsync(manifests.BuildJob, manifests.BuildNetworkPolicy, cancellationToken);
                    string packagePath = Path.Combine(artifactDirectory, "package.tgz");
                    string digest;
                    try
                    {
                        digest = await HashArtifactAsync(packagePath, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        state.Fail(revision.RunId);
                        throw;
                    }

                    state.RecordBuilt(revision.RunId, digest);
                    revision = state.GetSnapshot().Revisions[^1];
                }

                (string Input, IReadOnlyList<string> Names)? journeys = JourneyInput();
                if (revision.Phase == AppDeploymentPhase.Built)
                {
                    // Every revision with declared journeys proves them on a throwaway copy before it replaces anything.
                    revision = journeys is null ? state.BeginDeploy(revision.RunId) : state.BeginCheck(revision.RunId);
                }

                if (revision.Phase == AppDeploymentPhase.Checking)
                {
                    await jobs.EnsureServiceAccountAsync(manifests.DeployServiceAccount, cancellationToken);
                    if (journeys is { } declared)
                        await CheckAsync(revision.RunId, workspaceSubPath, artifactSubPath, declared.Input, declared.Names, cancellationToken);
                    revision = state.BeginDeploy(revision.RunId);
                }

                if (revision.Phase == AppDeploymentPhase.Deploying)
                {
                    await namespaces.EnsureAsync(identity.Id, cancellationToken);
                    await jobs.EnsureServiceAccountAsync(manifests.DeployServiceAccount, cancellationToken);
                    await RunDeployAsync(manifests.DeployJob, AppWorkerManifest.NamespaceName(identity.Id), cancellationToken);
                    state.Complete(revision.RunId);
                }

                return state.GetSnapshot();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AppDeploymentJobFailedException exception)
            {
                state.Fail(revision.RunId, exception.Reason);
                if (exception.Detail is not null) state.RecordCheckFailure(revision.RunId, exception.Detail);
                logger.LogError(exception, "App deployment {RunId} failed for app {AppId}.", revision.RunId, identity.Id);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "App deployment {RunId} for app {AppId} requires reconciliation before it can be retried.",
                    revision.RunId,
                    identity.Id);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The app's declared components; an app without a readable contract builds the single default shape.</summary>
    private AppComponents DeclaredComponents()
    {
        try { return AppBuildManifest.Read(workspace)?.EffectiveComponents ?? AppComponents.Default; }
        catch (InvalidOperationException) { return AppComponents.Default; }
    }

    /// <summary>
    /// The app's declared journeys as the runner's input, with their names in the order they run;
    /// null for an app that declares none.
    /// </summary>
    private (string Input, IReadOnlyList<string> Names)? JourneyInput()
    {
        AppRuntimeSpec? runtime;
        try { runtime = AppBuildManifest.Read(workspace)?.Runtime; }
        catch (InvalidOperationException) { runtime = null; }
        if (runtime?.Journeys is not { Count: > 0 } journeys) return null;
        return (System.Text.Json.JsonSerializer.Serialize(
            new { journeys, restartJourneys = runtime.RestartJourneys ?? [] },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            journeys.Concat(runtime.RestartJourneys ?? []).Select(journey => journey.Name).ToArray());
    }

    private async Task CheckAsync(
        string runId, string workspaceSubPath, string artifactSubPath, string journeys, IReadOnlyList<string> names,
        CancellationToken cancellationToken)
    {
        // A fresh namespace per attempt: a retry never waits on the previous copy's namespace to finish terminating.
        string checkId = Guid.NewGuid().ToString("N")[..16];
        AppCheckManifests check = AppWorkerManifest.CreateCheck(
            identity.Id,
            checkId,
            settings.CoordinatorNamespace,
            settings.WorkerImage,
            settings.WorkspaceClaim,
            workspaceSubPath,
            artifactSubPath,
            journeys,
            settings.ImagePullSecret,
            settings.RegistryPushEndpoint,
            settings.RegistryPullEndpoint,
            settings.RegistrySecret);
        // The app's promises (journey names only) for this revision: none checked until the runner reports.
        state.RecordJourneys(runId, AppJourneyPromise.From(names, null));
        try
        {
            await namespaces.EnsureCheckAsync(identity.Id, checkId, cancellationToken);
            await RunDeployAsync(check.DeployJob, check.CheckNamespace, cancellationToken);
            try
            {
                AppJourneyOutcome? outcome = await jobs.RunAsync(check.JourneyJob, check.JourneyNetworkPolicy, cancellationToken);
                // The runner exits successfully only after every journey passed; an older runner image names none.
                state.RecordJourneys(runId, AppJourneyPromise.From(names, outcome ?? new(names, null)));
            }
            catch (AppDeploymentJobFailedException exception) when (exception.Reason == AppDeploymentFailure.JourneysFailed)
            {
                state.RecordJourneys(runId, AppJourneyPromise.From(names, exception.Journeys));
                if (live is null) throw;
                // The browser only sees the gateway. Before the test copy is removed, say which services weren't ready
                // (reason codes and counts only), so a fix wave knows where to look.
                throw new AppDeploymentJobFailedException(exception.Message, exception.Reason,
                    (exception.Detail is null ? "" : exception.Detail + "\n\n") + await CheckCopyWorkloadsAsync(check.CheckNamespace),
                    exception.Journeys);
            }
        }
        finally
        {
            try
            {
                await namespaces.DeleteCheckAsync(identity.Id, checkId, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The provisioner also sweeps an app's check namespaces when the app is deleted.
                logger.LogWarning(exception, "Check namespace {Namespace} for app {AppId} was not removed.", check.CheckNamespace, identity.Id);
            }
        }
    }

    /// <summary>
    /// Runs a deploy Job while watching the target namespace's workloads (replica counts, reason codes and restart
    /// counts only). If the release never became ready, the team is told which workloads weren't ready and why.
    /// </summary>
    private async Task RunDeployAsync(V1Job job, string targetNamespace, CancellationToken cancellationToken)
    {
        if (live is null)
        {
            await jobs.RunAsync(job, null, cancellationToken);
            return;
        }
        using var watching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        AppLiveStatus? unready = null;
        Task watcher = Task.Run(async () =>
        {
            while (!watching.IsCancellationRequested)
            {
                try
                {
                    AppLiveStatus? status = await live.ReadAsync(targetNamespace, watching.Token);
                    // Keep the latest view in which something wasn't ready: a rollback afterwards removes the evidence.
                    if (status is not null && status.Nodes.Any(pair => pair.Key.StartsWith("workload:", StringComparison.Ordinal) && pair.Value.State != "ready"))
                        unready = status;
                    await Task.Delay(WatchInterval, watching.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Workload status for {Namespace} is unavailable.", targetNamespace);
                    try { await Task.Delay(WatchInterval, watching.Token); } catch (OperationCanceledException) { break; }
                }
            }
        }, CancellationToken.None);
        try
        {
            await jobs.RunAsync(job, null, cancellationToken);
        }
        catch (AppDeploymentJobFailedException exception) when (exception.Reason == AppDeploymentFailure.NotReady && exception.Detail is null &&
            unready is not null && unready.Nodes.Values.Any(status => status.Detail.Contains("Unschedulable", StringComparison.Ordinal)))
        {
            // The app's pods never got a place to run: a capacity problem for the person, not something for the team to fix.
            throw new AppDeploymentJobFailedException(exception.Message, AppDeploymentFailure.ClusterFull);
        }
        catch (AppDeploymentJobFailedException exception) when (exception.Reason == AppDeploymentFailure.NotReady && exception.Detail is null &&
            NotReadyDetail(unready) is { } detail)
        {
            throw new AppDeploymentJobFailedException(exception.Message, exception.Reason, detail);
        }
        finally
        {
            await watching.CancelAsync();
            await watcher;
        }
    }

    private async Task<string> CheckCopyWorkloadsAsync(string checkNamespace)
    {
        try
        {
            using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            AppLiveStatus? status = await live!.ReadAsync(checkNamespace, bounded.Token);
            return "In the test copy, by AppHost resource:\n" + (NotReadyDetail(status) ??
                "Every workload was ready, so the failure is in a request between the services: when the gateway answered " +
                "502 or 503, the service it forwards to failed that request (look at that service's handler and the gateway's " +
                "service name, path and port).");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogDebug(exception, "Workload status for {Namespace} is unavailable.", checkNamespace);
            return "The test copy's workload status couldn't be read.";
        }
    }

    /// <summary>Which workloads weren't ready, by Aspire resource, from reason codes and counts only.</summary>
    public static string? NotReadyDetail(AppLiveStatus? status)
    {
        if (status is null) return null;
        var workloads = status.Nodes.Where(pair => pair.Key.StartsWith("workload:", StringComparison.Ordinal))
            .Select(pair => (Name: pair.Key["workload:".Length..], Status: pair.Value,
                Component: status.Nodes.FirstOrDefault(other => !other.Key.Contains(':') && ReferenceEquals(other.Value, pair.Value)).Key))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToList();
        // Crashing or restarting workloads first: they are usually why the others (whose health checks depend on them) wait.
        var waiting = workloads.Where(item => item.Status.State != "ready")
            .OrderBy(item => item.Status.State == "failing" || item.Status.Detail.Contains("restart", StringComparison.Ordinal) ? 0 : 1)
            .ToList();
        if (waiting.Count == 0) return null;
        static string Explain(WorkloadStatus status) =>
            status.State == "starting" && !status.Detail.Contains("Pending", StringComparison.Ordinal) &&
            !status.Detail.Contains("restart", StringComparison.Ordinal)
                ? status.Detail + " · running, but /health/ready never answered 200"
                : status.Detail;
        var lines = waiting.Select(item => $"- {item.Component ?? item.Name} ({item.Name}): {Explain(item.Status)}").ToList();
        var ready = workloads.Where(item => item.Status.State == "ready").Select(item => item.Component ?? item.Name).ToList();
        if (ready.Count > 0) lines.Add("Ready: " + string.Join(", ", ready) + ".");
        return string.Join('\n', lines);
    }

    private static string RelativeSubPath(string mountRoot, string workspaceRoot)
    {
        string subPath = Path.GetRelativePath(mountRoot, workspaceRoot)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (subPath is "." or ".." || subPath.StartsWith("../", StringComparison.Ordinal) ||
            Path.IsPathRooted(subPath) || subPath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException("The app workspace is outside the worker mount.");
        }

        return subPath;
    }

    private static async Task<string> HashArtifactAsync(string path, CancellationToken cancellationToken)
    {
        var artifact = new FileInfo(path);
        if (!artifact.Exists || artifact.Length is <= 0 or > WorkspaceArtifacts.MaxArchiveBytes)
        {
            throw new InvalidOperationException("The build worker did not produce a bounded Aspire deployment package.");
        }

        await using FileStream stream = artifact.OpenRead();
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return "sha256:" + Convert.ToHexStringLower(digest);
    }
}

public sealed class KubernetesAppDeploymentJobClient(
    ILogger<KubernetesAppDeploymentJobClient> logger,
    SecretRedactor redactor) : IAppDeploymentJobClient, IDisposable
{
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));

    public async Task EnsureServiceAccountAsync(
        V1ServiceAccount serviceAccount,
        CancellationToken cancellationToken)
    {
        string namespaceName = serviceAccount.Metadata.NamespaceProperty;
        try
        {
            V1ServiceAccount existing = await client.Value.CoreV1.ReadNamespacedServiceAccountAsync(
                serviceAccount.Metadata.Name,
                namespaceName,
                cancellationToken: cancellationToken);
            if (!OwnedByApp(existing.Metadata, serviceAccount.Metadata.Labels) ||
                existing.AutomountServiceAccountToken != serviceAccount.AutomountServiceAccountToken)
            {
                throw new InvalidOperationException("The deployment service account is not owned by this app.");
            }
        }

        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await client.Value.CoreV1.CreateNamespacedServiceAccountAsync(
                serviceAccount,
                namespaceName,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<AppJourneyOutcome?> RunAsync(
        V1Job job,
        V1NetworkPolicy? networkPolicy,
        CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        bool policyReady = false;
        bool jobAttempted = false;
        bool workerTerminated = false;
        Exception? failure = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds((job.Spec.ActiveDeadlineSeconds ?? 1800) + 60));
        try
        {
            if (networkPolicy is not null)
            {
                await EnsureNetworkPolicyAsync(networkPolicy, timeout.Token);
                policyReady = true;
            }

            jobAttempted = true;
            await EnsureJobAsync(job, timeout.Token);
            return await WaitForCompletionAsync(job, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException(
                $"The {job.Metadata.Labels[AppWorkerManifest.StageLabel]} worker exceeded its execution limit.");
            failure = timeoutException;
            throw timeoutException;
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            if (jobAttempted)
            {
                try
                {
                    await RemoveJobAndWaitAsync(job, CancellationToken.None);
                    workerTerminated = true;
                }
                catch (Exception cleanupException) when (failure is not null)
                {
                    logger.LogError(
                        cleanupException,
                        "Worker job {JobName} cleanup failed after another deployment error.",
                        job.Metadata.Name);
                    throw new AppDeploymentReconciliationException(
                        $"Worker job {job.Metadata.Name} still needs cleanup before retry.",
                        cleanupException);
                }
            }

            if (policyReady && workerTerminated)
            {
                try
                {
                    await RemoveNetworkPolicyAsync(networkPolicy!, CancellationToken.None);
                }
                catch (Exception cleanupException) when (failure is not null)
                {
                    logger.LogError(
                        cleanupException,
                        "Worker egress policy cleanup failed after another deployment error.");
                    throw new AppDeploymentReconciliationException(
                        $"Worker egress policy {networkPolicy!.Metadata.Name} still needs cleanup before retry.",
                        cleanupException);
                }
            }
            else if (policyReady)
            {
                logger.LogError(
                    "Retaining deny-egress policy for worker run {RunId} because pod termination was not confirmed.",
                    job.Metadata.Labels[AppWorkerManifest.WorkerRunLabel]);
            }
        }
    }

    private async Task EnsureNetworkPolicyAsync(V1NetworkPolicy policy, CancellationToken cancellationToken)
    {
        string namespaceName = policy.Metadata.NamespaceProperty;
        try
        {
            V1NetworkPolicy existing = await client.Value.NetworkingV1.ReadNamespacedNetworkPolicyAsync(
                policy.Metadata.Name,
                namespaceName,
                cancellationToken: cancellationToken);
            if (!OwnedByApp(existing.Metadata, policy.Metadata.Labels) ||
                existing.Spec.PolicyTypes?.Contains("Egress", StringComparer.Ordinal) != true ||
                existing.Spec.Egress?.Count != policy.Spec.Egress?.Count ||
                !SameLabels(existing.Spec.PodSelector.MatchLabels, policy.Spec.PodSelector.MatchLabels))
            {
                throw new InvalidOperationException("An existing worker egress policy does not match this app run.");
            }
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await client.Value.NetworkingV1.CreateNamespacedNetworkPolicyAsync(
                policy,
                namespaceName,
                cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureJobAsync(V1Job job, CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        try
        {
            V1Job existing = await client.Value.BatchV1.ReadNamespacedJobAsync(
                job.Metadata.Name,
                namespaceName,
                cancellationToken: cancellationToken);
            if (!OwnedByApp(existing.Metadata, job.Metadata.Labels) || !MatchesSafeJobSpec(existing.Spec, job.Spec))
            {
                throw new InvalidOperationException("An existing worker job does not match this app run.");
            }
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await client.Value.BatchV1.CreateNamespacedJobAsync(
                job,
                namespaceName,
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>How long a worker pod may stay unschedulable (the cluster is out of room) before the run gives up.</summary>
    internal static readonly TimeSpan UnschedulableLimit = TimeSpan.FromMinutes(2);

    private async Task<AppJourneyOutcome?> WaitForCompletionAsync(V1Job job, CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        DateTimeOffset nextSchedulingCheck = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            if (DateTimeOffset.UtcNow >= nextSchedulingCheck)
            {
                nextSchedulingCheck = DateTimeOffset.UtcNow.AddSeconds(15);
                V1PodList waiting = await client.Value.CoreV1.ListNamespacedPodAsync(
                    namespaceName,
                    labelSelector: $"batch.kubernetes.io/job-name={job.Metadata.Name}",
                    cancellationToken: cancellationToken);
                // Reason codes only: a pod the scheduler can't place means the cluster is full, not that the app is broken.
                if (waiting.Items.Any(pod => AppWorkloadStatusCalculator.Unschedulable(pod) &&
                    pod.Metadata.CreationTimestamp is { } created && DateTime.UtcNow - created.ToUniversalTime() > UnschedulableLimit))
                {
                    string stage = job.Metadata.Labels[AppWorkerManifest.StageLabel];
                    logger.LogError("Worker {JobName} could not be scheduled: the cluster has no room.", job.Metadata.Name);
                    throw new AppDeploymentJobFailedException($"The {stage} worker could not be scheduled.", AppDeploymentFailure.ClusterFull);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            V1Job? current;
            try
            {
                current = await client.Value.BatchV1.ReadNamespacedJobAsync(
                    job.Metadata.Name,
                    namespaceName,
                    cancellationToken: cancellationToken);
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("The app deployment worker disappeared before reporting a result.");
            }

            if (current.Status?.Succeeded > 0)
            {
                return job.Metadata.Labels[AppWorkerManifest.StageLabel] == AppWorkerManifest.JourneyStage
                    ? await PassedJourneysAsync(job, cancellationToken)
                    : null;
            }

            if (current.Status?.Failed > 0)
            {
                V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(
                    namespaceName,
                    labelSelector: $"batch.kubernetes.io/job-name={job.Metadata.Name}",
                    cancellationToken: cancellationToken);
                string stage = job.Metadata.Labels[AppWorkerManifest.StageLabel];
                string? reason = null;
                string? detail = null;
                AppJourneyOutcome? journeys = null;
                foreach (V1Pod pod in pods.Items)
                {
                    await using Stream logs = await client.Value.CoreV1.ReadNamespacedPodLogAsync(
                        pod.Metadata.Name, namespaceName, tailLines: 60, limitBytes: 8192,
                        cancellationToken: cancellationToken);
                    using var reader = new StreamReader(logs);
                    string diagnostics = redactor.Redact(await reader.ReadToEndAsync(cancellationToken)) ?? "";
                    logger.LogError("Worker {JobName} failed: {Diagnostics}", job.Metadata.Name, diagnostics);
                    reason ??= AppDeploymentFailure.Classify(stage, diagnostics);
                    detail ??= stage == AppWorkerManifest.JourneyStage ? AppDeploymentFailure.JourneyDetail(diagnostics) : null;
                    journeys ??= stage == AppWorkerManifest.JourneyStage ? AppDeploymentFailure.JourneyOutcome(diagnostics) : null;
                }

                throw new AppDeploymentJobFailedException($"The {stage} worker failed.", reason, detail, journeys);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    /// <summary>
    /// The passed journey runner's result line (the last few log lines, bounded): which journeys it kept, names only.
    /// Null when it can't be read; the check still passed.
    /// </summary>
    private async Task<AppJourneyOutcome?> PassedJourneysAsync(V1Job job, CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        try
        {
            V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(
                namespaceName,
                labelSelector: $"batch.kubernetes.io/job-name={job.Metadata.Name}",
                cancellationToken: cancellationToken);
            foreach (V1Pod pod in pods.Items.Where(pod => pod.Status?.Phase == "Succeeded"))
            {
                await using Stream logs = await client.Value.CoreV1.ReadNamespacedPodLogAsync(
                    pod.Metadata.Name, namespaceName, tailLines: 5, limitBytes: 8192,
                    cancellationToken: cancellationToken);
                using var reader = new StreamReader(logs);
                if (AppDeploymentFailure.JourneyOutcome(redactor.Redact(await reader.ReadToEndAsync(cancellationToken)) ?? "") is { } outcome)
                    return outcome;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The passed journey result for {JobName} could not be read.", job.Metadata.Name);
        }
        return null;
    }

    private async Task RemoveJobAndWaitAsync(V1Job job, CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        try
        {
            await client.Value.BatchV1.DeleteNamespacedJobAsync(
                job.Metadata.Name,
                namespaceName,
                propagationPolicy: "Foreground",
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(
                namespaceName,
                labelSelector: $"batch.kubernetes.io/job-name={job.Metadata.Name}",
                cancellationToken: timeout.Token);
            if (pods.Items.Count == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
        }
    }

    private async Task RemoveNetworkPolicyAsync(V1NetworkPolicy policy, CancellationToken cancellationToken)
    {
        try
        {
            await client.Value.NetworkingV1.DeleteNamespacedNetworkPolicyAsync(
                policy.Metadata.Name,
                policy.Metadata.NamespaceProperty,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    private static bool OwnedByApp(V1ObjectMeta metadata, IDictionary<string, string> expectedLabels) =>
        expectedLabels.All(pair => metadata.Labels?.TryGetValue(pair.Key, out string? value) == true && value == pair.Value);

    private static bool MatchesSafeJobSpec(V1JobSpec? actual, V1JobSpec expected)
    {
        if (actual?.Template?.Spec is not { } pod ||
            expected.Template?.Spec is not { } expectedPod ||
            pod.ServiceAccountName != expectedPod.ServiceAccountName ||
            pod.AutomountServiceAccountToken != expectedPod.AutomountServiceAccountToken ||
            pod.HostNetwork == true || pod.HostPID == true || pod.HostIPC == true ||
            pod.Containers is not { Count: 1 } containers ||
            expectedPod.Containers is not { Count: 1 } expectedContainers ||
            pod.InitContainers is { Count: > 0 } || pod.EphemeralContainers is { Count: > 0 } ||
            pod.Volumes is null || expectedPod.Volumes is null ||
            pod.Volumes.Count != expectedPod.Volumes.Count)
        {
            return false;
        }

        V1Container container = containers[0];
        V1Container expectedContainer = expectedContainers[0];
        V1SecurityContext? security = container.SecurityContext;
        if (container.Image != expectedContainer.Image ||
            container.ImagePullPolicy != expectedContainer.ImagePullPolicy ||
            container.Command?.SequenceEqual(expectedContainer.Command ?? []) != true ||
            container.Args?.SequenceEqual(expectedContainer.Args ?? []) != true ||
            security?.RunAsNonRoot != true || security.RunAsUser != expectedContainer.SecurityContext.RunAsUser ||
            security.AllowPrivilegeEscalation != false || security.ReadOnlyRootFilesystem != true ||
            security.Capabilities?.Drop?.Contains("ALL", StringComparer.Ordinal) != true ||
            container.VolumeMounts is null || expectedContainer.VolumeMounts is null ||
            container.VolumeMounts.Count != expectedContainer.VolumeMounts.Count ||
            !container.VolumeMounts.OrderBy(mount => mount.Name, StringComparer.Ordinal)
                .Select(MountShape)
                .SequenceEqual(expectedContainer.VolumeMounts.OrderBy(mount => mount.Name, StringComparer.Ordinal)
                    .Select(MountShape)))
        {
            return false;
        }

        return pod.Volumes.OrderBy(volume => volume.Name, StringComparer.Ordinal)
            .Select(VolumeShape)
            .SequenceEqual(expectedPod.Volumes.OrderBy(volume => volume.Name, StringComparer.Ordinal)
                .Select(VolumeShape));
    }

    private static (string? Name, string? Path, string? SubPath, bool? ReadOnly) MountShape(V1VolumeMount mount) =>
        (mount.Name, mount.MountPath, mount.SubPath, mount.ReadOnlyProperty);

    private static (string? Name, string? Claim, bool? ClaimReadOnly, string? EmptyDirSize) VolumeShape(V1Volume volume) =>
        (volume.Name, volume.PersistentVolumeClaim?.ClaimName, volume.PersistentVolumeClaim?.ReadOnlyProperty,
            volume.EmptyDir?.SizeLimit?.ToString());

    private static bool SameLabels(
        IDictionary<string, string>? actual,
        IDictionary<string, string>? expected) =>
        expected is not null && actual is not null && expected.Count == actual.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out string? value) && value == pair.Value);

    public void Dispose()
    {
        if (client.IsValueCreated)
        {
            client.Value.Dispose();
        }
    }
}

public sealed class UnavailableAppDeploymentJobClient : IAppDeploymentJobClient
{
    public Task EnsureServiceAccountAsync(V1ServiceAccount serviceAccount, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App deployments require the isolated Kubernetes worker boundary.");

    public Task<AppJourneyOutcome?> RunAsync(V1Job job, V1NetworkPolicy? networkPolicy, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App deployments require the isolated Kubernetes worker boundary.");
}
