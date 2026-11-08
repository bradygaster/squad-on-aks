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
        endpoints.MapPost("/apps/{appId}", async (
            string appId,
            HttpContext context,
            IServiceAccountTokenValidator tokenValidator,
            IAppNamespaceProvisioner provisioner,
            ILogger<AppNamespaceProvisioner> logger) =>
        {
            string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
            if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header) ||
                !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(header.Parameter) ||
                !await tokenValidator.IsDaydreamManagerAsync(header.Parameter, context.RequestAborted))
            {
                return Results.Unauthorized();
            }

            try
            {
                string appNamespace = await provisioner.EnsureAsync(appId, context.RequestAborted);
                return Results.Ok(new { appNamespace });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "App namespace provisioning was rejected.");
                return Results.Conflict("The requested app namespace is not owned by Daydream.");
            }
            catch (Exception exception) when (exception is HttpRequestException or KubernetesException or
                HttpOperationException or IOException)
            {
                logger.LogError(exception, "Could not provision app namespace for {AppId}.", appId);
                return Results.Problem(
                    "App namespace provisioning failed.",
                    statusCode: (int)HttpStatusCode.ServiceUnavailable);
            }
        });

        return endpoints;
    }
}
