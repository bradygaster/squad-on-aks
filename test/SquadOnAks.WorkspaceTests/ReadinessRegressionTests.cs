using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Health;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class ReadinessRegressionTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        foreach (string template in new[] { "route-planner", "starter" })
        {
            string source = Path.Combine(Path.GetDirectoryName(fixtureSource)!, template);
            string root = Path.Combine(Path.GetTempPath(), $"readiness-tests-{Guid.NewGuid():N}");
            var workspace = new DisposableFixtureWorkspace(
                source, root, NullLogger<DisposableFixtureWorkspace>.Instance);
            var lifecycle = new WorkLifecycle();
            var check = new ProductionReadinessHealthCheck(
                workspace, lifecycle, new(false), new ConfigurationBuilder().Build());
            try
            {
                workspace.Reset();
                workspace.ValidateInitialized();
                await ExpectAsync(HealthStatus.Healthy, "fresh Needs cast workspace");

                Directory.CreateDirectory(Path.Combine(root, ".squad"));
                File.WriteAllText(Path.Combine(root, ".squad", "team.md"), "# Generated team\n");
                await ExpectAsync(HealthStatus.Healthy, "generated team state");

                string application = Path.Combine(root, "index.html");
                File.WriteAllText(application, "<h1>First wave</h1>");
                await ExpectAsync(HealthStatus.Healthy, "added application");
                File.AppendAllText(application, "<p>Second wave</p>");
                await ExpectAsync(HealthStatus.Healthy, "edited application");
                File.Delete(application);
                File.Delete(Path.Combine(root, "README.md"));
                await ExpectAsync(HealthStatus.Healthy, "deleted application content");

                bool rejected = false;
                try
                {
                    workspace.ValidateInitialized();
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                Require(rejected, "Strict initialization must still reject a changed template.");

                string git = Path.Combine(root, ".git");
                string savedGit = Path.Combine(root, "saved-git");
                Directory.Move(git, savedGit);
                await ExpectAsync(HealthStatus.Unhealthy, "missing repository");
                File.WriteAllText(git, "not a repository directory");
                await ExpectAsync(HealthStatus.Unhealthy, "invalid repository path");
                File.Delete(git);
                Directory.Move(savedGit, git);
                await ExpectAsync(HealthStatus.Healthy, "restored repository");

                var runtimeCheck = new ProductionReadinessHealthCheck(
                    workspace, lifecycle, new(true),
                    new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["COPILOT_CLI_PATH"] = Path.Combine(root, "missing-copilot")
                        }).Build());
                Require((await runtimeCheck.CheckHealthAsync(new())).Status == HealthStatus.Unhealthy,
                    "Missing production runtime must remain unhealthy.");

                lifecycle.BeginShutdown();
                await ExpectAsync(HealthStatus.Unhealthy, "shutdown");
                workspace.Reset();
                workspace.ValidateInitialized();
                Directory.Delete(root, recursive: true);
                await ExpectAsync(HealthStatus.Unhealthy, "missing workspace");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            async Task ExpectAsync(HealthStatus status, string scenario)
            {
                HealthCheckResult result = await check.CheckHealthAsync(new());
                Require(result.Status == status, $"{template}: {scenario} must be {status}, got {result.Status}.");
            }
        }

        Console.WriteLine("PASS readiness accepts mutable workspaces while preserving initialization and runtime checks");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
