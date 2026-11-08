using System.Net;
using System.Net.Http.Headers;

namespace SquadOnAks.Web.Workspace;

public interface IAppNamespaceProvisioner
{
    Task EnsureAsync(string appId, CancellationToken cancellationToken);
}

public sealed class HttpAppNamespaceProvisioner(
    IHttpClientFactory clients,
    string endpoint) : IAppNamespaceProvisioner
{
    public const string HttpClientName = "DaydreamAppNamespaceProvisioner";
    private const string ServiceAccountTokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    public async Task EnsureAsync(string appId, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? baseAddress) ||
            baseAddress.Scheme is not "http" and not "https")
        {
            throw new InvalidOperationException("The app namespace provisioner endpoint is not configured.");
        }

        string token = (await File.ReadAllTextAsync(ServiceAccountTokenPath, cancellationToken)).Trim();
        if (token.Length == 0)
        {
            throw new InvalidOperationException("The Daydream service account token is unavailable.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseAddress, $"apps/{Uri.EscapeDataString(appId)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await clients.CreateClient(HttpClientName)
            .SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("The app namespace provisioner rejected this deployment.");
        }

        throw new HttpRequestException(
            "The app namespace provisioner could not complete the request.",
            null,
            response.StatusCode);
    }
}

public sealed class UnavailableAppNamespaceProvisioner : IAppNamespaceProvisioner
{
    public Task EnsureAsync(string appId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App namespace provisioning is not configured for this host.");
}
