namespace SquadOnAks.Web.Work;

public sealed class WorkLifecycleShutdownService(
    WorkLifecycle lifecycle,
    ILogger<WorkLifecycleShutdownService> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lifecycle.BeginShutdown();
        try
        {
            await lifecycle.WaitForIdleAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lifecycle.FailShutdown();
            logger.LogError("Active Squad work did not stop within the shutdown grace period.");
        }
    }
}
