using k8s;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

/// <summary>
/// Live state of one workload: "ready", "starting", "failing" or "stopped", with a short detail
/// built only from replica counts, Kubernetes reason codes and restart counts (never log text or
/// event messages).
/// </summary>
public sealed record WorkloadStatus(string State, string Detail);

public sealed record AppLiveStatus(DateTimeOffset ObservedAt, IReadOnlyDictionary<string, WorkloadStatus> Nodes);

public interface IAppWorkloadStatusReader
{
    /// <summary>Null when live status is unavailable (local mode, no access, or the cluster did not answer).</summary>
    Task<AppLiveStatus?> ReadAsync(string appNamespace, CancellationToken cancellationToken);
}

public sealed class UnavailableAppWorkloadStatusReader : IAppWorkloadStatusReader
{
    public Task<AppLiveStatus?> ReadAsync(string appNamespace, CancellationToken cancellationToken) =>
        Task.FromResult<AppLiveStatus?>(null);
}

/// <summary>
/// Reads Deployments, StatefulSets and Pods in an app namespace through the read-only
/// <c>daydream-app-observer</c> role. Results are cached briefly so polling pages do not
/// multiply cluster reads.
/// </summary>
public sealed class KubernetesAppWorkloadStatusReader(ILogger<KubernetesAppWorkloadStatusReader> logger)
    : IAppWorkloadStatusReader, IDisposable
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(3);
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));
    private readonly Dictionary<string, AppLiveStatus> cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AppLiveStatus?> ReadAsync(string appNamespace, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(appNamespace, out AppLiveStatus? cached) &&
                DateTimeOffset.UtcNow - cached.ObservedAt < CacheFor)
                return cached;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            V1DeploymentList deployments = await client.Value.AppsV1.ListNamespacedDeploymentAsync(appNamespace, cancellationToken: timeout.Token);
            V1StatefulSetList statefulSets = await client.Value.AppsV1.ListNamespacedStatefulSetAsync(appNamespace, cancellationToken: timeout.Token);
            V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(appNamespace, cancellationToken: timeout.Token);
            var status = new AppLiveStatus(DateTimeOffset.UtcNow,
                AppWorkloadStatusCalculator.Calculate(deployments.Items, statefulSets.Items, pods.Items));
            cache[appNamespace] = status;
            return status;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Live status for {Namespace} is unavailable.", appNamespace);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (client.IsValueCreated) client.Value.Dispose();
        gate.Dispose();
    }
}

public static class AppWorkloadStatusCalculator
{
    private const string ComponentLabel = "app.kubernetes.io/component";

    /// <summary>Container waiting reasons that mean the workload will not become ready on its own.</summary>
    private static readonly HashSet<string> FailingReasons = new(StringComparer.Ordinal)
    {
        "CrashLoopBackOff", "ImagePullBackOff", "ErrImagePull", "InvalidImageName",
        "CreateContainerConfigError", "CreateContainerError", "RunContainerError"
    };

    /// <summary>
    /// Status keyed by cluster node id (<c>workload:{name}</c>) and by Aspire resource name
    /// (the workload's <c>app.kubernetes.io/component</c> label), matching <see cref="AppTopology"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, WorkloadStatus> Calculate(
        IEnumerable<V1Deployment> deployments,
        IEnumerable<V1StatefulSet> statefulSets,
        IEnumerable<V1Pod> pods)
    {
        V1Pod[] allPods = pods.ToArray();
        var workloads = deployments.Select(item => (item.Metadata, Desired: item.Spec?.Replicas ?? 1,
                Ready: item.Status?.ReadyReplicas ?? 0, Selector: item.Spec?.Selector?.MatchLabels))
            .Concat(statefulSets.Select(item => (item.Metadata, Desired: item.Spec?.Replicas ?? 1,
                Ready: item.Status?.ReadyReplicas ?? 0, Selector: item.Spec?.Selector?.MatchLabels)))
            .Where(item => item.Metadata?.Name is { Length: > 0 });

        var result = new Dictionary<string, WorkloadStatus>(StringComparer.Ordinal);
        foreach (var workload in workloads)
        {
            V1Pod[] owned = workload.Selector is { Count: > 0 } selector
                ? allPods.Where(pod => pod.Metadata?.Labels is { } labels &&
                    selector.All(pair => labels.TryGetValue(pair.Key, out string? value) && value == pair.Value)).ToArray()
                : [];
            V1ContainerStatus[] containers = owned.SelectMany(pod => pod.Status?.ContainerStatuses ?? []).ToArray();
            int restarts = containers.Sum(container => container.RestartCount);
            string? failing = containers.Select(container => container.State?.Waiting?.Reason)
                .FirstOrDefault(reason => reason is not null && FailingReasons.Contains(reason));
            bool pending = owned.Any(pod => pod.Status?.Phase == "Pending");

            string counts = $"{workload.Ready}/{workload.Desired} ready";
            string restartText = restarts == 1 ? "1 restart" : $"{restarts} restarts";
            WorkloadStatus status =
                workload.Desired == 0 ? new("stopped", "Scaled to 0")
                : failing is not null ? new("failing", $"{counts} · {failing}" + (restarts > 0 ? $" · {restartText}" : ""))
                : workload.Ready >= workload.Desired ? new("ready", counts + (restarts > 0 ? $" · {restartText}" : ""))
                : new("starting", counts + (pending ? " · Pending" : ""));

            result["workload:" + workload.Metadata.Name] = status;
            if (workload.Metadata.Labels?.TryGetValue(ComponentLabel, out string? component) == true && component.Length > 0)
                result[component] = status;
        }
        return result;
    }
}
