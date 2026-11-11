using k8s.Autorest;
using k8s.Exceptions;
using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public static class OnlineEndpoints
{
    public const string HttpClientName = "OnlineApps";
    public const int MaxRequestBytes = 256 * 1024;
    public const string SandboxPolicy = "sandbox allow-scripts allow-forms allow-popups allow-modals";

    public static void MapOnlineEndpoints(this WebApplication app)
    {
        app.MapGet("/workspace/online", (AppOnlineService online, HttpContext context) =>
            ExecuteAsync(() => online.GetAsync(context.RequestAborted), context));
        app.MapPost("/workspace/online", (AppOnlineService online, OnlineHostingSettings hosting, WorkLifecycle lifecycle, HttpContext context) =>
            ChangeAsync(online.PutAsync, hosting, lifecycle, context));
        app.MapDelete("/workspace/online", (AppOnlineService online, OnlineHostingSettings hosting, WorkLifecycle lifecycle, HttpContext context) =>
            ChangeAsync(online.RemoveAsync, hosting, lifecycle, context));
        app.MapMethods("/online/{id}/{**path}", ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"], ProxyAsync);
        app.MapMethods("/online/{id}", ["GET", "HEAD"], async (
            string id, HttpContext context, AppLibrary library, IHttpClientFactory clients) =>
        {
            if (StaticAppManifest.ValidId(id) && !context.Request.Path.Value!.EndsWith('/'))
            {
                context.Response.Redirect($"/online/{id}/");
                return;
            }
            await ProxyAsync(id, null, context, library, clients);
        });
    }

    private static async Task<IResult> ChangeAsync(
        Func<CancellationToken, Task<AppOnlineStatus>> action, OnlineHostingSettings hosting, WorkLifecycle lifecycle, HttpContext context)
    {
        if (!hosting.Available) return Results.Text(AppOnlineService.LocalMessage, statusCode: 422);
        string origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site" ||
            origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? source) ||
                source.GetLeftPart(UriPartial.Authority) != $"{context.Request.Scheme}://{context.Request.Host}"))
            return Results.Text("Open this action from Squad.", statusCode: 403);
        WorkLifecycle.WorkLease? lease = lifecycle.TryBegin("publish");
        if (lease is null)
            return Results.Text("Wait for this app to finish its current work.", statusCode: 409);
        WorkTerminalResult outcome = WorkTerminalResult.Failed;
        try
        {
            // A deploy must finish even if the page that started it reloads or closes; only Stop cancels it.
            // Otherwise a refresh during "Updating…" leaves the revision half-built and the app not online.
            IResult result = await ExecuteAsync(() => action(lease.CancellationToken), context);
            if (result is not IStatusCodeHttpResult { StatusCode: >= 400 }) outcome = WorkTerminalResult.Completed;
            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = WorkTerminalResult.Cancelled;
            throw;
        }
        finally { lease.Complete(outcome, outcome == WorkTerminalResult.Completed ? "Online action finished." : "Online action did not finish."); }
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<AppOnlineStatus>> action, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        try { return Results.Ok(await action()); }
        catch (InvalidOperationException exception)
        {
            context.RequestServices.GetRequiredService<ILogger<AppOnlineService>>()
                .LogWarning(exception, "App online action was rejected.");
            string message = exception is AppDeploymentJobFailedException failed
                ? AppDeploymentFailure.Message(failed.Reason) ?? "Your app could not go online. Open What’s happening for the step that failed."
                : exception.Message;
            return Results.Text(message, statusCode: 422);
        }
        catch (Exception exception) when (exception is HttpOperationException or HttpRequestException or IOException or
            System.Text.Json.JsonException or KubernetesClientException or k8s.KubernetesException ||
            exception is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            context.RequestServices.GetRequiredService<ILogger<AppOnlineService>>()
                .LogError(exception, "App online action failed.");
            return Results.Text("Your app could not be reached. Please try again.", statusCode: 503);
        }
    }

    private static async Task ProxyAsync(string id, string? path, HttpContext context, AppLibrary library, IHttpClientFactory clients)
    {
        context.Response.Headers.ContentSecurityPolicy = SandboxPolicy;
        context.Response.Headers.AccessControlAllowOrigin = "*";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "no-store";
        string publicPath = string.IsNullOrEmpty(path) || path.EndsWith('/') ? path + "index.html" : path;
        if (!StaticAppManifest.ValidId(id) || library.Find(id) is not { } services)
        {
            context.Response.StatusCode = 404;
            return;
        }
        bool aspire = services.GetRequiredService<WorkspaceArtifacts>().IsAspireApp();
        bool readOnly = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (aspire ? !StaticAppManifest.AppPath(publicPath) : !readOnly || !StaticAppManifest.PublicPath(publicPath))
        {
            context.Response.StatusCode = 404;
            return;
        }
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            // Online apps run in an opaque sandboxed origin, so their same-path API calls need a preflight answer.
            context.Response.Headers.AccessControlAllowMethods = "GET, HEAD, POST, PUT, PATCH, DELETE";
            context.Response.Headers.AccessControlAllowHeaders = "content-type";
            context.Response.Headers.AccessControlMaxAge = "600";
            context.Response.StatusCode = 204;
            return;
        }
        if (context.Request.ContentLength > MaxRequestBytes)
        {
            context.Response.StatusCode = 413;
            return;
        }

        try
        {
            Uri? destination = await services.GetRequiredService<AppOnlineService>().GetDestinationAsync(context.RequestAborted);
            if (destination is null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("This app is not online yet.");
                return;
            }
            // Build our own request: no cookies, credentials, forwarded headers, or user-controlled authority.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            string query = aspire ? context.Request.QueryString.Value ?? "" : "";
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method),
                new Uri(destination, string.Join('/', publicPath.Split('/').Select(Uri.EscapeDataString)) + query));
            if (!readOnly)
            {
                // Forward only a bounded body and its content type; never cookies, auth or forwarded headers.
                using var body = new MemoryStream();
                byte[] incoming = new byte[8192];
                int read;
                while ((read = await context.Request.Body.ReadAsync(incoming, timeout.Token)) > 0)
                {
                    if (body.Length + read > MaxRequestBytes)
                    {
                        context.Response.StatusCode = 413;
                        return;
                    }
                    body.Write(incoming, 0, read);
                }
                request.Content = new ByteArrayContent(body.ToArray());
                if (context.Request.ContentType is { Length: > 0 and <= 200 } contentType &&
                    System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
                    request.Content.Headers.ContentType = mediaType;
            }
            using HttpResponseMessage response = await clients.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            int status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode && !(aspire && status is >= 400 and < 500))
            {
                context.Response.StatusCode = response.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 : 502;
                return;
            }
            // Aspire app APIs report their own validation and not-found results; server errors stay opaque.
            context.Response.StatusCode = status;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            if (HttpMethods.IsHead(context.Request.Method)) return;
            await using Stream source = await response.Content.ReadAsStreamAsync(timeout.Token);
            // Buffer a single bounded file before responding; never expose upstream redirects or error bodies.
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + count > StaticAppManifest.MaxFileBytes)
                    throw new IOException("The online app returned an oversized file.");
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (Exception exception) when (exception is HttpOperationException or HttpRequestException or IOException or InvalidOperationException or
            System.Text.Json.JsonException or KubernetesClientException or k8s.KubernetesException ||
            exception is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            context.RequestServices.GetRequiredService<ILogger<AppOnlineService>>()
                .LogError(exception, "Online app {AppId} could not be opened.", id);
            context.Response.StatusCode = 503;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Your app is not ready to open. Please try again.");
        }
    }
}
