using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;

namespace SquadOnAks.Web.Workspace;

public sealed class DisposableFixtureWorkspace
{
    public const string FixtureName = "route-planner";
    public const string StarterTemplateName = "starter";
    public const string InitialState = "Needs cast";
    public const string FixtureTestCommand = "sh tests/run.sh";
    private const string WorkspaceMarkerName = "soaks-workspace.json";

    private sealed record WorkspaceMarker(string Template, string Generation, ImportedRepository? Imported = null);

    // The Squad harness the coordinator needs; an imported repository without its own gets these, untracked.
    private static readonly string[] HarnessDirectories = [".github/agents", ".github/skills/coordinator-init-mode"];

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

    public string Generation { get; private set; } = Guid.NewGuid().ToString("N");

    /// <summary>The GitHub repository this app was started from, or null for an app built from a description.</summary>
    public ImportedRepository? Imported { get; private set; }

    /// <summary>An imported repository that doesn't (yet) declare the Squad deploy contract is worked on as it is.</summary>
    public bool IsPlainRepository =>
        Imported is not null && !File.Exists(Path.Combine(RootPath, AppBuildManifest.FileName));

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

        Dictionary<string, string> sourceSnapshot = TemplateSnapshot();

        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        Directory.CreateDirectory(RootPath);
        CopySource();
        InitializeRepository();
        CommitBaseline();
        Generation = Guid.NewGuid().ToString("N");
        File.WriteAllText(
            StatePath(WorkspaceMarkerName),
            JsonSerializer.Serialize(new WorkspaceMarker(Template, Generation)));

        if (Directory.Exists(Path.Combine(RootPath, ".squad")))
        {
            throw new InvalidOperationException(
                "The packaged fixture must not contain pre-generated Squad state.");
        }

        Dictionary<string, string> sourceAfterCopy = TemplateSnapshot();
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

    /// <summary>
    /// Starts the workspace from an existing GitHub repository instead of the template. The token is passed to git
    /// only through the environment, never stored. A Squad team already in the repository is kept as it is.
    /// </summary>
    public async Task ImportAsync(ImportedRepository repository, string token, CancellationToken cancellationToken)
    {
        if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(RootPath)!);
        string basic = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"x-access-token:{token}"));
        (int exitCode, string output) = await GitAsync(Path.GetDirectoryName(RootPath)!, basic, TimeSpan.FromMinutes(5), cancellationToken,
            "clone", "--quiet", "--single-branch", "--branch", repository.DefaultBranch, repository.CloneUrl, RootPath);
        if (exitCode != 0)
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
            throw new InvalidOperationException(output.Contains("Repository not found", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("could not read", StringComparison.OrdinalIgnoreCase)
                ? "The repository couldn't be read with the configured GitHub token."
                : "The repository couldn't be copied from GitHub.");
        }

        string exclude = Path.Combine(RootPath, ".git", "info", "exclude");
        Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);
        var excluded = new List<string>(WorkspacePathPolicy.SquadRuntimeExcludePatterns);
        foreach (string directory in HarnessDirectories)
        {
            string source = Path.Combine(sourcePath, directory);
            if (!Directory.Exists(source)) continue;
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
                string destination = PathPolicy.ResolveWorkspacePath(relative);
                if (File.Exists(destination)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
                excluded.Add("/" + relative);
            }
        }
        File.AppendAllLines(exclude, excluded);
        RunGit("config", "user.name", "Squad on AKS");
        RunGit("config", "user.email", "squad@squad-on-aks.invalid");
        string head = (await GitAsync(RootPath, null, TimeSpan.FromSeconds(30), cancellationToken, "rev-parse", "HEAD")).Output.Trim();
        Imported = repository with { BaseCommit = head };
        Generation = Guid.NewGuid().ToString("N");
        File.WriteAllText(StatePath(WorkspaceMarkerName), JsonSerializer.Serialize(new WorkspaceMarker(Template, Generation, Imported)));
        logger.LogInformation("Imported {Repository} into workspace {WorkspacePath}.", repository.FullName, RootPath);
    }

    /// <summary>Runs git with an optional GitHub credential supplied through the environment only.</summary>
    internal static async Task<(int ExitCode, string Output)> GitAsync(
        string workingDirectory, string? basicCredential, TimeSpan timeout, CancellationToken cancellationToken, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (basicCredential is not null)
        {
            startInfo.Environment["GIT_CONFIG_COUNT"] = "2";
            startInfo.Environment["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
            startInfo.Environment["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: basic {basicCredential}";
            startInfo.Environment["GIT_CONFIG_KEY_1"] = "credential.helper";
            startInfo.Environment["GIT_CONFIG_VALUE_1"] = "";
        }
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(bounded.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            throw;
        }
        return (process.ExitCode, ((await stdout) + (await stderr)).Trim());
    }

    /// <summary>
    /// Picks up a workspace this template created earlier, keeping the person's squad, waves, and app
    /// across restarts. Returns false when there is nothing to resume and the caller should reset.
    /// </summary>
    public bool TryResume()
    {
        try
        {
            string marker = StatePath(WorkspaceMarkerName);
            if (!File.Exists(marker))
            {
                return false;
            }

            WorkspaceMarker? saved = JsonSerializer.Deserialize<WorkspaceMarker>(File.ReadAllText(marker));
            if (saved is null || saved.Template != Template || string.IsNullOrWhiteSpace(saved.Generation))
            {
                return false;
            }

            ValidateRuntime();
            Generation = saved.Generation;
            Imported = saved.Imported;
            logger.LogInformation("Resumed workspace {WorkspacePath}.", RootPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not resume workspace {WorkspacePath}: {Failure}", RootPath, exception.Message);
            return false;
        }
    }

    /// <summary>Squad's own bookkeeping lives under .git so it never shows up as app content.</summary>
    public string StatePath(string name) => Path.Combine(RootPath, ".git", name);

    public void ValidateInitialized()
    {
        ValidateRuntime();

        Dictionary<string, string> expected = TemplateSnapshot();
        Dictionary<string, string> actual = Snapshot(RootPath);
        if (!expected.OrderBy(item => item.Key).SequenceEqual(actual.OrderBy(item => item.Key)))
        {
            throw new InvalidOperationException(
                "The initialized fixture workspace does not match the packaged source.");
        }
    }

    public void ValidateRuntime()
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

        foreach ((string sourceFile, string relativePath) in TemplateFiles())
        {
            string destination = PathPolicy.ResolveWorkspacePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceFile, destination, overwrite: false);
        }
    }

    private IEnumerable<(string File, string RelativePath)> TemplateFiles()
    {
        foreach (string file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            yield return (file, Path.GetRelativePath(sourcePath, file).Replace('\\', '/'));
        }
    }

    private Dictionary<string, string> TemplateSnapshot() =>
        TemplateFiles().ToDictionary(
            item => item.RelativePath,
            item => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.File))),
            StringComparer.Ordinal);

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

/// <summary>Where an imported app came from; BaseCommit is the commit it was copied at.</summary>
public sealed record ImportedRepository(string Owner, string Name, string FullName, string CloneUrl, string HtmlUrl,
    string DefaultBranch, string Branch, bool HadSquad, string? BaseCommit = null);

public sealed record FixtureWorkspaceDiagnostics(
    string State,
    string Fixture,
    string TestCommand,
    IReadOnlyList<string> ExpectedChangedFiles,
    IReadOnlyList<string> HarnessFiles,
    bool NativeCoordinatorReady,
    bool IsEphemeral);
