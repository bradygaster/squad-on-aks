using Microsoft.Extensions.Logging.Abstractions;

namespace SquadOnAks.Web.Workspace;

public static class WorkspaceInitializer
{
    public const string InitializeArgument = "--initialize-workspace";

    public static void Initialize()
    {
        string workspacePath = RequireEnvironment(SquadOnAksApplication.WorkspacePathKey);
        string contentRoot = AppContext.BaseDirectory;
        string fixtureSourcePath = Path.Combine(
            contentRoot,
            "FixtureSource",
            DisposableFixtureWorkspace.FixtureName);

        CreateRuntimeDirectory("HOME");
        CreateRuntimeDirectory("TMPDIR");
        CreateRuntimeDirectory("XDG_CACHE_HOME");
        CreateRuntimeDirectory("XDG_CONFIG_HOME");

        var workspace = new DisposableFixtureWorkspace(
            fixtureSourcePath,
            workspacePath,
            NullLogger<DisposableFixtureWorkspace>.Instance);
        workspace.Reset();
        workspace.ValidateInitialized();
    }

    private static void CreateRuntimeDirectory(string environmentVariable)
    {
        string path = RequireEnvironment(environmentVariable);
        Directory.CreateDirectory(path);
    }

    private static string RequireEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{name}' is missing or empty.");
        }

        return Path.GetFullPath(value);
    }
}
