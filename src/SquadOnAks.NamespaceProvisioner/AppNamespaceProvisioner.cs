using System.Net;
using System.Text.RegularExpressions;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace SquadOnAks.NamespaceProvisioner;

public sealed record AppNamespaceResources(
    V1Namespace Namespace,
    V1ServiceAccount DefaultServiceAccount,
    V1Role DeployerRole,
    V1RoleBinding DeployerRoleBinding,
    V1NetworkPolicy Boundary);

public interface IAppNamespaceResourceStore
{
    Task EnsureAsync(AppNamespaceResources resources, CancellationToken cancellationToken);
}

public interface IAppNamespaceProvisioner
{
    Task<string> EnsureAsync(string appId, CancellationToken cancellationToken);
}

public interface IServiceAccountTokenValidator
{
    Task<bool> IsDaydreamManagerAsync(string token, CancellationToken cancellationToken);
}

public sealed class AppNamespaceProvisioner(
    IAppNamespaceResourceStore resources) : IAppNamespaceProvisioner
{
    public async Task<string> EnsureAsync(string appId, CancellationToken cancellationToken)
    {
        string coordinatorNamespace = File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/namespace").Trim();
        AppNamespaceResources desired = AppNamespaceManifest.Create(appId, coordinatorNamespace);
        await resources.EnsureAsync(desired, cancellationToken);
        return desired.Namespace.Metadata.Name;
    }
}

public static partial class AppNamespaceManifest
{
    public const string OwnerLabel = "daydream.dev/owner";
    public const string AppLabel = "daydream.dev/app-id";
    public const string Owner = "app-namespace-provisioner";
    public const string DeployerRoleName = "daydream-app-deployer";
    private const string DeployerServiceAccount = "app-deployer";
    private const string BoundaryPolicyName = "daydream-app-boundary";

    public static AppNamespaceResources Create(string appId, string coordinatorNamespace)
    {
        if (appId != "first" && (!Guid.TryParseExact(appId, "N", out _) ||
            appId.Any(char.IsAsciiLetterUpper)))
        {
            throw new ArgumentException("The app identifier is invalid.", nameof(appId));
        }

        if (!IsDnsLabel(coordinatorNamespace))
        {
            throw new ArgumentException("The coordinator namespace is invalid.", nameof(coordinatorNamespace));
        }

        string appNamespace = $"daydream-app-{appId}";
        string deployServiceAccount = $"{DeployerServiceAccount}-{appId}";
        Dictionary<string, string> ownership = new()
        {
            [OwnerLabel] = Owner,
            [AppLabel] = appId
        };
        var appNamespaceResource = new V1Namespace
        {
            ApiVersion = "v1",
            Kind = "Namespace",
            Metadata = new V1ObjectMeta
            {
                Name = appNamespace,
                Labels = new Dictionary<string, string>(ownership)
                {
                    ["pod-security.kubernetes.io/audit"] = "restricted",
                    ["pod-security.kubernetes.io/enforce"] = "restricted",
                    ["pod-security.kubernetes.io/warn"] = "restricted"
                }
            }
        };
        var defaultServiceAccount = new V1ServiceAccount
        {
            ApiVersion = "v1",
            Kind = "ServiceAccount",
            Metadata = new V1ObjectMeta
            {
                Name = "default",
                NamespaceProperty = appNamespace,
                Labels = new Dictionary<string, string>(ownership)
            },
            AutomountServiceAccountToken = false
        };
        var deployRole = new V1Role
        {
            ApiVersion = "rbac.authorization.k8s.io/v1",
            Kind = "Role",
            Metadata = new V1ObjectMeta
            {
                Name = DeployerRoleName,
                NamespaceProperty = appNamespace,
                Labels = new Dictionary<string, string>(ownership)
            },
            Rules =
            [
                new V1PolicyRule
                {
                    ApiGroups = ["", "apps"],
                    Resources = ["configmaps", "services", "deployments"],
                    Verbs = ["get", "list", "watch", "create", "update", "patch", "delete"]
                },
                new V1PolicyRule
                {
                    ApiGroups = ["", "apps"],
                    Resources = ["pods", "replicasets"],
                    Verbs = ["get", "list", "watch"]
                }
            ]
        };
        var roleBinding = new V1RoleBinding
        {
            ApiVersion = "rbac.authorization.k8s.io/v1",
            Kind = "RoleBinding",
            Metadata = new V1ObjectMeta
            {
                Name = DeployerRoleName,
                NamespaceProperty = appNamespace,
                Labels = new Dictionary<string, string>(ownership)
            },
            RoleRef = new V1RoleRef
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = "Role",
                Name = DeployerRoleName
            },
            Subjects =
            [
                new Rbacv1Subject
                {
                    Kind = "ServiceAccount",
                    Name = deployServiceAccount,
                    NamespaceProperty = coordinatorNamespace
                }
            ]
        };
        var boundary = new V1NetworkPolicy
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "NetworkPolicy",
            Metadata = new V1ObjectMeta
            {
                Name = BoundaryPolicyName,
                NamespaceProperty = appNamespace,
                Labels = new Dictionary<string, string>(ownership)
            },
            Spec = new V1NetworkPolicySpec
            {
                PodSelector = new V1LabelSelector(),
                PolicyTypes = ["Ingress", "Egress"],
                Ingress =
                [
                    new V1NetworkPolicyIngressRule
                    {
                        FromProperty =
                        [
                            new V1NetworkPolicyPeer
                            {
                                NamespaceSelector = new V1LabelSelector
                                {
                                    MatchLabels = new Dictionary<string, string>
                                    {
                                        ["kubernetes.io/metadata.name"] = coordinatorNamespace
                                    }
                                },
                                PodSelector = new V1LabelSelector
                                {
                                    MatchLabels = new Dictionary<string, string>
                                    {
                                        ["daydream.dev/host"] = "true"
                                    }
                                }
                            }
                        ]
                    }
                ],
                Egress = []
            }
        };

        return new(appNamespaceResource, defaultServiceAccount, deployRole, roleBinding, boundary);
    }

    private static bool IsDnsLabel(string value) =>
        value.Length is > 0 and <= 63 &&
        DnsLabelRegex().IsMatch(value);

    [GeneratedRegex("^[a-z0-9](?:[-a-z0-9]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex DnsLabelRegex();
}
public sealed class KubernetesAppNamespaceResourceStore : IAppNamespaceResourceStore, IDisposable
{
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));

    public async Task EnsureAsync(AppNamespaceResources resources, CancellationToken cancellationToken)
    {
        await EnsureNamespaceAsync(resources.Namespace, cancellationToken);
        await EnsureDefaultServiceAccountAsync(resources.DefaultServiceAccount, cancellationToken);
        await EnsureRoleAsync(resources.DeployerRole, cancellationToken);
        await EnsureRoleBindingAsync(resources.DeployerRoleBinding, cancellationToken);
        await EnsureBoundaryAsync(resources.Boundary, cancellationToken);
    }

    private async Task EnsureNamespaceAsync(V1Namespace desired, CancellationToken cancellationToken)
    {
        V1Namespace? existing = await ReadNamespaceAsync(desired.Metadata.Name, cancellationToken);
        if (existing is null)
        {
            try
            {
                await client.Value.CoreV1.CreateNamespaceAsync(desired, cancellationToken: cancellationToken);
                return;
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
            {
                existing = await ReadNamespaceAsync(desired.Metadata.Name, cancellationToken);
                if (existing is null) throw;
            }
        }

        RequireOwned(existing.Metadata, desired.Metadata.Labels);
        existing.Metadata.Labels ??= new Dictionary<string, string>();
        foreach ((string key, string value) in desired.Metadata.Labels)
        {
            existing.Metadata.Labels[key] = value;
        }

        await client.Value.CoreV1.ReplaceNamespaceAsync(
            existing,
            existing.Metadata.Name,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureDefaultServiceAccountAsync(
        V1ServiceAccount desired,
        CancellationToken cancellationToken)
    {
        V1ServiceAccount? existing = await ReadServiceAccountAsync(
            desired.Metadata.NamespaceProperty,
            desired.Metadata.Name,
            cancellationToken);
        if (existing is null)
        {
            try
            {
                await client.Value.CoreV1.CreateNamespacedServiceAccountAsync(
                    desired,
                    desired.Metadata.NamespaceProperty,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
            {
                existing = await ReadServiceAccountAsync(
                    desired.Metadata.NamespaceProperty,
                    desired.Metadata.Name,
                    cancellationToken);
                if (existing is null) throw;
            }
        }

        RequireOwnedOrUnlabeledDefaultServiceAccount(existing.Metadata, desired.Metadata.Labels);
        existing.AutomountServiceAccountToken = false;
        existing.Metadata.Labels ??= new Dictionary<string, string>();
        foreach ((string key, string value) in desired.Metadata.Labels)
        {
            existing.Metadata.Labels[key] = value;
        }

        await client.Value.CoreV1.ReplaceNamespacedServiceAccountAsync(
            existing,
            existing.Metadata.Name,
            existing.Metadata.NamespaceProperty,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureRoleAsync(V1Role desired, CancellationToken cancellationToken)
    {
        V1Role? existing = await ReadRoleAsync(desired.Metadata.NamespaceProperty, desired.Metadata.Name, cancellationToken);
        if (existing is null)
        {
            try
            {
                await client.Value.RbacAuthorizationV1.CreateNamespacedRoleAsync(
                    desired,
                    desired.Metadata.NamespaceProperty,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
            {
                existing = await ReadRoleAsync(desired.Metadata.NamespaceProperty, desired.Metadata.Name, cancellationToken);
                if (existing is null) throw;
            }
        }

        RequireOwned(existing.Metadata, desired.Metadata.Labels);
        desired.Metadata.ResourceVersion = existing.Metadata.ResourceVersion;
        await client.Value.RbacAuthorizationV1.ReplaceNamespacedRoleAsync(
            desired,
            desired.Metadata.Name,
            desired.Metadata.NamespaceProperty,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureRoleBindingAsync(V1RoleBinding desired, CancellationToken cancellationToken)
    {
        V1RoleBinding? existing = await ReadRoleBindingAsync(
            desired.Metadata.NamespaceProperty,
            desired.Metadata.Name,
            cancellationToken);
        if (existing is null)
        {
            try
            {
                await client.Value.RbacAuthorizationV1.CreateNamespacedRoleBindingAsync(
                    desired,
                    desired.Metadata.NamespaceProperty,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
            {
                existing = await ReadRoleBindingAsync(
                    desired.Metadata.NamespaceProperty,
                    desired.Metadata.Name,
                    cancellationToken);
                if (existing is null) throw;
            }
        }

        RequireOwned(existing.Metadata, desired.Metadata.Labels);
        desired.Metadata.ResourceVersion = existing.Metadata.ResourceVersion;
        await client.Value.RbacAuthorizationV1.ReplaceNamespacedRoleBindingAsync(
            desired,
            desired.Metadata.Name,
            desired.Metadata.NamespaceProperty,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureBoundaryAsync(V1NetworkPolicy desired, CancellationToken cancellationToken)
    {
        V1NetworkPolicy? existing = await ReadBoundaryAsync(
            desired.Metadata.NamespaceProperty,
            desired.Metadata.Name,
            cancellationToken);
        if (existing is null)
        {
            try
            {
                await client.Value.NetworkingV1.CreateNamespacedNetworkPolicyAsync(
                    desired,
                    desired.Metadata.NamespaceProperty,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
            {
                existing = await ReadBoundaryAsync(
                    desired.Metadata.NamespaceProperty,
                    desired.Metadata.Name,
                    cancellationToken);
                if (existing is null) throw;
            }
        }

        RequireOwned(existing.Metadata, desired.Metadata.Labels);
        desired.Metadata.ResourceVersion = existing.Metadata.ResourceVersion;
        await client.Value.NetworkingV1.ReplaceNamespacedNetworkPolicyAsync(
            desired,
            desired.Metadata.Name,
            desired.Metadata.NamespaceProperty,
            cancellationToken: cancellationToken);
    }

    private async Task<V1Namespace?> ReadNamespaceAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.CoreV1.ReadNamespaceAsync(name, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<V1ServiceAccount?> ReadServiceAccountAsync(
        string namespaceName,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.CoreV1.ReadNamespacedServiceAccountAsync(
                name,
                namespaceName,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<V1Role?> ReadRoleAsync(string namespaceName, string name, CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.RbacAuthorizationV1.ReadNamespacedRoleAsync(
                name,
                namespaceName,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<V1RoleBinding?> ReadRoleBindingAsync(
        string namespaceName,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.RbacAuthorizationV1.ReadNamespacedRoleBindingAsync(
                name,
                namespaceName,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<V1NetworkPolicy?> ReadBoundaryAsync(
        string namespaceName,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.Value.NetworkingV1.ReadNamespacedNetworkPolicyAsync(
                name,
                namespaceName,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static void RequireOwned(V1ObjectMeta actual, IDictionary<string, string> expectedLabels)
    {
        string[] ownershipKeys = [AppNamespaceManifest.OwnerLabel, AppNamespaceManifest.AppLabel];
        if (ownershipKeys.Any(key =>
            actual.Labels is null || !actual.Labels.TryGetValue(key, out string? value) ||
            value != expectedLabels[key]))
        {
            throw new InvalidOperationException("An app namespace resource is not owned by Daydream.");
        }
    }

    private static void RequireOwnedOrUnlabeledDefaultServiceAccount(
        V1ObjectMeta actual,
        IDictionary<string, string> expectedLabels)
    {
        if (expectedLabels.Keys.Any(key => actual.Labels?.ContainsKey(key) == true))
        {
            RequireOwned(actual, expectedLabels);
        }
    }

    public void Dispose()
    {
        if (client.IsValueCreated) client.Value.Dispose();
    }
}

public sealed class KubernetesServiceAccountTokenValidator : IServiceAccountTokenValidator, IDisposable
{
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));
    private readonly Lazy<string> coordinatorNamespace = new(() =>
        File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/namespace").Trim());

    public async Task<bool> IsDaydreamManagerAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        V1TokenReview review = await client.Value.AuthenticationV1.CreateTokenReviewAsync(
            new V1TokenReview
            {
                ApiVersion = "authentication.k8s.io/v1",
                Kind = "TokenReview",
                Spec = new V1TokenReviewSpec { Token = token }
            },
            cancellationToken: cancellationToken);
        return review.Status?.Authenticated == true &&
            review.Status.User?.Username ==
                $"system:serviceaccount:{coordinatorNamespace.Value}:daydream-app-manager";
    }

    public void Dispose()
    {
        if (client.IsValueCreated) client.Value.Dispose();
    }
}
