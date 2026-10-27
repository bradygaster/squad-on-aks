using System.Security.Cryptography;
using System.Diagnostics;

namespace SquadOnAks.Web.Workspace;

public sealed class DisposableFixtureWorkspace
{
    public const string FixtureName = "route-planner";
    public const string StarterTemplateName = "starter";
    public const string InitialState = "Needs cast";
    public const string FixtureTestCommand = "sh tests/run.sh";

    private static readonly string[] routePlannerExpectedChangedFiles = ["src/flight-plan.sh"];
    private readonly string sourcePath;
    private readonly ILogger<DisposableFixtureWorkspace> logger;

    public DisposableFixtureWorkspace(
        string sourcePath,
        string rootPath,
        ILogger<DisposableFixtureWorkspace> logger)
    {
        this.sourcePath = Path.GetFullPath(sourcePath);
        Template = Path.GetFileName(this.sourcePath.TrimEnd(Path.DirectorySeparatorChar));
        RootPath = Path.GetFullPath(rootPath);
        this.logger = logger;
        PathPolicy = new WorkspacePathPolicy(
            RootPath,
            logger);

        ValidateSeparatedPaths();
    }

    public string RootPath { get; }

    public string Template { get; }

    public static string ResolveTemplateName(string? configured)
    {
        string name = string.IsNullOrWhiteSpace(configured) ? FixtureName : configured.Trim();
        if (name is not (FixtureName or StarterTemplateName))
        {
            throw new InvalidOperationException(
                $"Unknown workspace template '{name}'. Use '{FixtureName}' or '{StarterTemplateName}'.");
        }

        return name;
    }

    public WorkspacePathPolicy PathPolicy { get; }

    public void Reset()
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(
                $"Packaged fixture source is missing: {sourcePath}");
        }

        Dictionary<string, string> sourceSnapshot = Snapshot(sourcePath);

        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        Directory.CreateDirectory(RootPath);
        CopySource();
        InitializeRepository();
        CommitBaseline();

        if (Directory.Exists(Path.Combine(RootPath, ".squad")))
        {
            throw new InvalidOperationException(
                "The packaged fixture must not contain pre-generated Squad state.");
        }

        Dictionary<string, string> sourceAfterCopy = Snapshot(sourcePath);
        if (!sourceSnapshot.OrderBy(item => item.Key).SequenceEqual(sourceAfterCopy.OrderBy(item => item.Key)))
        {
            throw new InvalidOperationException(
                "Fixture source changed while creating the writable workspace.");
        }

        logger.LogInformation(
            "Created clean disposable fixture workspace {WorkspacePath} in {State} state.",
            RootPath,
            InitialState);
    }

    public void ValidateInitialized()
    {
        if (!Directory.Exists(RootPath))
        {
            throw new DirectoryNotFoundException(
                $"The fixture init container did not create the workspace: {RootPath}");
        }

        string gitDirectory = Path.Combine(RootPath, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            throw new InvalidOperationException(
                $"The fixture workspace is not an initialized Git repository: {RootPath}");
        }

        Dictionary<string, string> expected = Snapshot(sourcePath);
        Dictionary<string, string> actual = Snapshot(RootPath);
        if (!expected.OrderBy(item => item.Key).SequenceEqual(actual.OrderBy(item => item.Key)))
        {
            throw new InvalidOperationException(
                "The initialized fixture workspace does not match the packaged source.");
        }
    }

    public FixtureWorkspaceDiagnostics GetDiagnostics()
    {
        bool nativeCoordinatorReady =
            File.Exists(Path.Combine(RootPath, ".github", "agents", "squad.agent.md")) &&
            File.Exists(Path.Combine(
                RootPath,
                ".github",
                "skills",
                "coordinator-init-mode",
                "SKILL.md"));
        string[] harnessFiles = Directory.Exists(RootPath)
            ? Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(RootPath, path).Replace('\\', '/'))
                .Where(WorkspacePathPolicy.IsGeneratedTeamPath)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

        return new(
            InitialState,
            Template,
            FixtureTestCommand,
            Template == FixtureName ? routePlannerExpectedChangedFiles : [],
            harnessFiles,
            nativeCoordinatorReady,
            IsEphemeral: true);
    }

    private void CopySource()
    {
        foreach (string sourceDirectory in Directory.EnumerateDirectories(
            sourcePath,
            "*",
            SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, sourceDirectory);
            string destination = PathPolicy.ResolveWorkspacePath(relativePath);
            Directory.CreateDirectory(destination);
        }

        foreach (string sourceFile in Directory.EnumerateFiles(
            sourcePath,
            "*",
            SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, sourceFile);
            string destination = PathPolicy.ResolveWorkspacePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceFile, destination, overwrite: false);
        }
    }

    private void InitializeRepository()
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("init");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--initial-branch=main");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not initialize the disposable Git repository.");
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not initialize the disposable Git repository: {standardError}");
        }

        string exclude = Path.Combine(RootPath, ".git", "info", "exclude");
        Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);
        File.AppendAllLines(exclude, WorkspacePathPolicy.SquadRuntimeExcludePatterns);
    }

    private void CommitBaseline()
    {
        RunGit("config", "user.name", "Squad fixture");
        RunGit("config", "user.email", "squad-fixture@example.invalid");
        RunGit("add", ".");
        RunGit(
            new Dictionary<string, string>
            {
                ["GIT_AUTHOR_DATE"] = "2026-01-01T00:00:00Z",
                ["GIT_COMMITTER_DATE"] = "2026-01-01T00:00:00Z"
            },
            "commit",
            "--quiet",
            "-m",
            "fixture baseline");
    }

    private void RunGit(params string[] arguments) =>
        RunGit(new Dictionary<string, string>(), arguments);

    private void RunGit(
        IReadOnlyDictionary<string, string> environment,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach ((string name, string value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git.");
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not create the disposable repository baseline: {standardError}");
        }
    }

    private void ValidateSeparatedPaths()
    {
        string sourcePrefix = sourcePath + Path.DirectorySeparatorChar;
        string workspacePrefix = RootPath + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(sourcePath, RootPath, comparison) ||
            sourcePath.StartsWith(workspacePrefix, comparison) ||
            RootPath.StartsWith(sourcePrefix, comparison))
        {
            throw new InvalidOperationException(
                "Fixture source and writable workspace must be separate directories.");
        }

        string? root = Path.GetPathRoot(RootPath);
        if (string.Equals(root, RootPath, comparison))
        {
            throw new InvalidOperationException(
                "The filesystem root cannot be used as the disposable workspace.");
        }

        if (Directory.Exists(RootPath) &&
            File.GetAttributes(RootPath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException(
                "A symbolic link cannot be used as the disposable workspace root.");
        }
    }

    private static Dictionary<string, string> Snapshot(string path) =>
        Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(path, file)
                .Replace('\\', '/')
                .StartsWith(".git/", StringComparison.Ordinal))
            .ToDictionary(
                file => Path.GetRelativePath(path, file).Replace('\\', '/'),
                file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
                StringComparer.Ordinal);
}

public sealed record FixtureWorkspaceDiagnostics(
    string State,
    string Fixture,
    string TestCommand,
    IReadOnlyList<string> ExpectedChangedFiles,
    IReadOnlyList<string> HarnessFiles,
    bool NativeCoordinatorReady,
    bool IsEphemeral);
