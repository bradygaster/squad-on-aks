using System.Net;
using System.Net.Http.Headers;
using k8s;
using k8s.Autorest;

namespace SquadOnAks.NamespaceProvisioner;

public static class AppNamespaceProvisionerEndpoints
{
    public static IEndpointRouteBuilder MapAppNamespaceProvisionerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/apps/{appId}", (string appId, HttpContext context, IAppNamespaceProvisioner provisioner) =>
            HandleAsync(context, "provisioning", appId, token => provisioner.EnsureAsync(appId, token)));
        endpoints.MapDelete("/apps/{appId}", (string appId, HttpContext context, IAppNamespaceProvisioner provisioner) =>
            HandleAsync(context, "deletion", appId, token => provisioner.DeleteAsync(appId, token)));
        // A throwaway namespace that tests one revision before it replaces the running one.
        endpoints.MapPost("/apps/{appId}/checks/{runId}", (string appId, string runId, HttpContext context, IAppNamespaceProvisioner provisioner) =>
            HandleAsync(context, "check provisioning", appId, token => provisioner.EnsureCheckAsync(appId, runId, token)));
        endpoints.MapDelete("/apps/{appId}/checks/{runId}", (string appId, string runId, HttpContext context, IAppNamespaceProvisioner provisioner) =>
            HandleAsync(context, "check deletion", appId, token => provisioner.DeleteCheckAsync(appId, runId, token)));
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context, string action, string appId, Func<CancellationToken, Task<string>> operation)
    {
        IServiceAccountTokenValidator tokenValidator = context.RequestServices.GetRequiredService<IServiceAccountTokenValidator>();
        ILogger logger = context.RequestServices.GetRequiredService<ILogger<AppNamespaceProvisioner>>();
        string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) ||
            !await tokenValidator.IsAppManagerAsync(header.Parameter, context.RequestAborted))
        {
            return Results.Unauthorized();
        }

        try
        {
            string appNamespace = await operation(context.RequestAborted);
            return Results.Ok(new { appNamespace });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "App namespace {Action} was rejected.", action);
            return Results.Conflict("The requested app namespace is not owned by Squad on AKS.");
        }
        catch (Exception exception) when (exception is HttpRequestException or KubernetesException or
            HttpOperationException or IOException)
        {
            logger.LogError(exception, "App namespace {Action} failed for {AppId}.", action, appId);
            return Results.Problem(
                $"App namespace {action} failed.",
                statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }
}
