using System.Net;
using System.Net.Http.Headers;

namespace SquadOnAks.Web.Workspace;

public interface IAppNamespaceProvisioner
{
    Task EnsureAsync(string appId, CancellationToken cancellationToken);
    Task DeleteAsync(string appId, CancellationToken cancellationToken);
    /// <summary>A throwaway namespace (<c>soaks-check-{runId}</c>) that tests one revision before it goes online.</summary>
    Task EnsureCheckAsync(string appId, string runId, CancellationToken cancellationToken);
    Task DeleteCheckAsync(string appId, string runId, CancellationToken cancellationToken);
}

public sealed class HttpAppNamespaceProvisioner(
    IHttpClientFactory clients,
    string endpoint) : IAppNamespaceProvisioner
{
    public const string HttpClientName = "AppNamespaceProvisioner";
    private const string ServiceAccountTokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    public Task EnsureAsync(string appId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, appId, cancellationToken);

    public Task DeleteAsync(string appId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, appId, cancellationToken);

    public Task EnsureCheckAsync(string appId, string runId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, appId, cancellationToken, runId);

    public Task DeleteCheckAsync(string appId, string runId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, appId, cancellationToken, runId);

    private async Task SendAsync(HttpMethod method, string appId, CancellationToken cancellationToken, string? checkRunId = null)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? baseAddress) ||
            baseAddress.Scheme is not "http" and not "https")
        {
            throw new InvalidOperationException("The app namespace provisioner endpoint is not configured.");
        }

        string token = (await File.ReadAllTextAsync(ServiceAccountTokenPath, cancellationToken)).Trim();
        if (token.Length == 0)
        {
            throw new InvalidOperationException("The host service account token is unavailable.");
        }

        using var request = new HttpRequestMessage(
            method,
            new Uri(baseAddress, checkRunId is null
                ? $"apps/{Uri.EscapeDataString(appId)}"
                : $"apps/{Uri.EscapeDataString(appId)}/checks/{Uri.EscapeDataString(checkRunId)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await clients.CreateClient(HttpClientName)
            .SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("The app namespace provisioner rejected this request.");
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

    // Without a provisioner nothing was ever deployed into a namespace, so there is nothing to remove.
    public Task DeleteAsync(string appId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureCheckAsync(string appId, string runId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("App namespace provisioning is not configured for this host.");

    public Task DeleteCheckAsync(string appId, string runId, CancellationToken cancellationToken) => Task.CompletedTask;
}
