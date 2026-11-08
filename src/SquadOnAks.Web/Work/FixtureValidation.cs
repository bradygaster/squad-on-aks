using System.Diagnostics;
using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed record FixtureValidationResult(int ExitCode, string Output);

public interface IFixtureValidationRunner
{
    Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace,
        string appId,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public interface IFixtureValidationJobClient
{
    string Namespace { get; }
    Task CreateAsync(V1Job job, CancellationToken cancellationToken);
    Task<V1Job?> ReadAsync(string name, CancellationToken cancellationToken);
    Task<string> ReadLogsAsync(string jobName, CancellationToken cancellationToken);
    Task DeleteAsync(string name, CancellationToken cancellationToken);
}

public sealed record FixtureValidationSettings(
    string WorkerImage,
    string WorkspaceMountRoot,
    string WorkspaceClaim,
    string? ImagePullSecret);

public sealed class KubernetesFixtureValidationRunner(
    IFixtureValidationJobClient client,
    FixtureValidationSettings settings,
    ILogger<KubernetesFixtureValidationRunner> logger) : IFixtureValidationRunner
{
    public async Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace,
        string appId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        string subPath = Path.GetRelativePath(settings.WorkspaceMountRoot, workspace.RootPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (subPath == "." || subPath == ".." ||
            subPath.StartsWith("../", StringComparison.Ordinal) ||
            Path.IsPathRooted(subPath))
        {
            throw new InvalidOperationException("The app workspace is outside the worker mount.");
        }

        string jobName = $"daydream-validate-{Guid.NewGuid():N}";
        V1Job job = AppWorkerManifest.CreateValidationJob(
            jobName, appId, client.Namespace, settings.WorkerImage, settings.WorkspaceClaim,
            subPath, settings.ImagePullSecret);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);

        await client.CreateAsync(job, bounded.Token);
        try
        {
            while (true)
            {
                bounded.Token.ThrowIfCancellationRequested();
                V1Job? current = await client.ReadAsync(jobName, bounded.Token);
                if (current?.Status?.Succeeded > 0)
                {
                    return new(0, await client.ReadLogsAsync(jobName, bounded.Token));
                }

                if (current?.Status?.Failed > 0)
                {
                    string output = await client.ReadLogsAsync(jobName, bounded.Token);
                    return new(1, output);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), bounded.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Fixture validation job {JobName} exceeded its execution limit.", jobName);
            throw new TimeoutException("Isolated fixture validation exceeded its execution limit.");
        }
        finally
        {
            await client.DeleteAsync(jobName, CancellationToken.None);
        }
    }
}

public sealed class UnavailableFixtureValidationRunner : IFixtureValidationRunner
{
    public Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace,
        string appId,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Isolated fixture validation is not configured for this host.");
}

public sealed class LocalFixtureValidationRunner : IFixtureValidationRunner
{
    public async Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace,
        string appId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("sh")
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("tests/run.sh");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start fixture tests.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(bounded.Token);
        Task<string> standardError = process.StandardError.ReadToEndAsync(bounded.Token);
        try
        {
            await process.WaitForExitAsync(bounded.Token);
            await Task.WhenAll(standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }

            using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(reap.Token);
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Fixture validation exceeded its execution limit.");
            }

            throw;
        }

        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.TrimEnd()));
        return new(process.ExitCode, output);
    }
}

public sealed class FixtureValidationJobClient : IFixtureValidationJobClient, IDisposable
{
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));

    public string Namespace => File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/namespace").Trim();

    public async Task CreateAsync(V1Job job, CancellationToken cancellationToken)
    {
        string runId = job.Metadata.Labels[AppWorkerManifest.WorkerRunLabel];
        string policyName = $"{job.Metadata.Name}-deny-egress";
        var policy = new V1NetworkPolicy
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "NetworkPolicy",
            Metadata = new V1ObjectMeta
            {
                Name = policyName,
                NamespaceProperty = Namespace,
                Labels = new Dictionary<string, string>
                {
                    [AppWorkerManifest.OwnerLabel] = AppWorkerManifest.Owner,
                    [AppWorkerManifest.WorkerRunLabel] = runId
                }
            },
            Spec = new V1NetworkPolicySpec
            {
                PodSelector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        [AppWorkerManifest.WorkerRunLabel] = runId
                    }
                },
                PolicyTypes = ["Egress"],
                Egress = []
            }
        };
        await client.Value.NetworkingV1.CreateNamespacedNetworkPolicyAsync(
            policy, Namespace, cancellationToken: cancellationToken);
        try
        {
            await client.Value.BatchV1.CreateNamespacedJobAsync(job, Namespace, cancellationToken: cancellationToken);
        }
        catch
        {
            await DeleteNetworkPolicyAsync(policyName, CancellationToken.None);
            throw;
        }
    }

    public async Task<V1Job?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.BatchV1.ReadNamespacedJobAsync(name, Namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string> ReadLogsAsync(string jobName, CancellationToken cancellationToken)
    {
        V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(
            Namespace,
            labelSelector: $"batch.kubernetes.io/job-name={jobName}",
            cancellationToken: cancellationToken);
        V1Pod pod = pods.Items.SingleOrDefault()
            ?? throw new InvalidOperationException("The validation job completed without a worker pod.");
        await using Stream log = await client.Value.CoreV1.ReadNamespacedPodLogAsync(
            pod.Metadata.Name, Namespace, cancellationToken: cancellationToken);
        using var reader = new StreamReader(log);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await client.Value.BatchV1.DeleteNamespacedJobAsync(
                name, Namespace, propagationPolicy: "Foreground", cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            V1PodList pods = await client.Value.CoreV1.ListNamespacedPodAsync(
                Namespace,
                labelSelector: $"{AppWorkerManifest.WorkerRunLabel}={name}",
                cancellationToken: cancellationToken);
            if (pods.Items.Count == 0)
            {
                await DeleteNetworkPolicyAsync($"{name}-deny-egress", cancellationToken);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException(
            $"Validation worker pods for job {name} did not terminate; its deny-egress policy was retained.");
    }

    private async Task DeleteNetworkPolicyAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await client.Value.NetworkingV1.DeleteNamespacedNetworkPolicyAsync(
                name, Namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    public void Dispose()
    {
        if (client.IsValueCreated)
        {
            client.Value.Dispose();
        }
    }
}
