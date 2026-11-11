// Provided by Squad. Keep this file unchanged: the platform deploys every app into its own
// Kubernetes namespace (restricted Pod Security, no service-account tokens), and these
// helpers produce workloads that namespace accepts, with PostgreSQL data on a real volume.
#pragma warning disable ASPIRECOMPUTE003
using Aspire.Hosting.Kubernetes.Resources;

namespace Aspire.Hosting;

public static class SquadHosting
{
    /// <summary>Publish mode only: target the cluster's Kubernetes environment and registry.</summary>
    public static IDistributedApplicationBuilder AddSquadKubernetes(this IDistributedApplicationBuilder builder)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            var registry = builder.AddContainerRegistry("registry", builder.AddParameter("registry-endpoint"));
            builder.AddKubernetesEnvironment("k8s").WithContainerRegistry(registry).WithDashboard(false);
        }
        return builder;
    }

    /// <summary>The API (which also serves the built front end) as a hardened Deployment on port 8080.</summary>
    public static IResourceBuilder<ProjectResource> PublishAsSquadApi(this IResourceBuilder<ProjectResource> api) =>
        api.PublishAsKubernetesService(resource =>
        {
            if (resource.Workload is not Deployment deployment)
                throw new InvalidOperationException("The API must publish as a Deployment.");
            PodSpecV1 pod = deployment.Spec.Template.Spec;
            Harden(pod, user: 1654);
            ContainerV1 container = pod.Containers.Single();
            container.SecurityContext!.ReadOnlyRootFilesystem = true; // set by Harden
            container.Resources = Limits("50m", "128Mi", "500m", "512Mi");
            container.ReadinessProbe = new ProbeV1
            {
                HttpGet = new HttpGetActionV1 { Path = "/health/ready", Port = 8080 },
                PeriodSeconds = 5
            };
        });

    /// <summary>PostgreSQL as a hardened StatefulSet whose data lives on a PersistentVolumeClaim.</summary>
    public static IResourceBuilder<PostgresServerResource> PublishAsSquadDatabase(
        this IResourceBuilder<PostgresServerResource> postgres, string storage = "1Gi") =>
        postgres.WithDataVolume().PublishAsKubernetesService(resource =>
        {
            if (resource.Workload is not StatefulSet statefulSet)
                throw new InvalidOperationException("PostgreSQL must publish as a StatefulSet.");
            PodSpecV1 pod = statefulSet.Spec.Template.Spec;
            Harden(pod, user: 999);
            pod.SecurityContext!.FsGroup = 999; // set by Harden
            pod.Containers.Single().Resources = Limits("50m", "128Mi", "500m", "512Mi");
            // Aspire publishes data volumes as emptyDir; replace it with a claim that survives restarts.
            VolumeV1 data = pod.Volumes.Single(volume => volume.EmptyDir is not null);
            pod.Volumes.Remove(data);
            var claim = new PersistentVolumeClaim();
            claim.Metadata.Name = data.Name;
            claim.Spec.AccessModes.Add("ReadWriteOnce");
            claim.Spec.Resources.Requests["storage"] = storage;
            // Empty data sources and selectors are rejected or block dynamic provisioning.
            claim.Spec.DataSource = null!;
            claim.Spec.DataSourceRef = null!;
            claim.Spec.Selector = null!;
            statefulSet.Spec.VolumeClaimTemplates.Add(claim);
        });

    private static void Harden(PodSpecV1 pod, long user)
    {
        pod.AutomountServiceAccountToken = false;
        pod.SecurityContext = new PodSecurityContextV1
        {
            RunAsNonRoot = true,
            RunAsUser = user,
            RunAsGroup = user,
            SeccompProfile = new SeccompProfileV1 { Type = "RuntimeDefault" }
        };
        var capabilities = new CapabilitiesV1();
        capabilities.Drop.Add("ALL");
        pod.Containers.Single().SecurityContext = new SecurityContextV1
        {
            AllowPrivilegeEscalation = false,
            Capabilities = capabilities
        };
    }

    private static ResourceRequirementsV1 Limits(string cpu, string memory, string cpuLimit, string memoryLimit) => new()
    {
        Requests = { ["cpu"] = cpu, ["memory"] = memory },
        Limits = { ["cpu"] = cpuLimit, ["memory"] = memoryLimit }
    };
}
