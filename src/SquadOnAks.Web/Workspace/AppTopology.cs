using k8s;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

/// <summary>A node in a topology view. <c>Kind</c> drives the node's shape and accent in the UI.</summary>
public sealed record TopologyNode(string Id, string Kind, string Label, string Detail, string? Parent = null);

/// <summary>A connection between two nodes. <c>Style</c> is "solid" (runtime traffic) or "dashed" (build-time).</summary>
public sealed record TopologyEdge(string From, string To, string Label, string Style = "solid");

public sealed record TopologyView(IReadOnlyList<TopologyNode> Nodes, IReadOnlyList<TopologyEdge> Edges);

/// <summary>
/// How an app is (or will be) built in Aspire and how it runs in its cluster namespace.
/// <c>State</c> is "planned" before the first build, "built" once the worker rendered the
/// Aspire chart, and "deployed" once that revision is running in the app's namespace.
/// </summary>
public sealed record AppTopologyLive(bool Available, DateTimeOffset? ObservedAt, IReadOnlyDictionary<string, WorkloadStatus> Nodes);

public sealed record AppTopology(
    string State,
    string Namespace,
    string? Revision,
    TopologyView Aspire,
    TopologyView Cluster);

/// <summary>
/// Builds the topology only from real artifacts: the app contract before a build, then the
/// manifests the worker rendered with <c>helm template</c>. Never reads secret values.
/// </summary>
public sealed class AppTopologyService(
    DisposableFixtureWorkspace workspace,
    AppIdentity identity,
    AppDeploymentStateStore deployments,
    IAppWorkloadStatusReader? live = null)
{
    public const string RenderedFileName = "rendered.yaml";
    public const long MaxRenderedBytes = 1024 * 1024;
    private const string ComponentLabel = "app.kubernetes.io/component";

    public string RenderedPath => workspace.StatePath(Path.Combine("soaks-artifacts", identity.Id, RenderedFileName));

    /// <summary>
    /// Live workload status for a deployed app, keyed like the topology's nodes. Returns
    /// <c>Available: false</c> before deployment, in local mode, or when the cluster cannot be read.
    /// </summary>
    public async Task<AppTopologyLive> GetLiveAsync(CancellationToken cancellationToken)
    {
        bool deployed = deployments.GetSnapshot().Revisions.Any(revision => revision.Phase == AppDeploymentPhase.Deployed);
        if (!deployed || live is null) return new(false, null, new Dictionary<string, WorkloadStatus>());
        AppLiveStatus? status = await live.ReadAsync(AppWorkerManifest.NamespaceName(identity.Id), cancellationToken);
        return status is null
            ? new(false, null, new Dictionary<string, WorkloadStatus>())
            : new(true, status.ObservedAt, status.Nodes);
    }

    public string BuiltComponentsPath => workspace.StatePath(Path.Combine("soaks-artifacts", identity.Id, BuiltComponentsFileName));
    public const string BuiltComponentsFileName = "components.json";

    public AppTopology Get()
    {
        string ns = AppWorkerManifest.NamespaceName(identity.Id);
        AppDeploymentRevision? deployed = deployments.GetSnapshot().Revisions
            .LastOrDefault(revision => revision.Phase == AppDeploymentPhase.Deployed);
        var rendered = new FileInfo(RenderedPath);
        if (!rendered.Exists || rendered.Length is 0 or > MaxRenderedBytes)
            return Planned(ns, DeclaredComponents());

        List<object> objects;
        try
        {
            objects = KubernetesYaml.LoadAllFromString(File.ReadAllText(rendered.FullName));
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidOperationException or ArgumentException)
        {
            return Planned(ns, DeclaredComponents());
        }

        return FromRendered(objects, ns, deployed is null ? "built" : "deployed", deployed?.RunId, BuiltFrontends());
    }

    private AppComponents DeclaredComponents()
    {
        try { return AppBuildManifest.Read(workspace)?.EffectiveComponents ?? AppComponents.Default; }
        catch (InvalidOperationException) { return AppComponents.Default; }
    }

    /// <summary>
    /// The front ends the worker actually built into the rendered revision (front ends have no chart objects of their
    /// own). A build from before components were recorded had the single default front end.
    /// </summary>
    private IReadOnlyList<AppFrontendComponent> BuiltFrontends()
    {
        try
        {
            var file = new FileInfo(BuiltComponentsPath);
            if (!file.Exists || file.Length > 16 * 1024) return AppComponents.Default.Frontends;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file.FullName));
            var frontends = document.RootElement.GetProperty("frontends").EnumerateArray()
                .Select(item => new AppFrontendComponent(
                    item.GetProperty("name").GetString()!, item.GetProperty("path").GetString()!, item.GetProperty("mount").GetString()!))
                .Take(AppComponents.MaxFrontends)
                .ToList();
            return frontends.All(item => System.Text.RegularExpressions.Regex.IsMatch(item.Name, @"\A[a-z][a-z0-9]{0,19}\z") &&
                (item.Mount == "/" || item.Mount == $"/{item.Name}/")) && frontends.Count > 0
                ? frontends : AppComponents.Default.Frontends;
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return AppComponents.Default.Frontends;
        }
    }

    /// <summary>The app's declared shape (see AppBuildPolicy), shown until a real build exists.</summary>
    public static AppTopology Planned(string ns, AppComponents? declared = null)
    {
        AppComponents components = declared ?? AppComponents.Default;
        var aspireNodes = new List<TopologyNode>();
        var aspireEdges = new List<TopologyEdge>();
        foreach (AppFrontendComponent frontend in components.Frontends)
        {
            aspireNodes.Add(new(frontend.Name, "frontend", frontend.Name, "Vite + React"));
            aspireEdges.Add(new(frontend.Name, AppComponents.Gateway, Served(frontend), "dashed"));
        }
        foreach (AppProjectComponent project in components.Projects.OrderBy(project => project.Name != AppComponents.Gateway))
            aspireNodes.Add(new(project.Name, "project", project.Name,
                project.Name == AppComponents.Gateway ? "ASP.NET Core API · entry" : "ASP.NET Core API"));
        foreach (string server in components.Servers)
            aspireNodes.Add(new(server, "database-server", server, "PostgreSQL · " +
                string.Join(", ", components.Databases.Where(database => database.Server == server).Select(database => database.Name))));
        // Before a build only the gateway's own database connection is known for certain.
        aspireEdges.Add(new(AppComponents.Gateway, "db", "WithReference"));

        var clusterNodes = new List<TopologyNode>
        {
            new("squad", "gateway", "Squad", "/online/{app}/ proxy"),
            new("namespace", "namespace", ns, "App namespace")
        };
        var clusterEdges = new List<TopologyEdge> { new("squad", "api-service", "HTTP") };
        foreach (AppProjectComponent project in components.Projects.OrderBy(project => project.Name != AppComponents.Gateway))
        {
            clusterNodes.Add(new($"{project.Name}-service", "service", $"{project.Name}-service", "ClusterIP :8080", "namespace"));
            clusterNodes.Add(new($"{project.Name}-deployment", "deployment", project.Name,
                project.Name == AppComponents.Gateway ? (components.Frontends.Count > 1 ? "Deployment · API + front ends" : "Deployment · API + front end") : "Deployment · API",
                "namespace"));
            clusterEdges.Add(new($"{project.Name}-service", $"{project.Name}-deployment", "selects"));
        }
        foreach (string server in components.Servers)
        {
            clusterNodes.Add(new($"{server}-service", "service", $"{server}-service", "ClusterIP :5432", "namespace"));
            clusterNodes.Add(new($"{server}-statefulset", "statefulset", server, "StatefulSet · PostgreSQL", "namespace"));
            clusterNodes.Add(new($"{server}-data", "volume", $"{server} data", "PersistentVolumeClaim", "namespace"));
            clusterEdges.Add(new($"{server}-service", $"{server}-statefulset", "selects"));
            clusterEdges.Add(new($"{server}-statefulset", $"{server}-data", "mounts"));
        }
        clusterEdges.Add(new("api-deployment", "db-service", "connection string"));
        return new("planned", ns, null, new(aspireNodes, aspireEdges), new(clusterNodes, clusterEdges));
    }

    private static string Served(AppFrontendComponent frontend) =>
        frontend.Mount == "/" ? "built into wwwroot" : $"served at {frontend.Mount}";

    public static AppTopology FromRendered(
        IReadOnlyList<object> objects, string ns, string state, string? revision, IReadOnlyList<AppFrontendComponent>? frontends = null)
    {
        var workloads = objects.OfType<V1Deployment>().Select(item => (Kind: "deployment", item.Metadata, Pod: item.Spec?.Template))
            .Concat(objects.OfType<V1StatefulSet>().Select(item => (Kind: "statefulset", item.Metadata, Pod: item.Spec?.Template)))
            .Where(item => item.Metadata?.Name is { Length: > 0 })
            .ToList();
        var services = objects.OfType<V1Service>().Where(item => item.Metadata?.Name is { Length: > 0 }).ToList();
        var configMaps = objects.OfType<V1ConfigMap>().Where(item => item.Metadata?.Name is { Length: > 0 }).ToList();
        var secrets = objects.OfType<V1Secret>().Where(item => item.Metadata?.Name is { Length: > 0 }).ToList();
        var claims = objects.OfType<V1StatefulSet>()
            .SelectMany(item => (item.Spec?.VolumeClaimTemplates ?? []).Select(claim => (Owner: item.Metadata.Name, Claim: claim)))
            .Where(item => item.Claim.Metadata?.Name is { Length: > 0 })
            .ToList();

        string Component(V1ObjectMeta metadata) =>
            metadata.Labels?.TryGetValue(ComponentLabel, out string? value) == true && value.Length > 0 ? value : metadata.Name;

        // Aspire view: one node per Aspire resource (chart component), connections from service references.
        var components = workloads.Select(item => (Name: Component(item.Metadata), item.Kind, Image: FirstImage(item.Pod)))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var aspireNodes = new List<TopologyNode>();
        var aspireEdges = new List<TopologyEdge>();
        // Logical databases come from the projects' rendered connection settings (<PREFIX>_HOST and <PREFIX>_DATABASENAME).
        var logical = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (V1ConfigMap map in configMaps)
        {
            if (map.Data is null) continue;
            foreach (var (key, value) in map.Data)
            {
                if (!key.EndsWith("_DATABASENAME", StringComparison.Ordinal) || string.IsNullOrEmpty(value)) continue;
                string prefix = key[..^"_DATABASENAME".Length];
                if (map.Data.TryGetValue(prefix + "_HOST", out string? host) && host.EndsWith("-service", StringComparison.Ordinal))
                {
                    string server = host[..^"-service".Length];
                    if (!logical.TryGetValue(server, out SortedSet<string>? names)) logical[server] = names = new(StringComparer.Ordinal);
                    names.Add(value);
                }
            }
        }
        foreach (var component in components)
        {
            bool database = component.Kind == "statefulset" ||
                component.Image?.Contains("postgres", StringComparison.OrdinalIgnoreCase) == true;
            string detail = database
                ? "PostgreSQL" + (logical.TryGetValue(component.Name, out SortedSet<string>? names) ? " · " + string.Join(", ", names) : "")
                : component.Kind == "deployment" ? (component.Name == AppComponents.Gateway ? "Project · entry" : "Project") : "Container";
            aspireNodes.Add(new(component.Name, database ? "database-server" : "project", component.Name, detail));
        }
        // Front ends are published into the gateway project, so they have no chart resource of their own.
        if (components.Any(component => component.Name == AppComponents.Gateway))
        {
            int at = 0;
            foreach (AppFrontendComponent frontend in frontends ?? AppComponents.Default.Frontends)
            {
                aspireNodes.Insert(at++, new(frontend.Name, "frontend", frontend.Name, "Vite + React"));
                aspireEdges.Add(new(frontend.Name, AppComponents.Gateway, Served(frontend), "dashed"));
            }
        }

        var references = new List<(string From, string To)>();
        foreach (var component in components)
        {
            string text = ReferenceText(component.Name, workloads, configMaps);
            foreach (var other in components.Where(other => other.Name != component.Name))
            {
                if (text.Contains(other.Name + "-service", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Host=" + other.Name, StringComparison.OrdinalIgnoreCase))
                    references.Add((component.Name, other.Name));
            }
        }
        aspireEdges.AddRange(references.Select(reference => new TopologyEdge(reference.From, reference.To, "WithReference")));

        // Cluster view: the namespace and the objects the chart creates in it (names only).
        var clusterNodes = new List<TopologyNode>
        {
            new("squad", "gateway", "Squad", "/online/{app}/ proxy"),
            new("namespace", "namespace", ns, "App namespace")
        };
        var clusterEdges = new List<TopologyEdge>();
        foreach (var workload in workloads)
        {
            string image = FirstImage(workload.Pod) is { } value ? ImageName(value) : "";
            clusterNodes.Add(new(Id("workload", workload.Metadata.Name), workload.Kind, workload.Metadata.Name,
                (workload.Kind == "statefulset" ? "StatefulSet" : "Deployment") + (image.Length > 0 ? " · " + image : ""), "namespace"));
            foreach (string reference in EnvSources(workload.Pod))
            {
                if (configMaps.Any(item => item.Metadata.Name == reference))
                    clusterEdges.Add(new(Id("workload", workload.Metadata.Name), Id("config", reference), "env", "dashed"));
                if (secrets.Any(item => item.Metadata.Name == reference))
                    clusterEdges.Add(new(Id("workload", workload.Metadata.Name), Id("secret", reference), "env", "dashed"));
            }
        }
        foreach (V1Service service in services)
        {
            string port = service.Spec?.Ports?.FirstOrDefault()?.Port is int number ? $" :{number}" : "";
            clusterNodes.Add(new(Id("service", service.Metadata.Name), "service", service.Metadata.Name,
                (service.Spec?.Type ?? "ClusterIP") + port, "namespace"));
            foreach (var workload in workloads.Where(workload => Selects(service, workload.Pod)))
                clusterEdges.Add(new(Id("service", service.Metadata.Name), Id("workload", workload.Metadata.Name), "selects"));
        }
        clusterNodes.AddRange(configMaps.Select(item => new TopologyNode(Id("config", item.Metadata.Name), "config", item.Metadata.Name, "ConfigMap", "namespace")));
        clusterNodes.AddRange(secrets.Select(item => new TopologyNode(Id("secret", item.Metadata.Name), "secret", item.Metadata.Name, "Secret", "namespace")));
        foreach (var (owner, claim) in claims)
        {
            clusterNodes.Add(new(Id("volume", owner + "-" + claim.Metadata.Name), "volume", claim.Metadata.Name, "PersistentVolumeClaim", "namespace"));
            clusterEdges.Add(new(Id("workload", owner), Id("volume", owner + "-" + claim.Metadata.Name), "mounts"));
        }
        // Runtime traffic between components flows through the referenced component's Service.
        foreach (var (from, to) in references)
        {
            string? source = workloads.FirstOrDefault(item => Component(item.Metadata) == from).Metadata?.Name;
            V1Service? target = services.FirstOrDefault(item => Component(item.Metadata) == to);
            if (source is not null && target is not null)
                clusterEdges.Add(new(Id("workload", source), Id("service", target.Metadata.Name), "connection"));
        }
        V1Service? entry = services.FirstOrDefault(item => Component(item.Metadata) == AppComponents.Gateway) ?? services.FirstOrDefault();
        if (entry is not null)
            clusterEdges.Insert(0, new("squad", Id("service", entry.Metadata.Name), "HTTP"));

        return new(state, ns, revision, new(aspireNodes, aspireEdges), new(clusterNodes, clusterEdges));
    }

    private static string Id(string kind, string name) => kind + ":" + name;

    private static string? FirstImage(V1PodTemplateSpec? pod) => pod?.Spec?.Containers?.FirstOrDefault()?.Image;

    /// <summary>The repository part of an image reference, without registry, tag or digest.</summary>
    private static string ImageName(string image)
    {
        string name = image.Split('@')[0];
        int slash = name.LastIndexOf('/');
        name = slash >= 0 ? name[(slash + 1)..] : name;
        int colon = name.IndexOf(':');
        return colon >= 0 ? name[..colon] : name;
    }

    private static IEnumerable<string> EnvSources(V1PodTemplateSpec? pod) =>
        (pod?.Spec?.Containers ?? []).SelectMany(container => container.EnvFrom ?? [])
            .Select(source => source.ConfigMapRef?.Name ?? source.SecretRef?.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

    /// <summary>Env and ConfigMap text a component can use to reach others; Secret values are never read.</summary>
    private static string ReferenceText(
        string component,
        IEnumerable<(string Kind, V1ObjectMeta Metadata, V1PodTemplateSpec? Pod)> workloads,
        IEnumerable<V1ConfigMap> configMaps)
    {
        var parts = new List<string>();
        foreach (var workload in workloads.Where(item =>
            (item.Metadata.Labels is { } labels && labels.TryGetValue(ComponentLabel, out string? name) ? name : item.Metadata.Name) == component))
        {
            foreach (V1Container container in workload.Pod?.Spec?.Containers ?? Enumerable.Empty<V1Container>())
                parts.AddRange((container.Env ?? Enumerable.Empty<V1EnvVar>()).Select(env => env.Value ?? ""));
            foreach (string source in EnvSources(workload.Pod))
                parts.AddRange(configMaps.Where(item => item.Metadata.Name == source)
                    .SelectMany(item => item.Data?.Values ?? []));
        }
        return string.Join('\n', parts);
    }

    private static bool Selects(V1Service service, V1PodTemplateSpec? pod) =>
        service.Spec?.Selector is { Count: > 0 } selector && pod?.Metadata?.Labels is { } labels &&
        selector.All(pair => labels.TryGetValue(pair.Key, out string? value) && value == pair.Value);
}
