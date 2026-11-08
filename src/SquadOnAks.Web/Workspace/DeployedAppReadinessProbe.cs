using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SquadOnAks.Web.Workspace;

public interface IDeployedAppReadinessProbe
{
    Task<bool> IsReadyAsync(Uri origin, CancellationToken cancellationToken);
}

public sealed class DeployedAppReadinessProbe(IHttpClientFactory clients) : IDeployedAppReadinessProbe
{
    public const string HttpClientName = "DaydreamAppReadiness";
    private const int MaxApiResponseBytes = 4096;

    public async Task<bool> IsReadyAsync(Uri origin, CancellationToken cancellationToken)
    {
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

        using HttpResponseMessage api = await client.GetAsync(
            new Uri(origin, "/api/answer"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (api.StatusCode != HttpStatusCode.OK ||
            !string.Equals(api.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            api.Content.Headers.ContentLength is > MaxApiResponseBytes)
        {
            return false;
        }

        await using Stream stream = await api.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxApiResponseBytes)
            {
                return false;
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            AnswerResponse? answer = JsonSerializer.Deserialize<AnswerResponse>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            return !string.IsNullOrWhiteSpace(answer?.Answer) && answer.Answer.Length <= 200;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record AnswerResponse([property: JsonPropertyName("answer")] string? Answer);
}
