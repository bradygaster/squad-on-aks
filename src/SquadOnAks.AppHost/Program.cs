#pragma warning disable ASPIREAZURE003
#pragma warning disable AZPROVISION001

using Aspire.Hosting.Azure;
using Aspire.Hosting.Azure.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Azure.Core;
using Azure.Provisioning;
using Azure.Provisioning.Cdn;
using Azure.Provisioning.ContainerService;
using Azure.Provisioning.Expressions;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

const string originDnsLabel = "squad-on-aks-live";
const string originHostName = $"{originDnsLabel}.westus2.cloudapp.azure.com";

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

IResourceBuilder<ProjectResource> app = builder.AddProject<Projects.SquadOnAks_Web>("app")
    .WithEnvironment("COPILOT_TOKEN", copilotToken)
    .WithEnvironment("SQUAD_WORKSPACE_PATH", workspacePath)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpProbe(ProbeType.Liveness, "/health/ready", endpointName: "http")
    .WithExternalHttpEndpoints()
    .WithNodePool(systemPool)
    .PublishAsKubernetesService(resource =>
    {
        if (resource.Service is null)
        {
            throw new InvalidOperationException("The application project did not generate a Kubernetes Service.");
        }

        resource.Service.Spec.Type = "LoadBalancer";
        resource.Service.Metadata.Annotations["service.beta.kubernetes.io/azure-dns-label-name"] =
            originDnsLabel;
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

AddAksFrontDoor(builder, app, originHostName);

builder.Build().Run();

static IResourceBuilder<AzureFrontDoorResource> AddAksFrontDoor(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> origin,
    string originHostName)
{
    builder.AddAzureProvisioning();
    const string frontDoorApiVersion = "2024-09-01";

    var resource = new AzureFrontDoorResource("frontdoor", infrastructure =>
    {
        var profile = new CdnProfile("frontdoor", frontDoorApiVersion)
        {
            Location = new AzureLocation("Global"),
            SkuName = CdnSkuName.StandardAzureFrontDoor,
            Tags = { { "aspire-resource-name", "frontdoor" } }
        };
        profile.Name = BicepFunction.Take(
            BicepFunction.Interpolate(
                $"frontdoor-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"),
            260);
        infrastructure.Add(profile);

        var endpoint = new FrontDoorEndpoint("appEndpoint", frontDoorApiVersion)
        {
            EnabledState = EnabledState.Enabled,
            Location = new AzureLocation("Global"),
            Parent = profile
        };
        endpoint.Name = BicepFunction.Take(
            BicepFunction.Interpolate(
                $"app-endpoint-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"),
            46);
        infrastructure.Add(endpoint);

        var originGroup = new FrontDoorOriginGroup("appOriginGroup", frontDoorApiVersion)
        {
            Parent = profile,
            HealthProbeSettings = new HealthProbeSettings
            {
                ProbeIntervalInSeconds = 30,
                ProbePath = "/health/ready",
                ProbeProtocol = HealthProbeProtocol.Http
            },
            LoadBalancingSettings = new LoadBalancingSettings
            {
                AdditionalLatencyInMilliseconds = 50,
                SampleSize = 4,
                SuccessfulSamplesRequired = 3
            }
        };
        originGroup.Name = BicepFunction.Take(
            BicepFunction.Interpolate(
                $"app-origin-group-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"),
            90);
        infrastructure.Add(originGroup);

        var frontDoorOrigin = new FrontDoorOrigin("appOrigin", frontDoorApiVersion)
        {
            EnabledState = EnabledState.Enabled,
            EnforceCertificateNameCheck = false,
            Parent = originGroup,
            HostName = originHostName,
            HttpPort = 80,
            OriginHostHeader = originHostName
        };
        frontDoorOrigin.Name = BicepFunction.Take(
            BicepFunction.Interpolate(
                $"app-origin-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id, originHostName)}"),
            90);
        infrastructure.Add(frontDoorOrigin);

        var route = new FrontDoorRoute("appRoute", frontDoorApiVersion)
        {
            EnabledState = EnabledState.Enabled,
            Parent = endpoint,
            ForwardingProtocol = ForwardingProtocol.HttpOnly,
            HttpsRedirect = HttpsRedirect.Enabled,
            LinkToDefaultDomain = LinkToDefaultDomain.Enabled,
            OriginGroupId = originGroup.Id,
            PatternsToMatch = ["/*"]
        };
        route.Name = BicepFunction.Take(
            BicepFunction.Interpolate(
                $"app-route-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"),
            90);
        route.DependsOn.Add(frontDoorOrigin);
        infrastructure.Add(route);

        infrastructure.Add(new ProvisioningOutput("app_endpointUrl", typeof(string))
        {
            Value = BicepFunction.Interpolate($"https://{endpoint.HostName}")
        });
    });

    IResourceBuilder<AzureFrontDoorResource> frontDoor = builder.ExecutionContext.IsPublishMode
        ? builder.AddResource(resource).WithIconName("GlobeArrowForward")
        : builder.CreateResourceBuilder(resource);

    return frontDoor.WithOrigin(origin);
}

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
