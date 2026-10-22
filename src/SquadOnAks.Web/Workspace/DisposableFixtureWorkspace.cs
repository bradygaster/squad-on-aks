using System.Security.Cryptography;
using System.Diagnostics;

namespace SquadOnAks.Web.Workspace;

public sealed class DisposableFixtureWorkspace
{
    public const string FixtureName = "route-planner";
    public const string InitialState = "Needs cast";
    public const string FixtureTestCommand = "sh tests/run.sh";

    private static readonly string[] expectedChangedFiles = ["src/flight-plan.sh"];
    private readonly string sourcePath;
    private readonly ILogger<DisposableFixtureWorkspace> logger;
    private Dictionary<string, string> baselineFiles = new(StringComparer.Ordinal);

    public DisposableFixtureWorkspace(
        string sourcePath,
        string rootPath,
        ILogger<DisposableFixtureWorkspace> logger)
    {
        this.sourcePath = Path.GetFullPath(sourcePath);
        RootPath = Path.GetFullPath(rootPath);
        this.logger = logger;
        PathPolicy = new WorkspacePathPolicy(
            RootPath,
            logger);

        ValidateSeparatedPaths();
    }

    public string RootPath { get; }

    public WorkspacePathPolicy PathPolicy { get; }

    public void Reset()
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(
                $"Packaged fixture source is missing: {sourcePath}");
        }

        Dictionary<string, string> sourceSnapshot = Snapshot(sourcePath);
        baselineFiles = ReadFiles(sourcePath);

        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        Directory.CreateDirectory(RootPath);
        CopySource();
        InitializeRepository();

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
            FixtureName,
            FixtureTestCommand,
            expectedChangedFiles,
            harnessFiles,
            nativeCoordinatorReady,
            IsEphemeral: true);
    }

    public IReadOnlyList<WorkspaceFileChange> GetChanges()
    {
        var changes = new List<WorkspaceFileChange>();
        Dictionary<string, string> currentFiles = ReadFiles(RootPath);
        foreach (string path in baselineFiles.Keys
            .Union(currentFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            baselineFiles.TryGetValue(path, out string? before);
            currentFiles.TryGetValue(path, out string? after);
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new(
                path,
                WorkspacePathPolicy.IsGeneratedTeamPath(path),
                BuildFocusedDiff(path, before, after)));
        }

        return changes;
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

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not initialize the disposable Git repository.");
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not initialize the disposable Git repository: {standardError}");
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

    private static Dictionary<string, string> ReadFiles(string path) =>
        Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Select(file => (
                Path: Path.GetRelativePath(path, file).Replace('\\', '/'),
                FullPath: file))
            .Where(item => !item.Path.StartsWith(".git/", StringComparison.Ordinal))
            .ToDictionary(
                item => item.Path,
                item => File.ReadAllText(item.FullPath),
                StringComparer.Ordinal);

    private static string BuildFocusedDiff(string path, string? before, string? after)
    {
        string[] beforeLines = SplitLines(before);
        string[] afterLines = SplitLines(after);
        int prefix = 0;
        while (prefix < beforeLines.Length &&
               prefix < afterLines.Length &&
               beforeLines[prefix] == afterLines[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < beforeLines.Length - prefix &&
               suffix < afterLines.Length - prefix &&
               beforeLines[^(suffix + 1)] == afterLines[^(suffix + 1)])
        {
            suffix++;
        }

        IEnumerable<string> removed = beforeLines
            .Skip(prefix)
            .Take(beforeLines.Length - prefix - suffix)
            .Select(line => $"-{line}");
        IEnumerable<string> added = afterLines
            .Skip(prefix)
            .Take(afterLines.Length - prefix - suffix)
            .Select(line => $"+{line}");

        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"--- a/{path}",
                $"+++ b/{path}",
                $"@@ -{prefix + 1},{beforeLines.Length - prefix - suffix} +{prefix + 1},{afterLines.Length - prefix - suffix} @@"
            }.Concat(removed).Concat(added));
    }

    private static string[] SplitLines(string? content) =>
        content is null
            ? []
            : content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}

public sealed record FixtureWorkspaceDiagnostics(
    string State,
    string Fixture,
    string TestCommand,
    IReadOnlyList<string> ExpectedChangedFiles,
    IReadOnlyList<string> HarnessFiles,
    bool NativeCoordinatorReady,
    bool IsEphemeral);

public sealed record WorkspaceFileChange(
    string Path,
    bool IsHarness,
    string Diff);
