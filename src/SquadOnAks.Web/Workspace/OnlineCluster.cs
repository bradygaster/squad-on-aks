using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

public interface IOnlineCluster
{
    string Namespace { get; }
    Task<IKubernetesObject<V1ObjectMeta>?> ReadAsync(string kind, string name, CancellationToken cancellationToken);
    Task ApplyAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken);
    Task DeleteAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken);
}

public sealed class OnlineCluster : IOnlineCluster, IDisposable
{
    private readonly Lazy<Kubernetes> client = new(() => new(KubernetesClientConfiguration.InClusterConfig()));
    public string Namespace => File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/namespace").Trim();

    public async Task<IKubernetesObject<V1ObjectMeta>?> ReadAsync(string kind, string name, CancellationToken cancellationToken)
    {
        try
        {
            return kind switch
            {
                "ConfigMap" => await client.Value.CoreV1.ReadNamespacedConfigMapAsync(name, Namespace, cancellationToken: cancellationToken),
                "Deployment" => await client.Value.AppsV1.ReadNamespacedDeploymentAsync(name, Namespace, cancellationToken: cancellationToken),
                "Service" => await client.Value.CoreV1.ReadNamespacedServiceAsync(name, Namespace, cancellationToken: cancellationToken),
                "NetworkPolicy" => await client.Value.NetworkingV1.ReadNamespacedNetworkPolicyAsync(name, Namespace, cancellationToken: cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task ApplyAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken)
    {
        string name = resource.Metadata.Name;
        bool exists = resource.Metadata.ResourceVersion is not null;
        switch (resource)
        {
            case V1ConfigMap value:
                if (exists) await client.Value.CoreV1.ReplaceNamespacedConfigMapAsync(value, name, Namespace, cancellationToken: cancellationToken);
                else await client.Value.CoreV1.CreateNamespacedConfigMapAsync(value, Namespace, cancellationToken: cancellationToken);
                break;
            case V1Deployment value:
                if (exists) await client.Value.AppsV1.ReplaceNamespacedDeploymentAsync(value, name, Namespace, cancellationToken: cancellationToken);
                else await client.Value.AppsV1.CreateNamespacedDeploymentAsync(value, Namespace, cancellationToken: cancellationToken);
                break;
            case V1Service value:
                if (exists) await client.Value.CoreV1.ReplaceNamespacedServiceAsync(value, name, Namespace, cancellationToken: cancellationToken);
                else await client.Value.CoreV1.CreateNamespacedServiceAsync(value, Namespace, cancellationToken: cancellationToken);
                break;
            case V1NetworkPolicy value:
                if (exists) await client.Value.NetworkingV1.ReplaceNamespacedNetworkPolicyAsync(value, name, Namespace, cancellationToken: cancellationToken);
                else await client.Value.NetworkingV1.CreateNamespacedNetworkPolicyAsync(value, Namespace, cancellationToken: cancellationToken);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(resource));
        }
    }

    public async Task DeleteAsync(IKubernetesObject<V1ObjectMeta> resource, CancellationToken cancellationToken)
    {
        var options = new V1DeleteOptions
        {
            Preconditions = new() { Uid = resource.Metadata.Uid, ResourceVersion = resource.Metadata.ResourceVersion }
        };
        string name = resource.Metadata.Name;
        try
        {
            switch (resource.Kind)
            {
                case "ConfigMap":
                    await client.Value.CoreV1.DeleteNamespacedConfigMapAsync(name, Namespace, body: options, cancellationToken: cancellationToken); break;
                case "Deployment":
                    await client.Value.AppsV1.DeleteNamespacedDeploymentAsync(name, Namespace, body: options, cancellationToken: cancellationToken); break;
                case "Service":
                    await client.Value.CoreV1.DeleteNamespacedServiceAsync(name, Namespace, body: options, cancellationToken: cancellationToken); break;
                case "NetworkPolicy":
                    await client.Value.NetworkingV1.DeleteNamespacedNetworkPolicyAsync(name, Namespace, body: options, cancellationToken: cancellationToken); break;
                default: throw new ArgumentOutOfRangeException(nameof(resource));
            }
        }
        catch (HttpOperationException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound) { }
    }

    public void Dispose()
    {
        if (client.IsValueCreated) client.Value.Dispose();
    }
}
