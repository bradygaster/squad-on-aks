using Microsoft.Extensions.Diagnostics.HealthChecks;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Health;

public sealed class ProductionReadinessHealthCheck(
    DisposableFixtureWorkspace workspace,
    WorkLifecycle lifecycle,
    ProductionReadinessSettings settings,
    IConfiguration configuration)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            workspace.ValidateInitialized();
            if (settings.RequireProductionRuntime)
            {
                ValidateExecutable(configuration["COPILOT_CLI_PATH"] ?? "copilot");
                ValidateWritableDirectory("HOME");
                ValidateWritableDirectory("TMPDIR");
                ValidateWritableDirectory("XDG_CACHE_HOME");
                ValidateWritableDirectory("XDG_CONFIG_HOME");
            }

            return Task.FromResult(
                lifecycle.IsShuttingDown
                    ? HealthCheckResult.Unhealthy()
                    : HealthCheckResult.Healthy());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }

    private static void ValidateExecutable(string executable)
    {
        if (Path.IsPathFullyQualified(executable))
        {
            if (!IsExecutable(executable))
            {
                throw new InvalidOperationException("The Copilot runtime is unavailable.");
            }

            return;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path) ||
            !path.Split(Path.PathSeparator).Any(directory =>
                IsExecutable(Path.Combine(directory, executable))))
        {
            throw new InvalidOperationException("The Copilot runtime is unavailable.");
        }
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        return (mode & (UnixFileMode.UserExecute |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherExecute)) != 0;
    }

    private static void ValidateWritableDirectory(string environmentVariable)
    {
        string? path = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new InvalidOperationException(
                $"Runtime directory '{environmentVariable}' is unavailable.");
        }

        string probe = Path.Combine(
            path,
            $".write-probe-{Environment.ProcessId}-{Guid.NewGuid():N}");
        File.WriteAllText(probe, string.Empty);
        File.Delete(probe);
    }
}

public sealed record ProductionReadinessSettings(bool RequireProductionRuntime);
