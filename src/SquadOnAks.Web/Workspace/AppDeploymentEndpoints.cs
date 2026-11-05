namespace SquadOnAks.Web.Workspace;

public static class AppDeploymentEndpoints
{
    public static void MapAppDeploymentEndpoints(this WebApplication app)
    {
        app.MapGet("/workspace/deployments", (AppDeploymentStateStore deployments) =>
        {
            AppDeploymentSnapshot snapshot = deployments.GetSnapshot();
            return Results.Ok(snapshot);
        });
    }
}
