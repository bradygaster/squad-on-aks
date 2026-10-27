using Aspire.Hosting.Azure.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Azure.Provisioning.ContainerService;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> copilotToken = builder.AddParameter("copilot-token", secret: true);

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

if (!builder.ExecutionContext.IsPublishMode)
{
    IResourceBuilder<ParameterResource> workspacePath = builder.AddParameter("workspace-path");
    string workspaceTemplate = builder.Configuration["Parameters:workspace-template"]
        ?? builder.Configuration["Parameters:workspace_template"]
        ?? "route-planner";
    IResourceBuilder<ProjectResource> app = builder.AddProject<Projects.SquadOnAks_Web>("app")
        .WithEnvironment("COPILOT_TOKEN", copilotToken)
        .WithEnvironment("GITHUB_TOKEN", copilotToken)
        .WithEnvironment("SQUAD_WORKSPACE_PATH", workspacePath)
        .WithEnvironment("SQUAD_WORKSPACE_TEMPLATE", workspaceTemplate)
        .WithEnvironment("SQUAD_INITIALIZE_WORKSPACE_ON_START", "true")
        .WithEnvironment("SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY", "false")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithHttpEndpoint(targetPort: 8081, name: "preview")
        .WithEnvironment("SQUAD_PREVIEW_PORT", "8081")
        .WithHttpHealthCheck("/health/ready")
        .WithExternalHttpEndpoints();
    app.WithEnvironment("SQUAD_PREVIEW_URL", app.GetEndpoint("preview"));
}
else
{
    builder.AddDockerfile(
            "app",
            "../..",
            "src/SquadOnAks.Web/Dockerfile",
            stage: "production")
        .WithEnvironment("COPILOT_TOKEN", copilotToken)
        .WithEnvironment("SQUAD_WORKSPACE_PATH", "/workspace/fixture")
        .WithEnvironment("SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY", "true")
        .WithEnvironment("HOME", "/workspace/runtime/home")
        .WithEnvironment("TMPDIR", "/workspace/runtime/tmp")
        .WithEnvironment("XDG_CACHE_HOME", "/workspace/runtime/cache")
        .WithEnvironment("XDG_CONFIG_HOME", "/workspace/runtime/config")
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
            pod.TerminationGracePeriodSeconds = 30;
            pod.SecurityContext = new PodSecurityContextV1
            {
                RunAsNonRoot = true,
                RunAsUser = 1654,
                RunAsGroup = 1654,
                FsGroup = 1654,
                FsGroupChangePolicy = "OnRootMismatch",
                SeccompProfile = new SeccompProfileV1
                {
                    Type = "RuntimeDefault"
                }
            };

            pod.Volumes.Add(new VolumeV1
            {
                Name = "workspace",
                EmptyDir = new EmptyDirVolumeSourceV1
                {
                    SizeLimit = "2Gi"
                }
            });

            application.VolumeMounts.Add(new VolumeMountV1
            {
                Name = "workspace",
                MountPath = "/workspace"
            });
            application.SecurityContext = CreateSecurityContext();
            application.Resources = CreateResourceRequirements(
                requestCpu: "50m",
                requestMemory: "512Mi",
                requestEphemeralStorage: "1Gi",
                limitCpu: "500m",
                limitMemory: "1Gi",
                limitEphemeralStorage: "2Gi");
            application.LivenessProbe = CreateHttpProbe("/health/live");
            application.ReadinessProbe = CreateHttpProbe("/health/ready");

            var fixtureInitializer = new ContainerV1
            {
                Name = "fixture-init",
                Image = application.Image,
                ImagePullPolicy = application.ImagePullPolicy,
                SecurityContext = CreateSecurityContext(),
                Resources = CreateResourceRequirements(
                    requestCpu: "25m",
                    requestMemory: "128Mi",
                    requestEphemeralStorage: "256Mi",
                    limitCpu: "250m",
                    limitMemory: "256Mi",
                    limitEphemeralStorage: "512Mi")
            };
            fixtureInitializer.Command.Add("dotnet");
            fixtureInitializer.Args.Add("SquadOnAks.Web.dll");
            fixtureInitializer.Args.Add("--initialize-workspace");
            fixtureInitializer.Env.Add(new EnvVarV1
            {
                Name = "SQUAD_WORKSPACE_PATH",
                Value = "/workspace/fixture"
            });
            AddRuntimeEnvironment(fixtureInitializer);
            fixtureInitializer.VolumeMounts.Add(new VolumeMountV1
            {
                Name = "workspace",
                MountPath = "/workspace"
            });
            pod.InitContainers.Add(fixtureInitializer);
        }
    });
}

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

static SecurityContextV1 CreateSecurityContext()
{
    var capabilities = new CapabilitiesV1();
    capabilities.Drop.Add("ALL");
    return new SecurityContextV1
    {
        RunAsNonRoot = true,
        RunAsUser = 1654,
        RunAsGroup = 1654,
        AllowPrivilegeEscalation = false,
        Privileged = false,
        ReadOnlyRootFilesystem = true,
        Capabilities = capabilities,
        SeccompProfile = new SeccompProfileV1
        {
            Type = "RuntimeDefault"
        }
    };
}

static void AddRuntimeEnvironment(ContainerV1 container)
{
    container.Env.Add(new EnvVarV1 { Name = "HOME", Value = "/workspace/runtime/home" });
    container.Env.Add(new EnvVarV1 { Name = "TMPDIR", Value = "/workspace/runtime/tmp" });
    container.Env.Add(new EnvVarV1 { Name = "XDG_CACHE_HOME", Value = "/workspace/runtime/cache" });
    container.Env.Add(new EnvVarV1 { Name = "XDG_CONFIG_HOME", Value = "/workspace/runtime/config" });
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
