using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public static class AppDeploymentEndpoints
{
    public static void MapAppDeploymentEndpoints(this WebApplication app)
    {
        app.MapGet("/workspace/topology", (AppTopologyService topology, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(topology.Get());
        });
        app.MapGet("/workspace/topology/live", async (AppTopologyService topology, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await topology.GetLiveAsync(context.RequestAborted));
        });
        app.MapGet("/workspace/deployments", (AppDeploymentStateStore deployments) =>
        {
            AppDeploymentSnapshot snapshot = deployments.GetSnapshot();
            return Results.Ok(snapshot);
        });
        app.MapPost("/workspace/deployments", async (
            KubernetesAppDeploymentRunner runner,
            OnlineHostingSettings hosting,
            WorkLifecycle lifecycle,
            HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!hosting.Available)
            {
                return Results.Text("App deployments need Squad running in a Kubernetes cluster.", statusCode: 422);
            }

            string origin = context.Request.Headers.Origin.ToString();
            if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site" ||
                origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? source) ||
                    source.GetLeftPart(UriPartial.Authority) != $"{context.Request.Scheme}://{context.Request.Host}"))
            {
                return Results.Text("Open this action from Squad.", statusCode: 403);
            }

            WorkLifecycle.WorkLease? lease = lifecycle.TryBegin("publish");
            if (lease is null)
            {
                return Results.Text("Wait for this app to finish its current work.", statusCode: 409);
            }

            WorkTerminalResult outcome = WorkTerminalResult.Failed;
            try
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    lease.CancellationToken);
                AppDeploymentSnapshot snapshot = await runner.RunAsync(cancellation.Token);
                outcome = WorkTerminalResult.Completed;
                return Results.Ok(snapshot);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                outcome = WorkTerminalResult.Cancelled;
                throw;
            }
            catch (OperationCanceledException)
            {
                outcome = WorkTerminalResult.Cancelled;
                return Results.Text("App deployment was cancelled and can be resumed.", statusCode: 409);
            }
            catch (InvalidOperationException exception)
            {
                context.RequestServices.GetRequiredService<ILogger<KubernetesAppDeploymentRunner>>()
                    .LogWarning(exception, "App deployment was rejected.");
                return Results.Text(exception.Message, statusCode: 422);
            }
            catch (Exception exception)
            {
                context.RequestServices.GetRequiredService<ILogger<KubernetesAppDeploymentRunner>>()
                    .LogError(exception, "App deployment failed.");
                return Results.Text("App deployment failed. Review its deployment history and try again.", statusCode: 503);
            }
            finally
            {
                lease.Complete(outcome, outcome == WorkTerminalResult.Completed
                    ? "App deployment completed."
                    : "App deployment did not complete.");
            }
        });
    }
}
