using k8s.Models;

namespace SquadOnAks.Web.Workspace;

public sealed record AppWorkerManifests(
    V1ServiceAccount DeployServiceAccount,
    V1Job BuildJob,
    V1Job DeployJob,
    V1NetworkPolicy BuildNetworkPolicy);

public static class AppWorkerManifest
{
    public const string OwnerLabel = "daydream.dev/owner";
    public const string AppLabel = "daydream.dev/app-id";
    public const string StageLabel = "daydream.dev/worker-stage";
    public const string WorkerRunLabel = "daydream.dev/worker-run";
    public const string Owner = "app-worker";
    public const string DeployerServiceAccount = "app-deployer";
    public const string WorkspaceVolume = "workspace";
    public const string ArtifactVolume = "artifacts";

    public static string NamespaceName(string appId)
    {
        _ = StaticAppManifest.Name(appId);
        return $"daydream-app-{appId}";
    }

    public static V1Job CreateValidationJob(
        string jobName,
        string appId,
        string appNamespace,
        string workerImage,
        string workspaceClaim,
        string workspaceSubPath,
        string? imagePullSecret = null)
    {
        _ = StaticAppManifest.Name(appId);
        RequireDnsLabel(jobName, nameof(jobName));
        RequireDnsLabel(appNamespace, nameof(appNamespace));
        RequireDnsLabel(workspaceClaim, nameof(workspaceClaim));
        RequireOptionalDnsLabel(imagePullSecret);
        if (!IsSha256ImageReference(workerImage))
        {
            throw new ArgumentException("The worker image must be pinned by a lowercase sha256 digest.", nameof(workerImage));
        }

        if (string.IsNullOrWhiteSpace(workspaceSubPath) ||
            workspaceSubPath.StartsWith("/", StringComparison.Ordinal) ||
            workspaceSubPath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A normalized workspace subpath is required.", nameof(workspaceSubPath));
        }

        var labels = new Dictionary<string, string>
        {
            [OwnerLabel] = Owner,
            [AppLabel] = appId,
            [StageLabel] = "validation",
            [WorkerRunLabel] = jobName
        };
        var container = new V1Container
        {
            Name = "validator",
            Image = workerImage,
            ImagePullPolicy = "IfNotPresent",
            Command = ["sh"],
            Args = ["-c", "set -eu; unset COPILOT_TOKEN GITHUB_TOKEN; mkdir -p /tmp/home /tmp/source; " +
                "cp -R /workspace/. /tmp/source/; cd /tmp/source; exec sh tests/run.sh"],
            WorkingDir = "/tmp",
            Env = [new V1EnvVar { Name = "TMPDIR", Value = "/tmp" },
                new V1EnvVar { Name = "HOME", Value = "/tmp/home" },
                new V1EnvVar { Name = "DOTNET_CLI_HOME", Value = "/tmp/home" }],
            VolumeMounts =
            [
                new V1VolumeMount
                {
                    Name = WorkspaceVolume,
                    MountPath = "/workspace",
                    ReadOnlyProperty = true,
                    SubPath = workspaceSubPath
                },
                new V1VolumeMount { Name = "tmp", MountPath = "/tmp" }
            ],
            SecurityContext = ContainerSecurityContext(),
            Resources = new V1ResourceRequirements
            {
                Requests = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new("100m"),
                    ["memory"] = new("128Mi"),
                    ["ephemeral-storage"] = new("64Mi")
                },
                Limits = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new("1"),
                    ["memory"] = new("1Gi"),
                    ["ephemeral-storage"] = new("512Mi")
                }
            }
        };
        var podSpec = new V1PodSpec
        {
            ServiceAccountName = "default",
            AutomountServiceAccountToken = false,
            RestartPolicy = "Never",
            SecurityContext = new V1PodSecurityContext
            {
                RunAsNonRoot = true,
                RunAsUser = 1654,
                RunAsGroup = 1654,
                FsGroup = 1654,
                SeccompProfile = new V1SeccompProfile { Type = "RuntimeDefault" }
            },
            Containers = [container],
            Volumes =
            [
                new V1Volume
                {
                    Name = WorkspaceVolume,
                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                    {
                        ClaimName = workspaceClaim,
                        ReadOnlyProperty = true
                    }
                },
                new V1Volume
                {
                    Name = "tmp",
                    EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = "512Mi" }
                }
            ]
        };
        if (imagePullSecret is not null)
        {
            podSpec.ImagePullSecrets = [new V1LocalObjectReference { Name = imagePullSecret }];
        }

        return new V1Job
        {
            ApiVersion = "batch/v1",
            Kind = "Job",
            Metadata = new V1ObjectMeta
            {
                Name = jobName,
                NamespaceProperty = appNamespace,
                Labels = labels
            },
            Spec = new V1JobSpec
            {
                BackoffLimit = 0,
                ActiveDeadlineSeconds = 600,
                TtlSecondsAfterFinished = 300,
                Parallelism = 1,
                Completions = 1,
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = podSpec
                }
            }
        };
    }

    public static AppWorkerManifests Create(
        string appId,
        string runId,
        string coordinatorNamespace,
        string appNamespace,
        string workerImage,
        string sharedWorkspaceClaim,
        string workspaceSubPath,
        string artifactSubPath,
        string? imagePullSecret = null,
        string? registryPushEndpoint = null,
        string? registryPullEndpoint = null)
    {
        string appName = StaticAppManifest.Name(appId);
        string deployServiceAccountName = $"{DeployerServiceAccount}-{appId}";
        RequireDnsLabel(runId, nameof(runId));
        if (runId.Length > 16)
        {
            throw new ArgumentException("The worker run identifier must be at most 16 characters.", nameof(runId));
        }

        RequireDnsLabel(coordinatorNamespace, nameof(coordinatorNamespace));
        RequireDnsLabel(appNamespace, nameof(appNamespace));
        if (string.Equals(coordinatorNamespace, appNamespace, StringComparison.Ordinal))
        {
            throw new ArgumentException("The app namespace must be separate from the coordinator namespace.", nameof(appNamespace));
        }

        RequireDnsLabel(sharedWorkspaceClaim, nameof(sharedWorkspaceClaim));
        RequireSubPath(workspaceSubPath, nameof(workspaceSubPath));
        RequireSubPath(artifactSubPath, nameof(artifactSubPath));
        RequireOptionalDnsLabel(imagePullSecret);
        if (!IsSha256ImageReference(workerImage))
        {
            throw new ArgumentException("The worker image must be pinned by a lowercase sha256 digest.", nameof(workerImage));
        }

        Dictionary<string, string> BuildLabels() => new()
        {
            [OwnerLabel] = Owner,
            [AppLabel] = appId,
            [StageLabel] = "build",
            [WorkerRunLabel] = runId
        };

        Dictionary<string, string> DeployLabels() => new()
        {
            [OwnerLabel] = Owner,
            [AppLabel] = appId,
            [StageLabel] = "deploy",
            [WorkerRunLabel] = runId
        };

        V1Job BuildJob() => CreateJob(
            JobName(appId, "build", runId),
            coordinatorNamespace,
            BuildLabels(),
            workerImage,
            "/worker/build",
            ["/workspace", "/artifacts"],
            "default",
            automountServiceAccountToken: false,
            sharedWorkspaceClaim,
            workspaceSubPath,
            artifactSubPath,
            imagePullSecret,
            readOnlyWorkspace: true);

        V1Job DeployJob() => CreateJob(
            JobName(appId, "deploy", runId),
            coordinatorNamespace,
            DeployLabels(),
            workerImage,
            "/worker/deploy",
            ["upgrade", "--install", appName, "/artifacts/package.tgz", "--namespace", appNamespace,
                "--wait", "--atomic", "--timeout", "10m"],
            deployServiceAccountName,
            automountServiceAccountToken: true,
            sharedWorkspaceClaim,
            workspaceSubPath: null,
            artifactSubPath,
            imagePullSecret,
            readOnlyWorkspace: true);

        var deployServiceAccount = new V1ServiceAccount
        {
            ApiVersion = "v1",
            Kind = "ServiceAccount",
            Metadata = new V1ObjectMeta
            {
                Name = deployServiceAccountName,
                NamespaceProperty = coordinatorNamespace,
                Labels = new Dictionary<string, string>
                {
                    [OwnerLabel] = Owner,
                    [AppLabel] = appId
                }
            },
            AutomountServiceAccountToken = true
        };
        var buildNetworkPolicy = new V1NetworkPolicy
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "NetworkPolicy",
            Metadata = new V1ObjectMeta
            {
                Name = $"{appName}-build-egress-deny",
                NamespaceProperty = coordinatorNamespace,
                Labels = BuildLabels()
            },
            Spec = new V1NetworkPolicySpec
            {
                PodSelector = new V1LabelSelector { MatchLabels = BuildLabels() },
                PolicyTypes = ["Egress"],
                Egress = []
            }
        };

        V1Job deployJob = DeployJob();
        if (registryPushEndpoint is not null && registryPullEndpoint is not null)
        {
            deployJob.Spec.Template.Spec.Containers.Single().Env.Add(
                new V1EnvVar { Name = "DAYDREAM_REGISTRY_PUSH", Value = registryPushEndpoint });
            deployJob.Spec.Template.Spec.Containers.Single().Env.Add(
                new V1EnvVar { Name = "DAYDREAM_REGISTRY_PULL", Value = registryPullEndpoint });
        }

        return new(deployServiceAccount, BuildJob(), deployJob, buildNetworkPolicy);
    }

    private static V1Job CreateJob(
        string name,
        string appNamespace,
        Dictionary<string, string> labels,
        string image,
        string command,
        IReadOnlyList<string> arguments,
        string serviceAccountName,
        bool automountServiceAccountToken,
        string sharedWorkspaceClaim,
        string? workspaceSubPath,
        string artifactSubPath,
        string? imagePullSecret,
        bool readOnlyWorkspace)
    {
        var container = new V1Container
        {
            Name = "worker",
            Image = image,
            ImagePullPolicy = "IfNotPresent",
            Command = [command],
            Args = arguments.ToList(),
            Env =
            [
                new V1EnvVar { Name = "HELM_DRIVER", Value = "configmap" },
                new V1EnvVar { Name = "DAYDREAM_ARTIFACT_PATH", Value = "/artifacts" }
            ],
            VolumeMounts =
            [
                new V1VolumeMount
                {
                    Name = ArtifactVolume,
                    MountPath = "/artifacts",
                    ReadOnlyProperty = command == "/worker/deploy",
                    SubPath = artifactSubPath
                },
                new V1VolumeMount { Name = "tmp", MountPath = "/tmp" }
            ],
            SecurityContext = ContainerSecurityContext(),
            Resources = new V1ResourceRequirements
            {
                Requests = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new("250m"),
                    ["memory"] = new("256Mi"),
                    ["ephemeral-storage"] = new("128Mi")
                },
                Limits = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new("1"),
                    ["memory"] = new("1Gi"),
                    ["ephemeral-storage"] = new("1Gi")
                }
            }
        };

        var volumes = new List<V1Volume>
        {
            new()
            {
                Name = ArtifactVolume,
                PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                {
                    ClaimName = sharedWorkspaceClaim,
                    ReadOnlyProperty = command == "/worker/deploy"
                }
            },
            new()
            {
                Name = "tmp",
                EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = "1Gi" }
            }
        };
        if (workspaceSubPath is not null)
        {
            volumes.Add(new V1Volume
            {
                Name = WorkspaceVolume,
                PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                {
                    ClaimName = sharedWorkspaceClaim,
                    ReadOnlyProperty = false
                }
            });
            container.VolumeMounts.Add(new V1VolumeMount
            {
                Name = WorkspaceVolume,
                MountPath = "/workspace",
                ReadOnlyProperty = readOnlyWorkspace,
                SubPath = workspaceSubPath
            });
        }

        var podSpec = new V1PodSpec
        {
            ServiceAccountName = serviceAccountName,
            AutomountServiceAccountToken = automountServiceAccountToken,
            RestartPolicy = "Never",
            SecurityContext = new V1PodSecurityContext
            {
                RunAsNonRoot = true,
                RunAsUser = 1654,
                RunAsGroup = 1654,
                FsGroup = 1654,
                SeccompProfile = new V1SeccompProfile { Type = "RuntimeDefault" }
            },
            Containers = [container],
            Volumes = volumes
        };
        if (imagePullSecret is not null)
        {
            podSpec.ImagePullSecrets = [new V1LocalObjectReference { Name = imagePullSecret }];
        }

        return new V1Job
        {
            ApiVersion = "batch/v1",
            Kind = "Job",
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = appNamespace,
                Labels = labels
            },
            Spec = new V1JobSpec
            {
                BackoffLimit = 0,
                ActiveDeadlineSeconds = 1800,
                TtlSecondsAfterFinished = 3600,
                Parallelism = 1,
                Completions = 1,
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = podSpec
                }
            }
        };
    }

    private static V1SecurityContext ContainerSecurityContext() => new()
    {
        RunAsNonRoot = true,
        RunAsUser = 1654,
        RunAsGroup = 1654,
        AllowPrivilegeEscalation = false,
        ReadOnlyRootFilesystem = true,
        Capabilities = new V1Capabilities { Drop = ["ALL"] },
        SeccompProfile = new V1SeccompProfile { Type = "RuntimeDefault" }
    };

    private static void RequireDnsLabel(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 63 ||
            (value[0] is < 'a' or > 'z') && (value[0] is < '0' or > '9') ||
            (value[^1] is < 'a' or > 'z') && (value[^1] is < '0' or > '9') ||
            value.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))
        {
            throw new ArgumentException("A valid lowercase Kubernetes DNS label is required.", parameterName);
        }
    }

    private static string JobName(string appId, string stage, string runId)
    {
        string appToken = appId == AppLibrary.FirstAppId ? "first" : appId[..12];
        return $"daydream-{stage}-{appToken}-{runId}";
    }

    private static bool IsSha256ImageReference(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return false;
        }

        int separator = image.LastIndexOf('@');
        if (separator <= 0 || image.IndexOf('@') != separator || image.Any(char.IsWhiteSpace))
        {
            return false;
        }

        ReadOnlySpan<char> digest = image.AsSpan(separator + 1);
        if (!digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71)
        {
            return false;
        }

        foreach (char character in digest[7..])
        {
            if (!char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireOptionalDnsLabel(string? value)
    {
        if (value is not null)
        {
            RequireDnsLabel(value, nameof(value));
        }
    }

    private static void RequireSubPath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A normalized workspace subpath is required.", parameterName);
        }
    }
}
