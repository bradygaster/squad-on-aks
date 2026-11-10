using System.Net;

namespace SquadOnAks.Web.Workspace;

public interface IDeployedAppReadinessProbe
{
    /// <summary>
    /// True only when the deployed front end serves HTML and every declared health path
    /// (which the app contract requires to check its database) answers 200.
    /// </summary>
    Task<bool> IsReadyAsync(Uri origin, IReadOnlyList<string> healthPaths, CancellationToken cancellationToken);
}

public sealed class DeployedAppReadinessProbe(IHttpClientFactory clients) : IDeployedAppReadinessProbe
{
    public const string HttpClientName = "DaydreamAppReadiness";
    public static readonly IReadOnlyList<string> DefaultHealthPaths = ["/health/ready"];

    public async Task<bool> IsReadyAsync(Uri origin, IReadOnlyList<string> healthPaths, CancellationToken cancellationToken)
    {
        if (healthPaths.Count == 0 || healthPaths.Any(path => !AppBuildManifest.ValidHttpPath(path)))
        {
            return false;
        }

        using HttpClient client = clients.CreateClient(HttpClientName);
        using HttpResponseMessage frontend = await client.GetAsync(
            new Uri(origin, "/"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (frontend.StatusCode != HttpStatusCode.OK ||
            !string.Equals(frontend.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string path in healthPaths)
        {
            using HttpResponseMessage health = await client.GetAsync(
                new Uri(origin, path),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (health.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }
        }

        return true;
    }
}
