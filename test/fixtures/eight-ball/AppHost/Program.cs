using Aspire.Hosting.Kubernetes.Resources;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
#pragma warning disable ASPIRECOMPUTE003
    var registryEndpoint = builder.AddParameter("registry-endpoint");
    var registry = builder.AddContainerRegistry("registry", registryEndpoint);
    builder.AddKubernetesEnvironment("k8s").WithContainerRegistry(registry).WithDashboard(false);
#pragma warning restore ASPIRECOMPUTE003
}

var api = builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();

var frontend = builder.AddViteApp("frontend", "../frontend")
    .WithNpm(installCommand: "ci")
    .WithReference(api)
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"));

api.PublishWithContainerFiles(frontend, "./wwwroot");
api.PublishAsKubernetesService(resource =>
{
    if (resource.Workload is not Deployment deployment)
        throw new InvalidOperationException("The API must publish as a Deployment.");
    var pod = deployment.Spec.Template.Spec;
    pod.AutomountServiceAccountToken = false;
    pod.SecurityContext = new PodSecurityContextV1
    {
        RunAsNonRoot = true,
        RunAsUser = 1654,
        SeccompProfile = new SeccompProfileV1 { Type = "RuntimeDefault" }
    };
    var container = pod.Containers.Single();
    var capabilities = new CapabilitiesV1();
    capabilities.Drop.Add("ALL");
    container.SecurityContext = new SecurityContextV1
    {
        AllowPrivilegeEscalation = false,
        ReadOnlyRootFilesystem = true,
        Capabilities = capabilities
    };
    container.Resources = new ResourceRequirementsV1
    {
        Requests = { ["cpu"] = "50m", ["memory"] = "64Mi" },
        Limits = { ["cpu"] = "500m", ["memory"] = "256Mi" }
    };
    container.ReadinessProbe = new ProbeV1
    {
        HttpGet = new HttpGetActionV1 { Path = "/health/ready", Port = 8080 },
        PeriodSeconds = 5
    };
});

builder.Build().Run();
