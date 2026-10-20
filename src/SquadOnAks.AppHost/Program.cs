using Aspire.Hosting.Azure.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Azure.Provisioning.ContainerService;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> copilotToken = builder.AddParameter("copilot-token", secret: true);
IResourceBuilder<ParameterResource> workspacePath = builder.AddParameter("workspace-path");

IResourceBuilder<AzureKubernetesEnvironmentResource> aks = builder.AddAzureKubernetesEnvironment("aks")
    .WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 1);

aks.ConfigureInfrastructure(infrastructure =>
{
    ContainerServiceManagedCluster cluster = infrastructure.GetProvisionableResources()
        .OfType<ContainerServiceManagedCluster>()
        .Single();
    cluster.AgentPoolProfiles.Clear();
    cluster.AgentPoolProfiles.Add(new ManagedClusterAgentPoolProfile
    {
        Name = "system",
        VmSize = "Standard_D2s_v5",
        Count = 1,
        IsAutoScalingEnabled = false,
        Mode = AgentPoolMode.System,
        OSType = ContainerServiceOSType.Linux
    });
});

IResourceBuilder<AksNodePoolResource> systemPool = builder.CreateResourceBuilder(
    new AksNodePoolResource(
        "system",
        new AksNodePoolConfig(
            "system",
            "Standard_D2s_v5",
            1,
            1,
            AksNodePoolMode.System),
        aks.Resource));

builder.AddProject<Projects.SquadOnAks_Web>("app")
    .WithEnvironment("COPILOT_TOKEN", copilotToken)
    .WithEnvironment("SQUAD_WORKSPACE_PATH", workspacePath)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints()
    .WithNodePool(systemPool)
    .PublishAsKubernetesService(resource =>
    {
        if (resource.Service is null)
        {
            throw new InvalidOperationException("The application project did not generate a Kubernetes Service.");
        }

        resource.Service.Spec.Type = "LoadBalancer";
        ServicePortV1 servicePort = resource.Service.Spec.Ports.Single();
        servicePort.Port = 80;
        servicePort.TargetPort = 8080;

        if (resource.Workload is Deployment deployment)
        {
            deployment.Spec.Replicas = 1;
            PodSpecV1 pod = deployment.Spec.Template.Spec;
            ContainerV1 application = pod.Containers.Single();

            pod.Volumes.Add(new VolumeV1
            {
                Name = "workspace",
                EmptyDir = new EmptyDirVolumeSourceV1
                {
                    SizeLimit = "2Gi"
                }
            });

            var fixtureInitializer = new ContainerV1
            {
                Name = "fixture-init",
                Image = "mcr.microsoft.com/azurelinux/base/core:3.0",
                Resources = CreateResourceRequirements(
                    requestCpu: "50m",
                    requestMemory: "64Mi",
                    requestEphemeralStorage: "64Mi",
                    limitCpu: "100m",
                    limitMemory: "128Mi",
                    limitEphemeralStorage: "128Mi")
            };
            fixtureInitializer.Command.Add("sh");
            fixtureInitializer.Args.Add("-c");
            fixtureInitializer.Args.Add(
                "mkdir -p /workspace/fixture && printf '# Remediation fixture\\n' > /workspace/fixture/README.md");
            fixtureInitializer.VolumeMounts.Add(new VolumeMountV1
            {
                Name = "workspace",
                MountPath = "/workspace"
            });
            pod.InitContainers.Add(fixtureInitializer);

            application.VolumeMounts.Add(new VolumeMountV1
            {
                Name = "workspace",
                MountPath = "/workspace"
            });
            application.Resources = CreateResourceRequirements(
                requestCpu: "50m",
                requestMemory: "512Mi",
                requestEphemeralStorage: "1Gi",
                limitCpu: "500m",
                limitMemory: "1Gi",
                limitEphemeralStorage: "2Gi");
            application.LivenessProbe = CreateHttpProbe("/health/live");
            application.ReadinessProbe = CreateHttpProbe("/health/ready");
        }
    });

builder.Build().Run();

static ResourceRequirementsV1 CreateResourceRequirements(
    string requestCpu,
    string requestMemory,
    string requestEphemeralStorage,
    string limitCpu,
    string limitMemory,
    string limitEphemeralStorage)
{
    var resources = new ResourceRequirementsV1();
    resources.Requests["cpu"] = requestCpu;
    resources.Requests["memory"] = requestMemory;
    resources.Requests["ephemeral-storage"] = requestEphemeralStorage;
    resources.Limits["cpu"] = limitCpu;
    resources.Limits["memory"] = limitMemory;
    resources.Limits["ephemeral-storage"] = limitEphemeralStorage;
    return resources;
}

static ProbeV1 CreateHttpProbe(string path) =>
    new()
    {
        HttpGet = new HttpGetActionV1
        {
            Path = path,
            Port = 8080,
            Scheme = "HTTP"
        },
        InitialDelaySeconds = 5,
        PeriodSeconds = 10,
        TimeoutSeconds = 2,
        FailureThreshold = 3
    };
