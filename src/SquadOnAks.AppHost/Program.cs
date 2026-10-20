using Aspire.Hosting.Kubernetes.Resources;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> copilotToken = builder.AddParameter("copilot-token", secret: true);

builder.AddAzureKubernetesEnvironment("aks")
    .WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 1);

builder.AddProject<Projects.SquadOnAks_DeploymentProbe>("app")
    .WithEnvironment("COPILOT_TOKEN", copilotToken)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .PublishAsKubernetesService(resource =>
    {
        if (resource.Service is null)
        {
            throw new InvalidOperationException("The application project did not generate a Kubernetes Service.");
        }

        resource.Service.Spec.Type = "LoadBalancer";

        if (resource.Workload is Deployment deployment)
        {
            deployment.Spec.Replicas = 1;
        }
    });

builder.Build().Run();
