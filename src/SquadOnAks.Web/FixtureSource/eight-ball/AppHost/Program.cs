var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
#pragma warning disable ASPIRECOMPUTE003
    var registryEndpoint = builder.AddParameter("registry-endpoint");
    var registry = builder.AddContainerRegistry("registry", registryEndpoint);
    builder.AddKubernetesEnvironment("k8s").WithContainerRegistry(registry);
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

builder.Build().Run();
