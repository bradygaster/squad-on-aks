using System.Text.Json;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Workspace;

/// <summary>
/// The topology view is drawn only from real artifacts. Fixtures/aspire-rendered.yaml is
/// `helm template` output of an actual `aspire publish` of the contract's app shape
/// (AppHost + api + PostgreSQL via AppHost/SquadHosting.cs).
/// </summary>
internal static class TopologyTests
{
    public static void Run(string repositoryRoot, string fixtureSource)
    {
        FixtureRoot = fixtureSource;
        string rendered = File.ReadAllText(Path.Combine(repositoryRoot, "test", "SquadOnAks.WorkspaceTests", "Fixtures", "aspire-rendered.yaml"));
        const string ns = "soaks-app-first";
        AppTopology topology = AppTopologyService.FromRendered(KubernetesYaml.LoadAllFromString(rendered), ns, "built", null);

        Require(topology.State == "built" && topology.Namespace == ns, "A rendered chart produces a built topology for the app's namespace.");
        Require(Ids(topology.Aspire).SequenceEqual(["frontend", "api", "db"]) &&
            topology.Aspire.Nodes.Single(node => node.Id == "db").Kind == "database-server" &&
            topology.Aspire.Nodes.Single(node => node.Id == "api").Kind == "project",
            "The Aspire view shows the front end, the API project and the PostgreSQL server.");
        Require(topology.Aspire.Edges.Any(edge => edge is { From: "frontend", To: "api", Style: "dashed" }) &&
            topology.Aspire.Edges.Any(edge => edge is { From: "api", To: "db", Label: "WithReference" }) &&
            !topology.Aspire.Edges.Any(edge => edge.From == "db"),
            "Aspire connections come from the API's configured reference to the database service.");

        string[] cluster = Ids(topology.Cluster);
        Require(new[]
            {
                "squad", "namespace", "workload:api-deployment", "workload:db-statefulset", "service:api-service",
                "service:db-service", "config:api-config", "secret:db-secrets"
            }.All(cluster.Contains) &&
            topology.Cluster.Nodes.Any(node => node.Kind == "volume") &&
            topology.Cluster.Nodes.Where(node => node.Id is not ("squad" or "namespace")).All(node => node.Parent == "namespace"),
            "The cluster view shows every chart object inside the app's namespace, including the database volume.");
        Require(topology.Cluster.Edges.Any(edge => edge is { From: "squad", To: "service:api-service" }) &&
            topology.Cluster.Edges.Any(edge => edge is { From: "service:api-service", To: "workload:api-deployment", Label: "selects" }) &&
            topology.Cluster.Edges.Any(edge => edge is { From: "service:db-service", To: "workload:db-statefulset" }) &&
            topology.Cluster.Edges.Any(edge => edge is { From: "workload:api-deployment", To: "service:db-service", Label: "connection" }) &&
            topology.Cluster.Edges.Any(edge => edge.From == "workload:db-statefulset" && edge.Label == "mounts"),
            "Cluster connections follow Service selectors, the database reference and the volume mount.");
        Require(topology.Cluster.Nodes.Single(node => node.Id == "workload:db-statefulset").Detail == "StatefulSet · postgres",
            "Images are shown by repository name only, without registry, tag or digest.");

        string json = JsonSerializer.Serialize(topology);
        Require(!json.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
            !json.Contains("postgresql://", StringComparison.Ordinal) &&
            !json.Contains("scram", StringComparison.Ordinal),
            "The topology never carries secret or configuration values.");

        AppTopology planned = AppTopologyService.Planned(ns);
        Require(planned.State == "planned" && planned.Revision is null &&
            Ids(planned.Aspire).SequenceEqual(["frontend", "api", "db"]) &&
            planned.Cluster.Nodes.Single(node => node.Kind == "namespace").Label == ns,
            "Before a build, the planned topology shows the contract's shape in the app's namespace.");

        MultiComponentChecks(repositoryRoot, ns);
        ServiceChecks(fixtureSource, rendered);
        LiveStatusChecks();
        Console.WriteLine("PASS topology views come from the app contract and rendered Aspire manifests, never secret values");
    }

    /// <summary>
    /// Fixtures/aspire-rendered-multi.yaml is `helm template` output of a real `aspire publish` of a gateway ("api") that
    /// references a second API ("catalog") and PostgreSQL "db"/appdb, while "catalog" owns "ordersserver"/ordersdb.
    /// </summary>
    private static void MultiComponentChecks(string repositoryRoot, string ns)
    {
        string rendered = File.ReadAllText(Path.Combine(repositoryRoot, "test", "SquadOnAks.WorkspaceTests", "Fixtures", "aspire-rendered-multi.yaml"));
        AppTopology topology = AppTopologyService.FromRendered(KubernetesYaml.LoadAllFromString(rendered), ns, "deployed", "run1",
            [new("frontend", "frontend", "/"), new("admin", "admin", "/admin/")]);
        Require(Ids(topology.Aspire).OrderBy(id => id).SequenceEqual(new[] { "admin", "api", "catalog", "db", "frontend", "ordersserver" }),
            "The Aspire view has one node per built front end, project and PostgreSQL server, and nothing else.");
        Require(topology.Aspire.Edges.Any(edge => edge is { From: "admin", To: "api", Label: "served at /admin/", Style: "dashed" }) &&
            topology.Aspire.Edges.Any(edge => edge is { From: "frontend", To: "api", Label: "built into wwwroot" }) &&
            topology.Aspire.Edges.Any(edge => edge is { From: "api", To: "catalog", Label: "WithReference" }) &&
            topology.Aspire.Edges.Any(edge => edge is { From: "api", To: "db" }) &&
            topology.Aspire.Edges.Any(edge => edge is { From: "catalog", To: "ordersserver" }) &&
            !topology.Aspire.Edges.Any(edge => edge is { From: "api", To: "ordersserver" }) &&
            !topology.Aspire.Edges.Any(edge => edge is { From: "catalog", To: "db" }),
            "Aspire connections follow each project's real references: the gateway to catalog and db, catalog to its own server.");
        Require(topology.Aspire.Nodes.Single(node => node.Id == "db").Detail == "PostgreSQL · appdb" &&
            topology.Aspire.Nodes.Single(node => node.Id == "ordersserver").Detail == "PostgreSQL · ordersdb" &&
            topology.Aspire.Nodes.Single(node => node.Id == "api").Detail == "Project · entry",
            "Database servers list their logical databases, and the gateway is marked as the entry.");
        string[] cluster = Ids(topology.Cluster);
        Require(new[]
            {
                "workload:api-deployment", "workload:catalog-deployment", "workload:db-statefulset", "workload:ordersserver-statefulset",
                "service:api-service", "service:catalog-service", "service:db-service", "service:ordersserver-service"
            }.All(cluster.Contains) && topology.Cluster.Nodes.Count(node => node.Kind == "volume") == 2,
            "The cluster view shows both Deployments, both StatefulSets, every Service and both data volumes.");
        Require(topology.Cluster.Edges.Any(edge => edge is { From: "squad", To: "service:api-service" }) &&
            topology.Cluster.Edges.Count(edge => edge.From == "squad") == 1 &&
            topology.Cluster.Edges.Any(edge => edge is { From: "workload:api-deployment", To: "service:catalog-service", Label: "connection" }) &&
            topology.Cluster.Edges.Any(edge => edge is { From: "workload:catalog-deployment", To: "service:ordersserver-service", Label: "connection" }),
            "Only the gateway is reached from Squad; internal traffic goes through the referenced Services.");
        Require(!JsonSerializer.Serialize(topology).Contains("Password", StringComparison.OrdinalIgnoreCase),
            "A multi-service topology never carries secret values.");

        AppTopology planned = AppTopologyService.Planned(ns, new AppComponents(
            [new("api", "Api"), new("catalog", "Catalog")],
            [new("frontend", "frontend", "/"), new("admin", "admin", "/admin/")],
            [new("db", "appdb"), new("db", "reportsdb"), new("ordersserver", "ordersdb")]));
        Require(Ids(planned.Aspire).SequenceEqual(new[] { "frontend", "admin", "api", "catalog", "db", "ordersserver" }) &&
            planned.Aspire.Nodes.Single(node => node.Id == "db").Detail == "PostgreSQL · appdb, reportsdb" &&
            planned.Cluster.Nodes.Count(node => node.Kind == "statefulset") == 2 &&
            planned.Cluster.Nodes.Count(node => node.Kind == "deployment") == 2,
            "Before a build, the planned topology shows the declared components.");
    }

    private static void ServiceChecks(string fixtureSource, string rendered)
    {
        string root = Path.Combine(Path.GetTempPath(), "squad-topology-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, root, NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var identity = new AppIdentity(AppLibrary.FirstAppId);
            var deployments = new AppDeploymentStateStore(workspace, identity);
            var service = new AppTopologyService(workspace, identity, deployments);
            Require(service.Get().State == "planned", "Without a rendered chart the service reports the planned shape.");

            Directory.CreateDirectory(Path.GetDirectoryName(service.RenderedPath)!);
            File.WriteAllText(service.RenderedPath, "kind: [unterminated");
            Require(service.Get().State == "planned", "An unreadable chart falls back to the planned shape.");

            File.WriteAllText(service.RenderedPath, rendered);
            Require(service.Get().State == "built", "A rendered chart without a deployment is built, not running.");
            deployments.Begin("0123456789abcdef");
            deployments.RecordBuilt("0123456789abcdef", "sha256:" + new string('b', 64));
            deployments.BeginDeploy("0123456789abcdef");
            deployments.Complete("0123456789abcdef");
            AppTopology deployed = service.Get();
            Require(deployed is { State: "deployed", Revision: "0123456789abcdef" },
                "A deployed revision marks the topology as running, with its revision.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void LiveStatusChecks()
    {
        static V1Deployment Deployment(string name, string component, int desired, int ready) => new()
        {
            Metadata = new() { Name = name, Labels = new Dictionary<string, string> { ["app.kubernetes.io/component"] = component } },
            Spec = new() { Replicas = desired, Selector = new() { MatchLabels = new Dictionary<string, string> { ["app.kubernetes.io/component"] = component } } },
            Status = new() { ReadyReplicas = ready }
        };
        static V1Pod Pod(string component, string phase, string? waiting = null, int restarts = 0) => new()
        {
            Metadata = new() { Labels = new Dictionary<string, string> { ["app.kubernetes.io/component"] = component } },
            Status = new()
            {
                Phase = phase,
                ContainerStatuses =
                [
                    new() { Name = component, Image = "x", ImageID = "x", Ready = waiting is null, RestartCount = restarts,
                        State = waiting is null ? new() { Running = new() } : new() { Waiting = new() { Reason = waiting, Message = "private log text" } } }
                ]
            }
        };
        var database = new V1StatefulSet
        {
            Metadata = new() { Name = "db-statefulset", Labels = new Dictionary<string, string> { ["app.kubernetes.io/component"] = "db" } },
            Spec = new() { Replicas = 1, Selector = new() { MatchLabels = new Dictionary<string, string> { ["app.kubernetes.io/component"] = "db" } } },
            Status = new() { ReadyReplicas = 0 }
        };

        IReadOnlyDictionary<string, WorkloadStatus> status = AppWorkloadStatusCalculator.Calculate(
            [Deployment("api-deployment", "api", 1, 0), Deployment("worker-deployment", "worker", 1, 1), Deployment("idle-deployment", "idle", 0, 0)],
            [database],
            [Pod("api", "Running", "CrashLoopBackOff", 4), Pod("db", "Pending"), Pod("worker", "Running", restarts: 1)]);

        Require(status["workload:api-deployment"] == new WorkloadStatus("failing", "0/1 ready · CrashLoopBackOff · 4 restarts") &&
            status["api"] == status["workload:api-deployment"],
            "A crash-looping API is failing, keyed by both its cluster workload and its Aspire resource.");
        Require(status["workload:db-statefulset"] == new WorkloadStatus("starting", "0/1 ready · Pending"),
            "A pending database is starting.");
        Require(status["workload:worker-deployment"] == new WorkloadStatus("ready", "1/1 ready · 1 restart") &&
            status["workload:idle-deployment"].State == "stopped",
            "Ready and scaled-to-zero workloads are reported as such.");
        Require(status.Values.All(item => !item.Detail.Contains("private log text", StringComparison.Ordinal)),
            "Live status carries reason codes and counts, never container messages.");

        string root = Path.Combine(Path.GetTempPath(), "squad-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            string starter = Path.Combine(Path.GetDirectoryName(FixtureRoot)!, DisposableFixtureWorkspace.StarterTemplateName);
            var workspace = new DisposableFixtureWorkspace(starter, root, NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var identity = new AppIdentity(AppLibrary.FirstAppId);
            var deployments = new AppDeploymentStateStore(workspace, identity);
            var reader = new FakeStatusReader(new AppLiveStatus(DateTimeOffset.UtcNow, status));
            var service = new AppTopologyService(workspace, identity, deployments, reader);
            Require(!service.GetLiveAsync(default).GetAwaiter().GetResult().Available && reader.Calls == 0,
                "Live status is not read before the app is deployed.");
            deployments.Begin("fedcba9876543210");
            deployments.RecordBuilt("fedcba9876543210", "sha256:" + new string('c', 64));
            deployments.BeginDeploy("fedcba9876543210");
            deployments.Complete("fedcba9876543210");
            AppTopologyLive live = service.GetLiveAsync(default).GetAwaiter().GetResult();
            Require(live.Available && live.Nodes["api"].State == "failing" &&
                reader.Namespaces.Single() == "soaks-app-first",
                "A deployed app reads live status from its own namespace only.");
            Require(!new AppTopologyService(workspace, identity, deployments).GetLiveAsync(default).GetAwaiter().GetResult().Available,
                "Without a status reader (local mode), live status is reported as unavailable.");
            reader.Result = null;
            Require(!service.GetLiveAsync(default).GetAwaiter().GetResult().Available,
                "A cluster that cannot be read reports live status as unavailable, not as healthy.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string FixtureRoot = "";

    private sealed class FakeStatusReader(AppLiveStatus? result) : IAppWorkloadStatusReader
    {
        public AppLiveStatus? Result { get; set; } = result;
        public int Calls { get; private set; }
        public List<string> Namespaces { get; } = [];

        public Task<AppLiveStatus?> ReadAsync(string appNamespace, CancellationToken cancellationToken)
        {
            Calls++;
            Namespaces.Add(appNamespace);
            return Task.FromResult(Result);
        }
    }

    private static string[] Ids(TopologyView view) => view.Nodes.Select(node => node.Id).ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
