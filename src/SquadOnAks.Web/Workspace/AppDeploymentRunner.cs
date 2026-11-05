using System.Net;
using System.Security.Cryptography;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

public sealed record AppDeploymentSettings(
    string CoordinatorNamespace,
    string WorkerImage,
    string WorkspaceMountRoot,
    string WorkspaceClaim,
    string? ImagePullSecret);

public interface IAppDeploymentJobClient
{
    Task EnsureServiceAccountAsync(V1ServiceAccount serviceAccount, CancellationToken cancellationToken);
    Task RunAsync(V1Job job, V1NetworkPolicy? networkPolicy, CancellationToken cancellationToken);
}

public sealed class AppDeploymentJobFailedException(string message) : InvalidOperationException(message);
public sealed class AppDeploymentReconciliationException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

public sealed class KubernetesAppDeploymentRunner(
    AppIdentity identity,
    DisposableFixtureWorkspace workspace,
    AppDeploymentStateStore state,
    AppDeploymentSettings settings,
    IAppDeploymentJobClient jobs,
    IAppNamespaceProvisioner namespaces,
    ILogger<KubernetesAppDeploymentRunner> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AppDeploymentSnapshot> RunAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            AppDeploymentSnapshot snapshot = state.GetSnapshot();
            AppDeploymentRevision? revision = snapshot.Revisions.LastOrDefault(item =>
                item.Phase is AppDeploymentPhase.Building or AppDeploymentPhase.Built or AppDeploymentPhase.Deploying);
            if (revision is null)
            {
                string runId = Guid.NewGuid().ToString("N")[..16];
                state.Begin(runId);
                revision = state.GetSnapshot().Revisions[^1];
            }

            string workspaceSubPath = RelativeSubPath(settings.WorkspaceMountRoot, workspace.RootPath);
            string artifactSubPath = $"{workspaceSubPath}/.git/daydream-artifacts/{identity.Id}";
            string artifactDirectory = workspace.StatePath(Path.Combine("daydream-artifacts", identity.Id));
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
                settings.ImagePullSecret);

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

                if (revision.Phase is AppDeploymentPhase.Built or AppDeploymentPhase.Deploying)
                {
                    await namespaces.EnsureAsync(identity.Id, cancellationToken);
                    await jobs.EnsureServiceAccountAsync(manifests.DeployServiceAccount, cancellationToken);
                }

                if (revision.Phase == AppDeploymentPhase.Built)
                {
                    state.BeginDeploy(revision.RunId);
                    revision = state.GetSnapshot().Revisions[^1];
                }

                if (revision.Phase == AppDeploymentPhase.Deploying)
                {
                    await jobs.RunAsync(manifests.DeployJob, null, cancellationToken);
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
                state.Fail(revision.RunId);
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
    ILogger<KubernetesAppDeploymentJobClient> logger) : IAppDeploymentJobClient, IDisposable
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

    public async Task RunAsync(
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
            await WaitForCompletionAsync(job, timeout.Token);
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
                existing.Spec.Egress is not { Count: 0 } ||
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

    private async Task WaitForCompletionAsync(V1Job job, CancellationToken cancellationToken)
    {
        string namespaceName = job.Metadata.NamespaceProperty;
        while (true)
        {
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
                return;
            }

            if (current.Status?.Failed > 0)
            {
                throw new AppDeploymentJobFailedException(
                    $"The {job.Metadata.Labels[AppWorkerManifest.StageLabel]} worker failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
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
            security?.RunAsNonRoot != true || security.RunAsUser != 10001 ||
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

    public Task RunAsync(V1Job job, V1NetworkPolicy? networkPolicy, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App deployments require the isolated Kubernetes worker boundary.");
}
