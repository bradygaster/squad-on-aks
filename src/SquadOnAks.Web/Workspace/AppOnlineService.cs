using System.Net;
using System.Text.Json;
using k8s;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

public sealed record OnlineHostingSettings(bool Available)
{
    public string Mode => Available ? "cluster" : "local";
}
public sealed record AppOnlineStatus(bool Available, bool Online, string? Url, string Message);
public sealed record AppOnlineState(bool Requested, string Message, DateTimeOffset UpdatedAt, bool Online = false);

public sealed class AppOnlineService(
    AppIdentity identity, OnlineHostingSettings hosting, IOnlineCluster cluster,
    DisposableFixtureWorkspace workspace, WorkspaceArtifacts artifacts,
    AppDeploymentStateStore deployments, KubernetesAppDeploymentRunner deploymentRunner,
    IDeployedAppReadinessProbe readinessProbe,
    ILogger<AppOnlineService> logger)
{
    public const string LocalMessage = "Online publishing is unavailable in this local installation. Previews and ZIP downloads are still available.";
    private readonly SemaphoreSlim gate = new(1, 1);
    private string StatePath => workspace.StatePath("soaks-online.json");
    private string Name => StaticAppManifest.Name(identity.Id);
    private AppOnlineStatus Status(bool online, string message) =>
        new(hosting.Available, online, online ? $"/online/{identity.Id}/" : null, message);

    public async Task<AppOnlineStatus> GetAsync(CancellationToken cancellationToken)
    {
        if (!hosting.Available) return Status(false, LocalMessage);
        // Putting an app online holds the gate for the whole build and deploy (minutes). Status reads
        // must not queue behind it: report the last saved state until that work finishes.
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            AppOnlineState? saved = Load();
            return Status(saved?.Online == true, saved?.Message ?? "Your app is getting ready. Try again shortly.");
        }
        try { return await GetCoreAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task<AppOnlineStatus> GetCoreAsync(CancellationToken cancellationToken)
    {
        AppOnlineState? state = File.Exists(StatePath)
            ? JsonSerializer.Deserialize<AppOnlineState>(await File.ReadAllTextAsync(StatePath, cancellationToken))
                ?? throw new InvalidOperationException("The app's online state could not be read.")
            : null;
        if (state?.Requested != true) return Status(false, state?.Message ?? "This app is not online yet.");
        if (artifacts.IsAspireApp()) return await GetAspireCoreAsync(state, cancellationToken);

        V1Deployment? deployment = await OwnedAsync("Deployment", cancellationToken) as V1Deployment;
        V1Service? service = await OwnedAsync("Service", cancellationToken) as V1Service;
        if (deployment?.Status?.Conditions?.Any(condition =>
            condition.Type == "Progressing" && condition.Status == "False" ||
            condition.Type == "ReplicaFailure" && condition.Status == "True") == true)
            return PersistObserved(false, "Your app could not get ready. Try putting it online again.", state);
        bool ready = deployment?.Status is { AvailableReplicas: >= 1, UpdatedReplicas: >= 1 } status &&
            status.UpdatedReplicas == status.Replicas &&
            status.AvailableReplicas == status.Replicas &&
            status.ObservedGeneration >= deployment.Metadata.Generation && SafeService(service);
        return PersistObserved(ready, ready ? "Your app is online." : "Your app is getting ready. Try again shortly.", state);
    }

    public async Task<AppOnlineStatus> PutAsync(CancellationToken cancellationToken)
    {
        if (!hosting.Available) throw new InvalidOperationException(LocalMessage);
        if (workspace.IsPlainRepository)
            throw new InvalidOperationException("This repository isn't set up to run on your cluster yet. Ask your team in a wave to make it deployable with Squad, then put it online.");
        bool aspire = artifacts.IsAspireApp();
        if (!aspire && artifacts.IsServerBacked())
            throw new InvalidOperationException("This app uses an older shape that can't be deployed. Ask your team to rebuild it as an Aspire app.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (aspire)
            {
                // An update keeps the running revision online until the new one is deployed and ready.
                AppOnlineState? current = Load();
                Save(current?.Online == true
                    ? current with { Requested = true, Message = "Your app is online. An update is being deployed.", UpdatedAt = DateTimeOffset.UtcNow }
                    : new(true, "Your app is getting ready. Try again shortly.", DateTimeOffset.UtcNow));
                await deploymentRunner.RunAsync(cancellationToken);
                return await GetCoreAsync(cancellationToken);
            }

            StaticAppManifest manifest = StaticAppManifest.Create(identity.Id, cluster.Namespace, artifacts.ReadOnlineFiles());
            IKubernetesObject<V1ObjectMeta>[] desired = [manifest.Content, manifest.NetworkPolicy, manifest.Service, manifest.Deployment];
            // Check every destination before making the first change.
            foreach (IKubernetesObject<V1ObjectMeta> resource in desired)
            {
                IKubernetesObject<V1ObjectMeta>? existing = await OwnedAsync(resource.Kind, cancellationToken);
                resource.Metadata.ResourceVersion = existing?.Metadata.ResourceVersion;
                if (resource is V1Service service && existing is V1Service current)
                {
                    service.Spec.ClusterIP = current.Spec.ClusterIP;
                    service.Spec.ClusterIPs = current.Spec.ClusterIPs;
                }
            }
            Save(new(true, "Your app is getting ready. Try again shortly.", DateTimeOffset.UtcNow));
            foreach (IKubernetesObject<V1ObjectMeta> resource in desired)
                await cluster.ApplyAsync(resource, cancellationToken);
            return await GetCoreAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not put app {AppId} online.", identity.Id);
            string failure = AppDeploymentFailure.Message((exception as AppDeploymentJobFailedException)?.Reason)
                ?? "Your app could not go online. Please try again.";
            // A failed update never replaced the running revision: keep that one online.
            if (aspire && Load() is { Online: true } running &&
                deployments.GetSnapshot().Revisions.Any(revision => revision.Phase == AppDeploymentPhase.Deployed))
                Save(running with { Requested = true, Message = "Your app is online. " + failure, UpdatedAt = DateTimeOffset.UtcNow });
            else
                Save(new(false, failure, DateTimeOffset.UtcNow));
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task<AppOnlineStatus> RemoveAsync(CancellationToken cancellationToken)
    {
        if (!hosting.Available) throw new InvalidOperationException(LocalMessage);
        if (artifacts.IsAspireApp())
            throw new InvalidOperationException("Removing an Aspire app requires its scoped deployment worker.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var resources = new List<IKubernetesObject<V1ObjectMeta>>();
            foreach (string kind in new[] { "Service", "Deployment", "ConfigMap", "NetworkPolicy" })
                if (await OwnedAsync(kind, cancellationToken) is { } resource) resources.Add(resource);
            foreach (IKubernetesObject<V1ObjectMeta> resource in resources)
                await cluster.DeleteAsync(resource, cancellationToken);
            Save(new(false, "Your app is no longer online.", DateTimeOffset.UtcNow));
            return Status(false, "Your app is no longer online.");
        }
        finally { gate.Release(); }
    }

    public async Task<Uri?> GetDestinationAsync(CancellationToken cancellationToken)
    {
        if (!(await GetAsync(cancellationToken)).Online) return null;
        if (artifacts.IsAspireApp())
        {
            AppDeploymentSnapshot snapshot = deployments.GetSnapshot();
            return new UriBuilder("http", $"api-service.{snapshot.Namespace}.svc.cluster.local", 8080).Uri;
        }

        var service = await OwnedAsync("Service", cancellationToken) as V1Service;
        return SafeService(service) ? new UriBuilder("http", service!.Spec.ClusterIP, 80).Uri : null;
    }

    private async Task<AppOnlineStatus> GetAspireCoreAsync(
        AppOnlineState state,
        CancellationToken cancellationToken)
    {
        AppDeploymentSnapshot snapshot = deployments.GetSnapshot();
        AppDeploymentRevision? latest = snapshot.Revisions.LastOrDefault();
        // An update that is still in progress, or that failed or was held back by its check, never replaced the
        // running revision (the check uses a throwaway copy and helm --atomic rolls back), so the app stays online.
        bool previousRunning = snapshot.Revisions.Any(revision => revision.Phase == AppDeploymentPhase.Deployed);
        if (latest?.Phase != AppDeploymentPhase.Deployed && !previousRunning)
        {
            string message = latest?.Phase == AppDeploymentPhase.Failed
                ? "Your app could not get ready. Try putting it online again."
                : "Your app is getting ready. Try again shortly.";
            return PersistObserved(false, message, state);
        }

        var origin = new UriBuilder("http", $"api-service.{snapshot.Namespace}.svc.cluster.local", 8080).Uri;
        IReadOnlyList<string> healthPaths = AppBuildManifest.Read(workspace)?.Runtime?.HealthPaths
            ?? DeployedAppReadinessProbe.DefaultHealthPaths;
        bool ready = await readinessProbe.IsReadyAsync(origin, healthPaths, cancellationToken);
        return PersistObserved(
            ready,
            ready ? "Your app is online." : "Your app is getting ready. Try again shortly.",
            state);
    }

    private bool SafeService(V1Service? service) =>
        service?.Spec is { Type: "ClusterIP", ExternalName: null } spec &&
        IPAddress.TryParse(spec.ClusterIP, out IPAddress? address) &&
        !IPAddress.IsLoopback(address) && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) &&
        spec.Selector?.Count == 2 &&
        StaticAppManifest.Labels(identity.Id).All(pair => spec.Selector.TryGetValue(pair.Key, out string? value) && value == pair.Value) &&
        spec.Ports is { Count: 1 } && spec.Ports[0].Port == 80 && spec.Ports[0].TargetPort.ToString() == "8080";

    private async Task<IKubernetesObject<V1ObjectMeta>?> OwnedAsync(string kind, CancellationToken cancellationToken)
    {
        IKubernetesObject<V1ObjectMeta>? value = await cluster.ReadAsync(kind, Name, cancellationToken);
        if (value is not null && !StaticAppManifest.IsOwned(value.Metadata, identity.Id))
            throw new InvalidOperationException("An existing app resource is not owned by this app.");
        return value;
    }

    private void Save(AppOnlineState state)
    {
        string temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, StatePath, overwrite: true);
    }

    private AppOnlineState? Load()
    {
        try
        {
            return File.Exists(StatePath) ? JsonSerializer.Deserialize<AppOnlineState>(File.ReadAllText(StatePath)) : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    private AppOnlineStatus PersistObserved(bool online, string message, AppOnlineState state)
    {
        if (state.Online != online || state.Message != message)
            Save(state with { Online = online, Message = message, UpdatedAt = DateTimeOffset.UtcNow });
        return Status(online, message);
    }
}
