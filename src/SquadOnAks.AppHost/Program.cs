using System.Runtime.InteropServices;
using Aspire.Hosting.Azure.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Aspire.Hosting.Publishing;
using Azure.Provisioning.ContainerService;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> copilotToken = builder.AddParameter("copilot-token", secret: true);
string deploymentTarget = Environment.GetEnvironmentVariable("Parameters__deployment_target") ?? "aks";
if (deploymentTarget is not "aks" and not "docker-desktop")
{
    throw new InvalidOperationException(
        $"Unsupported deployment target '{deploymentTarget}'. Use 'aks' or 'docker-desktop'.");
}

bool isDockerDesktopTarget = deploymentTarget == "docker-desktop";
IResourceBuilder<AksNodePoolResource>? systemPool = null;
if (isDockerDesktopTarget)
{
    IResourceBuilder<ParameterResource> registryEndpoint = builder.AddParameter("registry-endpoint");
#pragma warning disable ASPIRECOMPUTE003
    var registry = builder.AddContainerRegistry("registry", registryEndpoint);
    builder.AddKubernetesEnvironment("docker-desktop").WithContainerRegistry(registry);
#pragma warning restore ASPIRECOMPUTE003
}
else
{
    // Arm64 nodes: the host, worker, Playwright and Copilot CLI images are the same arm64 builds proven locally.
    const string nodeSize = "Standard_D4ps_v5";
    const int minNodes = 2, maxNodes = 3;
    IResourceBuilder<AzureKubernetesEnvironmentResource> aks = builder.AddAzureKubernetesEnvironment("aks")
        .WithSystemNodePool(nodeSize, minCount: minNodes, maxCount: maxNodes);
    // The cluster API is reachable only from the addresses given at deploy time (comma separated CIDRs).
    string[] authorizedRanges = (Environment.GetEnvironmentVariable("SOAKS_AKS_AUTHORIZED_IPS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    aks.ConfigureInfrastructure(infrastructure =>
    {
        ContainerServiceManagedCluster cluster = infrastructure.GetProvisionableResources()
            .OfType<ContainerServiceManagedCluster>()
            .Single();
        cluster.AgentPoolProfiles.Clear();
        cluster.AgentPoolProfiles.Add(new ManagedClusterAgentPoolProfile
        {
            Name = "system",
            VmSize = nodeSize,
            Count = minNodes,
            MinCount = minNodes,
            MaxCount = maxNodes,
            IsAutoScalingEnabled = true,
            Mode = AgentPoolMode.System,
            OSType = ContainerServiceOSType.Linux
        });
        // Cilium enforces the NetworkPolicies Squad relies on (no network for builds, per-app boundaries).
        cluster.NetworkProfile = new ContainerServiceNetworkProfile
        {
            NetworkPlugin = ContainerServiceNetworkPlugin.Azure,
            NetworkPluginMode = ContainerServiceNetworkPluginMode.Overlay,
            NetworkDataplane = NetworkDataplane.Cilium,
            NetworkPolicy = ContainerServiceNetworkPolicy.Cilium
        };
        if (authorizedRanges.Length > 0)
        {
            var access = new ManagedClusterApiServerAccessProfile();
            foreach (string range in authorizedRanges) access.AuthorizedIPRanges.Add(range);
            cluster.ApiServerAccessProfile = access;
        }
    });

    systemPool = builder.CreateResourceBuilder(
        new AksNodePoolResource(
            "system",
            new AksNodePoolConfig(
                "system",
                nodeSize,
                minNodes,
                maxNodes,
                AksNodePoolMode.System),
            aks.Resource));
}

if (!builder.ExecutionContext.IsPublishMode)
{
    IResourceBuilder<ParameterResource> workspacePath = builder.AddParameter("workspace-path");
    string workspaceTemplate = builder.Configuration["Parameters:workspace-template"]
        ?? builder.Configuration["Parameters:workspace_template"]
        ?? "starter";
    IResourceBuilder<ProjectResource> app = builder.AddProject<Projects.SquadOnAks_Web>("app")
        .WithEnvironment("COPILOT_TOKEN", copilotToken)
        .WithEnvironment("GITHUB_TOKEN", copilotToken)
        .WithEnvironment("SQUAD_WORKSPACE_PATH", workspacePath)
        .WithEnvironment("SQUAD_WORKSPACE_TEMPLATE", workspaceTemplate)
        .WithEnvironment("SQUAD_INITIALIZE_WORKSPACE_ON_START", "true")
        .WithEnvironment("SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY", "false")
        .WithEnvironment("SQUAD_LOCAL_VALIDATION_IMAGE", builder.Configuration["SQUAD_LOCAL_VALIDATION_IMAGE"] ?? "")
        .WithEnvironment("SQUAD_LOCAL_VALIDATION_IMAGES", builder.Configuration["SQUAD_LOCAL_VALIDATION_IMAGES"] ?? "")
        .WithEnvironment("SQUAD_LOCAL_RUNTIME_IMAGE", builder.Configuration["SQUAD_LOCAL_RUNTIME_IMAGE"] ?? "")
        .WithEnvironment("SQUAD_LOCAL_POSTGRES_IMAGE", builder.Configuration["SQUAD_LOCAL_POSTGRES_IMAGE"] ?? "")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithHttpEndpoint(targetPort: 8081, name: "preview")
        .WithEnvironment("SQUAD_PREVIEW_PORT", "8081")
        .WithHttpHealthCheck("/health/ready")
        .WithExternalHttpEndpoints();
    app.WithEnvironment("SQUAD_PREVIEW_URL", app.GetEndpoint("preview"));
}
else
{
    IResourceBuilder<ParameterResource> workerImage = builder.AddParameter("worker-image");
    string deployedTemplate = Environment.GetEnvironmentVariable("Parameters__workspace_template") ?? "starter";
    const string deployedWorkspace = "/workspace/fixture";
    string? workerPullSecret = Environment.GetEnvironmentVariable("SOAKS_WORKER_IMAGE_PULL_SECRET");
    var namespaceProvisioner = builder.AddProject<Projects.SquadOnAks_NamespaceProvisioner>("namespace-provisioner")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithHttpHealthCheck("/health/ready");
    namespaceProvisioner.PublishAsKubernetesService(resource =>
    {
        if (resource.Service is null || resource.Workload is not Deployment deployment)
        {
            throw new InvalidOperationException("The namespace provisioner must publish as a Kubernetes service.");
        }

        resource.Service.Spec.Type = "ClusterIP";
        const string accountName = "soaks-namespace-provisioner-account";
        const string roleName = "soaks-namespace-provisioner";
        deployment.Spec.Template.Spec.ServiceAccountName = accountName;
        var account = new ServiceAccountV1 { Metadata = new() { Name = accountName } };
        var role = new ClusterRole { Metadata = new() { Name = roleName } };
        var namespaceRule = new PolicyRuleV1();
        namespaceRule.ApiGroups.Add("");
        namespaceRule.Resources.Add("namespaces");
        namespaceRule.Verbs.Add("get");
        // list: find an app's leftover check namespaces (by ownership labels) when the app is deleted.
        namespaceRule.Verbs.Add("list");
        namespaceRule.Verbs.Add("create");
        namespaceRule.Verbs.Add("update");
        namespaceRule.Verbs.Add("delete");
        role.Rules.Add(namespaceRule);

        var serviceAccountRule = new PolicyRuleV1();
        serviceAccountRule.ApiGroups.Add("");
        serviceAccountRule.Resources.Add("serviceaccounts");
        serviceAccountRule.Verbs.Add("get");
        serviceAccountRule.Verbs.Add("update");
        serviceAccountRule.ResourceNames.Add("default");
        role.Rules.Add(serviceAccountRule);

        var deployRoleRule = new PolicyRuleV1();
        deployRoleRule.ApiGroups.Add("rbac.authorization.k8s.io");
        deployRoleRule.Resources.Add("roles");
        deployRoleRule.Verbs.Add("get");
        deployRoleRule.Verbs.Add("update");
        deployRoleRule.ResourceNames.Add("soaks-app-deployer");
        deployRoleRule.ResourceNames.Add("soaks-app-observer");
        role.Rules.Add(deployRoleRule);

        var deployRoleBindingRule = new PolicyRuleV1();
        deployRoleBindingRule.ApiGroups.Add("rbac.authorization.k8s.io");
        deployRoleBindingRule.Resources.Add("roles");
        deployRoleBindingRule.Verbs.Add("bind");
        deployRoleBindingRule.ResourceNames.Add("soaks-app-deployer");
        deployRoleBindingRule.ResourceNames.Add("soaks-app-observer");
        role.Rules.Add(deployRoleBindingRule);

        var roleBindingRule = new PolicyRuleV1();
        roleBindingRule.ApiGroups.Add("rbac.authorization.k8s.io");
        roleBindingRule.Resources.Add("rolebindings");
        roleBindingRule.Verbs.Add("get");
        roleBindingRule.Verbs.Add("update");
        roleBindingRule.ResourceNames.Add("soaks-app-deployer");
        roleBindingRule.ResourceNames.Add("soaks-app-observer");
        role.Rules.Add(roleBindingRule);

        var networkPolicyRule = new PolicyRuleV1();
        networkPolicyRule.ApiGroups.Add("networking.k8s.io");
        networkPolicyRule.Resources.Add("networkpolicies");
        networkPolicyRule.Verbs.Add("get");
        networkPolicyRule.Verbs.Add("update");
        networkPolicyRule.ResourceNames.Add("soaks-app-boundary");
        role.Rules.Add(networkPolicyRule);
        foreach ((string group, string resourceName) in new[]
        {
            ("rbac.authorization.k8s.io", "roles"),
            ("rbac.authorization.k8s.io", "rolebindings"),
            ("networking.k8s.io", "networkpolicies")
        })
        {
            // Kubernetes cannot authorize create using resourceNames.
            var createRule = new PolicyRuleV1();
            createRule.ApiGroups.Add(group);
            createRule.Resources.Add(resourceName);
            createRule.Verbs.Add("create");
            if (resourceName == "roles")
            {
                createRule.Verbs.Add("escalate");
            }
            role.Rules.Add(createRule);
        }

        var tokenReviewRule = new PolicyRuleV1();
        tokenReviewRule.ApiGroups.Add("authentication.k8s.io");
        tokenReviewRule.Resources.Add("tokenreviews");
        tokenReviewRule.Verbs.Add("create");
        role.Rules.Add(tokenReviewRule);
        var binding = new ClusterRoleBinding
        {
            Metadata = new() { Name = $"{roleName}-binding" },
            RoleRef = new()
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = "ClusterRole",
                Name = roleName
            }
        };
        binding.Subjects.Add(new SubjectV1
        {
            Kind = "ServiceAccount",
            Name = accountName,
            Namespace = "{{ .Release.Namespace }}"
        });
        resource.AdditionalResources.Add(account);
        resource.AdditionalResources.Add(role);
        resource.AdditionalResources.Add(binding);
    });
    var app = builder.AddDockerfile(
        "app",
        "../..",
        "src/SquadOnAks.Web/Dockerfile",
        stage: "production")
        .WithEnvironment("COPILOT_TOKEN", copilotToken)
        .WithEnvironment("SQUAD_WORKSPACE_PATH", deployedWorkspace)
        .WithEnvironment("SQUAD_WORKSPACE_CLAIM", "soaks-workspace")
        .WithEnvironment("SQUAD_WORKER_IMAGE", workerImage)
        .WithEnvironment("SQUAD_WORKSPACE_TEMPLATE", deployedTemplate)
        .WithEnvironment("SQUAD_NAMESPACE_PROVISIONER_ENDPOINT", namespaceProvisioner.GetEndpoint("http"))
        .WithEnvironment("SOAKS_HOSTING", "cluster")
        .WithEnvironment("SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY", "true")
        .WithEnvironment("HOME", "/workspace/runtime/home")
        .WithEnvironment("TMPDIR", "/workspace/runtime/tmp")
        .WithEnvironment("XDG_CACHE_HOME", "/workspace/runtime/cache")
        .WithEnvironment("XDG_CONFIG_HOME", "/workspace/runtime/config")
        .WithEnvironment("SQUAD_WORKER_IMAGE_PULL_SECRET", workerPullSecret ?? "")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithHttpHealthCheck("/health/ready")
        .WithExternalHttpEndpoints();

    if (systemPool is not null)
    {
        // Everything runs on the one (autoscaling) system pool; there is no separate workload pool.
        app.WithNodePool(systemPool);
        namespaceProvisioner.WithNodePool(systemPool);
    }

    if (isDockerDesktopTarget)
    {
        app.WithEnvironment("SOAKS_REGISTRY_PUSH", Environment.GetEnvironmentVariable("SOAKS_REGISTRY_PUSH") ?? "")
            .WithEnvironment("SOAKS_REGISTRY_PULL", Environment.GetEnvironmentVariable("Parameters__registry_endpoint") ?? "");
    }
    else
    {
        // Generated apps' images go to the cluster's own registry (aks-acr): the deploy worker pushes with a scoped token
        // (Secret soaks-registry-push), and nodes pull with the kubelet identity's AcrPull role. The address is a plain
        // deploy-time value, not a reference to the Azure resource: a reference would give the host an Azure workload
        // identity (and a different service account) that it doesn't need.
        string registry = Environment.GetEnvironmentVariable("SOAKS_REGISTRY_ENDPOINT") ?? "";
        app.WithEnvironment("SOAKS_REGISTRY_PUSH", registry)
            .WithEnvironment("SOAKS_REGISTRY_PULL", registry)
            .WithEnvironment("SOAKS_REGISTRY_SECRET", "soaks-registry-push");
    }

    string? nugetSource = Environment.GetEnvironmentVariable("SOAKS_NUGET_SOURCE");
    if (!string.IsNullOrWhiteSpace(nugetSource))
    {
        app.WithBuildArg("NUGET_SOURCE", nugetSource);
    }

#pragma warning disable ASPIREPIPELINES003
    // Docker Desktop matches this machine; AKS uses arm64 nodes.
    ContainerTargetPlatform platform = !isDockerDesktopTarget ? ContainerTargetPlatform.LinuxArm64
        : RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => ContainerTargetPlatform.LinuxArm64,
            Architecture.X64 => ContainerTargetPlatform.LinuxAmd64,
            _ => throw new PlatformNotSupportedException("Docker Desktop deployment requires an ARM64 or x64 host.")
        };
    app.WithContainerBuildOptions(context => context.TargetPlatform = platform);
    namespaceProvisioner.WithContainerBuildOptions(context => context.TargetPlatform = platform);
#pragma warning restore ASPIREPIPELINES003

    app.PublishAsKubernetesService(resource =>
    {
        if (resource.Service is null)
        {
            throw new InvalidOperationException("The application project did not generate a Kubernetes Service.");
        }

        // Squad has no sign-in and holds the person's GitHub token: it is reached only through a port-forward.
        resource.Service.Spec.Type = "ClusterIP";
        ServicePortV1 servicePort = resource.Service.Spec.Ports.Single();
        servicePort.Port = 80;
        servicePort.TargetPort = 8080;

        if (resource.Workload is Deployment deployment)
        {
            const string accountName = "soaks-app-manager";
            const string roleName = "soaks-app-writer";
            var workspaceClaim = new PersistentVolumeClaim
            {
                ApiVersion = "v1",
                Kind = "PersistentVolumeClaim",
                Metadata = new ObjectMetaV1
                {
                    Name = "soaks-workspace",
                    Annotations =
                    {
                        ["helm.sh/resource-policy"] = "keep"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        ["soaks.dev/owner"] = "soaks",
                        ["soaks.dev/resource"] = "workspace"
                    }
                },
                Spec = new PersistentVolumeClaimSpecV1
                {
                    DataSource = null!,
                    DataSourceRef = null!,
                    Selector = null!,
                    AccessModes = { "ReadWriteOnce" },
                    Resources = new VolumeResourceRequirementsV1
                    {
                        // Imported repositories and their packages live here too.
                        Requests = { ["storage"] = isDockerDesktopTarget ? "2Gi" : "32Gi" }
                    }
                }
            };
            resource.AdditionalResources.Add(workspaceClaim);

            var account = new ServiceAccountV1 { Metadata = new() { Name = accountName } };
            var role = new Role { Metadata = new() { Name = roleName } };
            foreach ((string group, string[] resources) in new[]
            {
                ("", new[] { "configmaps", "services" }),
                ("apps", new[] { "deployments" }),
                ("networking.k8s.io", new[] { "networkpolicies" }),
                ("batch", new[] { "jobs" })
            })
            {
                var rule = new PolicyRuleV1();
                rule.ApiGroups.Add(group);
                foreach (string name in resources) rule.Resources.Add(name);
                foreach (string verb in group == "batch"
                    ? new[] { "get", "create", "delete" }
                    : new[] { "get", "create", "update", "delete" })
                    rule.Verbs.Add(verb);
                role.Rules.Add(rule);
            }
            var serviceAccountRule = new PolicyRuleV1();
            serviceAccountRule.ApiGroups.Add("");
            serviceAccountRule.Resources.Add("serviceaccounts");
            serviceAccountRule.Verbs.Add("get");
            serviceAccountRule.Verbs.Add("create");
            serviceAccountRule.Verbs.Add("delete");
            role.Rules.Add(serviceAccountRule);
            var podsReadRule = new PolicyRuleV1();
            podsReadRule.ApiGroups.Add("");
            podsReadRule.Resources.Add("pods");
            podsReadRule.Verbs.Add("get");
            podsReadRule.Verbs.Add("list");
            role.Rules.Add(podsReadRule);
            var podLogsRule = new PolicyRuleV1();
            podLogsRule.ApiGroups.Add("");
            podLogsRule.Resources.Add("pods/log");
            podLogsRule.Verbs.Add("get");
            role.Rules.Add(podLogsRule);
            var binding = new RoleBinding
            {
                Metadata = new() { Name = "soaks-app-access" },
                RoleRef = new() { ApiGroup = "rbac.authorization.k8s.io", Kind = "Role", Name = roleName }
            };
            binding.Subjects.Add(new SubjectV1
            {
                Kind = "ServiceAccount",
                Name = accountName,
                Namespace = "{{ .Release.Namespace }}"
            });
            resource.AdditionalResources.Add(account);
            resource.AdditionalResources.Add(role);
            resource.AdditionalResources.Add(binding);
            deployment.Spec.Replicas = 1;
            // The workspace disk attaches to one node at a time: stop the old host before starting the new one.
            deployment.Spec.Strategy = new DeploymentStrategyV1 { Type = "Recreate", RollingUpdate = null! };
            PodSpecV1 pod = deployment.Spec.Template.Spec;
            pod.ServiceAccountName = accountName;
            deployment.Spec.Template.Metadata.Labels["soaks.dev/host"] = "true";
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
                PersistentVolumeClaim = new PersistentVolumeClaimVolumeSourceV1
                {
                    ClaimName = workspaceClaim.Metadata.Name,
                    ReadOnly = false
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
                // Teams compile and install packages inside this pod too; half a core stalled three concurrent teams.
                limitCpu: "2",
                // ~270 MiB for the host plus ~300-335 MiB per Copilot session with an active turn (measured), plus the
                // team's own builds: three concurrent teams were OOMKilled at 2Gi.
                limitMemory: "4Gi",
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
                Value = deployedWorkspace
            });
            fixtureInitializer.Env.Add(new EnvVarV1
            {
                Name = "SQUAD_WORKSPACE_TEMPLATE",
                Value = deployedTemplate
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
