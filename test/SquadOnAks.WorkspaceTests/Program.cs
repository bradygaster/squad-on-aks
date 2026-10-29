using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Squad.Agents.AI;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Workspace;

string repositoryRoot = FindRepositoryRoot();
string fixtureSource = Path.Combine(
    repositoryRoot,
    "src",
    "SquadOnAks.Web",
    "FixtureSource",
    DisposableFixtureWorkspace.FixtureName);
string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    "squad-on-aks-workspace-tests",
    Guid.NewGuid().ToString("N"));
string workspaceRoot = Path.Combine(temporaryRoot, "workspace");
var logger = new CapturingLogger();

OriginalIntentTests.Run(fixtureSource);
await ReviewRegressionTests.RunAsync(fixtureSource);
await PublisherTests.RunAsync(fixtureSource);

try
{
    Dictionary<string, string> sourceBefore = Snapshot(fixtureSource);
    var workspace = new DisposableFixtureWorkspace(
        fixtureSource,
        workspaceRoot,
        logger);

    workspace.Reset();
    workspace.ValidateInitialized();
    string firstBaseline = await ReadGitHeadAsync(workspaceRoot);

    EqualSnapshots(sourceBefore, Snapshot(workspaceRoot), "Startup copy must match fixture source.");
    EqualSnapshots(sourceBefore, Snapshot(fixtureSource), "Startup must not mutate fixture source.");

    FixtureWorkspaceDiagnostics diagnostics = workspace.GetDiagnostics();
    Require(diagnostics.State == DisposableFixtureWorkspace.InitialState, "Initial state must be Needs cast.");
    Require(diagnostics.HarnessFiles.Count == 0, "Initial workspace must contain no generated team files.");
    Require(!Directory.Exists(Path.Combine(workspaceRoot, ".squad")), "Initial workspace must not contain .squad.");
    Require(Directory.Exists(Path.Combine(workspaceRoot, ".git")), "Workspace must be an isolated Git repository.");
    Require(diagnostics.NativeCoordinatorReady, "Official native Squad bootstrap files must be present.");
    Require(
        new SquadAgentOptions().AgentFileName == "squad",
        "Squad.Agents.AI must retain native coordinator auto-selection.");

    ProcessResult failingTest = await RunFixtureTestsAsync(workspaceRoot);
    Require(failingTest.ExitCode != 0, "Fixture tests must fail before remediation.");
    Require(failingTest.StandardError.Contains("FAIL route order", StringComparison.Ordinal), "Expected deterministic failure output.");

    string repairPath = workspace.PathPolicy.ResolveWorkspacePath("src/flight-plan.sh");
    await File.WriteAllTextAsync(
        repairPath,
        "#!/bin/sh\n\nprintf '%s\\n' LAUNCH ORBIT LAND\n");

    ProcessResult passingTest = await RunFixtureTestsAsync(workspaceRoot);
    Require(passingTest.ExitCode == 0, $"Fixture tests must pass after remediation: {passingTest.StandardError}");
    Require(passingTest.StandardOutput.Contains("PASS route order", StringComparison.Ordinal), "Expected deterministic passing output.");

    string[] changedFiles = ChangedPaths(sourceBefore, Snapshot(workspaceRoot));
    Equal(
        diagnostics.ExpectedChangedFiles.Order(StringComparer.Ordinal),
        changedFiles,
        "Remediation must change only the stable expected file set.");

    RequireThrows(
        () => workspace.PathPolicy.ResolveWorkspacePath("../outside.md"),
        "Parent traversal must be rejected.");
    RequireThrows(
        () => workspace.PathPolicy.ResolveWorkspacePath(Path.GetFullPath(Path.Combine(temporaryRoot, "outside.md"))),
        "Absolute paths must be rejected.");
    RequireThrows(
        () => workspace.PathPolicy.ResolveHarnessPath(".squad/secrets.txt"),
        "Non-allow-listed harness files must be rejected.");

    string outsideDirectory = Path.Combine(temporaryRoot, "outside");
    Directory.CreateDirectory(outsideDirectory);
    Directory.CreateSymbolicLink(
        Path.Combine(workspaceRoot, "linked-outside"),
        outsideDirectory);
    RequireThrows(
        () => workspace.PathPolicy.ResolveWorkspacePath("linked-outside/escape.md"),
        "Symbolic-link escapes must be rejected.");
    Require(
        logger.Events.Count(item => item.EventId == WorkspacePathPolicy.RejectedPathEvent.Id) >= 4,
        "Rejected escape attempts must be logged.");

    workspace.PathPolicy.ResolveHarnessPath(".squad/team.md");
    workspace.PathPolicy.ResolveHarnessPath(".squad/agents/flight/charter.md");
    workspace.PathPolicy.ResolveHarnessPath(".squad/agents/flight/history.md");
    workspace.PathPolicy.ResolveHarnessPath(".squad/casting/session.json");
    workspace.PathPolicy.ResolveHarnessPath(".github/agents/squad.agent.md");
    workspace.PathPolicy.ResolveHarnessPath(
        ".github/skills/coordinator-init-mode/references/casting-reference.md");

    await CreateNativeCastAsync(workspaceRoot);
    var review = new CastReviewService(workspace);
    Require(review.GetStatus().State == "Cast review", "Generated native files must enter Cast review.");
    string[] reviewPaths = review.GetReviewFiles().Select(file => file.Path).ToArray();
    Equal(
        new[]
        {
            ".squad/team.md",
            ".squad/routing.md",
            ".squad/agents/flight/charter.md",
            ".squad/agents/retro/charter.md"
        },
        reviewPaths,
        "Review must expose only team, routing, and active charters.");
    Require(
        !review.Update("../outside.md", "escape").Success,
        "Cast review must reject traversal.");
    Require(
        !review.Update(".squad/agents/flight/history.md", "not editable").Success,
        "Cast review must reject native files outside the edit allowlist.");
    Require(
        !review.Update(".squad/agents/inactive/charter.md", "not active").Success,
        "Cast review must reject non-active charters.");

    string linkedCharter = Path.Combine(workspaceRoot, ".squad", "agents", "linked");
    Directory.CreateSymbolicLink(linkedCharter, outsideDirectory);
    string linkedTeam = await File.ReadAllTextAsync(Path.Combine(workspaceRoot, ".squad", "team.md"));
    await File.WriteAllTextAsync(
        Path.Combine(workspaceRoot, ".squad", "team.md"),
        linkedTeam + "\n| Linked | Escape | .squad/agents/linked/charter.md |\n");
    Require(
        review.Validate().Any(error => error.Contains("invalid charter path", StringComparison.Ordinal)),
        "Cast review validation must reject symbolic-link escapes.");
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, ".squad", "team.md"), linkedTeam);

    string routingPath = Path.Combine(workspaceRoot, ".squad", "routing.md");
    string validRouting = await File.ReadAllTextAsync(routingPath);
    Require(review.Update(".squad/routing.md", "").Success, "Allowed routing edits must be saved.");
    CastActivationResult malformed = review.Activate();
    Require(!malformed.Success, "Malformed native files must block activation.");
    Require(
        malformed.Errors.Contains("routing.md is missing or empty."),
        "Malformed native files must return an actionable validation error.");
    Require(review.Update(".squad/routing.md", validRouting).Success, "Valid routing must be restorable.");
    CastActivationResult activated = review.Activate();
    Require(activated.Success && activated.State == "Ready", "Valid native files must activate.");
    Require(
        !review.Update(".squad/routing.md", "accidental recast").Success,
        "Active team files must be read-only.");
    Require(
        review.GetStatus().CanTakeRequests,
        "Rejected review edits must not deactivate the Squad.");
    Require(review.GetStatus().CanTakeRequests, "Work controls may unlock only after activation.");

    var restartedWorkspace = new DisposableFixtureWorkspace(
        fixtureSource,
        workspaceRoot,
        logger);
    restartedWorkspace.Reset();
    restartedWorkspace.ValidateInitialized();
    string secondBaseline = await ReadGitHeadAsync(workspaceRoot);

    EqualSnapshots(sourceBefore, Snapshot(workspaceRoot), "Restart must restore a clean fixture.");
    Require(firstBaseline == secondBaseline, "Fixture initialization must create a deterministic baseline commit.");
    Require(
        restartedWorkspace.GetDiagnostics().State == DisposableFixtureWorkspace.InitialState,
        "Restart must return to Needs cast.");
    Require(
        restartedWorkspace.GetDiagnostics().HarnessFiles.Count == 0,
        "Restart must remove generated team files.");
    Require(
        restartedWorkspace.GetDiagnostics().NativeCoordinatorReady,
        "Restart must restore the native coordinator bootstrap.");
    Require(
        (await RunFixtureTestsAsync(workspaceRoot)).ExitCode != 0,
        "Restarted fixture must restore the known failing test.");
    EqualSnapshots(sourceBefore, Snapshot(fixtureSource), "Restart must not mutate fixture source.");

    string runtimeTranscript = workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state/s1/events.jsonl");
    Directory.CreateDirectory(Path.GetDirectoryName(runtimeTranscript)!);
    await File.WriteAllTextAsync(runtimeTranscript, "{}\n");
    await File.WriteAllTextAsync(workspace.PathPolicy.ResolveWorkspacePath(".squad/session-store.db-wal"), "wal");
    await File.WriteAllTextAsync(workspace.PathPolicy.ResolveWorkspacePath(".squad/decisions.md"), "# Decisions\n");
    ProcessStartInfo statusInfo = new("git")
    {
        WorkingDirectory = workspaceRoot,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };
    foreach (string argument in new[] { "status", "--porcelain", "--untracked-files=all" })
    {
        statusInfo.ArgumentList.Add(argument);
    }

    using (Process status = Process.Start(statusInfo)!)
    {
        string porcelain = await status.StandardOutput.ReadToEndAsync();
        await status.WaitForExitAsync();
        Require(!porcelain.Contains("session-state", StringComparison.Ordinal) &&
            !porcelain.Contains("session-store", StringComparison.Ordinal),
            $"Squad runtime state must be git-excluded: {porcelain}");
        Require(porcelain.Contains(".squad/decisions.md", StringComparison.Ordinal),
            "Squad team files must remain tracked.");
    }

    string[] artifactPaths = new WorkspaceArtifacts(restartedWorkspace).ListFiles().Select(file => file.Path).ToArray();
    Require(artifactPaths.Contains(".squad/decisions.md"), "Artifacts must list Squad team files.");
    Require(!artifactPaths.Any(WorkspacePathPolicy.IsSquadRuntimePath), "Artifacts must hide Squad runtime state.");

    Console.WriteLine("PASS disposable fixture workspace");
    Console.WriteLine("PASS native uncast state");
    Console.WriteLine("PASS deterministic failing and passing fixture tests");
    Console.WriteLine("PASS containment rejection and logging");
    Console.WriteLine("PASS native cast review allowlist, validation, and activation");
    Console.WriteLine("PASS clean restart reset");
    Console.WriteLine("PASS squad runtime state excluded from git and artifacts");
    return 0;
}
finally
{
    if (Directory.Exists(temporaryRoot))
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
}

static async Task<ProcessResult> RunFixtureTestsAsync(string workspaceRoot)
{
    ProcessStartInfo startInfo = new("sh")
    {
        WorkingDirectory = workspaceRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("tests/run.sh");

    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start fixture tests.");
    string standardOutput = await process.StandardOutput.ReadToEndAsync();
    string standardError = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new(process.ExitCode, standardOutput, standardError);
}

static async Task<string> ReadGitHeadAsync(string workspaceRoot)
{
    ProcessStartInfo startInfo = new("git")
    {
        WorkingDirectory = workspaceRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("rev-parse");
    startInfo.ArgumentList.Add("HEAD");

    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not inspect fixture HEAD.");
    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    Require(process.ExitCode == 0, $"Could not inspect fixture HEAD: {error}");
    return output.Trim();
}

static async Task CreateNativeCastAsync(string workspaceRoot)
{
    string agentsRoot = Path.Combine(workspaceRoot, ".squad", "agents");
    Directory.CreateDirectory(Path.Combine(agentsRoot, "flight"));
    Directory.CreateDirectory(Path.Combine(agentsRoot, "retro"));
    Directory.CreateDirectory(Path.Combine(workspaceRoot, ".squad", "casting"));

    await File.WriteAllTextAsync(
        Path.Combine(workspaceRoot, ".squad", "team.md"),
        """
        # Squad

        ## Members

        | Name | Role | Charter |
        | --- | --- | --- |
        | FLIGHT | Lead | .squad/agents/flight/charter.md |
        | RETRO | Reviewer | .squad/agents/retro/charter.md |
        """);
    await File.WriteAllTextAsync(
        Path.Combine(workspaceRoot, ".squad", "routing.md"),
        "# Routing\n\nImplementation: FLIGHT\nReview: RETRO\n");
    await File.WriteAllTextAsync(
        Path.Combine(agentsRoot, "flight", "charter.md"),
        "# FLIGHT\n\nLead the route planner.\n");
    await File.WriteAllTextAsync(
        Path.Combine(agentsRoot, "flight", "history.md"),
        "# FLIGHT history\n");
    await File.WriteAllTextAsync(
        Path.Combine(agentsRoot, "retro", "charter.md"),
        "# RETRO\n\nReview route planner changes.\n");
    await File.WriteAllTextAsync(
        Path.Combine(agentsRoot, "retro", "history.md"),
        "# RETRO history\n");
    await File.WriteAllTextAsync(
        Path.Combine(workspaceRoot, ".squad", "casting", "registry.json"),
        """{"agents":[{"persistent_name":"FLIGHT","status":"active"}]}""");
}

static Dictionary<string, string> Snapshot(string path) =>
    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        .Where(file => !Path.GetRelativePath(path, file)
            .Replace('\\', '/')
            .StartsWith(".git/", StringComparison.Ordinal))
        .ToDictionary(
            file => Path.GetRelativePath(path, file).Replace('\\', '/'),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
            StringComparer.Ordinal);

static string[] ChangedPaths(
    IReadOnlyDictionary<string, string> before,
    IReadOnlyDictionary<string, string> after) =>
    before.Keys
        .Union(after.Keys, StringComparer.Ordinal)
        .Where(path =>
            !before.TryGetValue(path, out string? oldHash) ||
            !after.TryGetValue(path, out string? newHash) ||
            oldHash != newHash)
        .Order(StringComparer.Ordinal)
        .ToArray();

static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{message} Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}

static void EqualSnapshots(
    IReadOnlyDictionary<string, string> expected,
    IReadOnlyDictionary<string, string> actual,
    string message) =>
    Equal(
        expected.OrderBy(item => item.Key, StringComparer.Ordinal),
        actual.OrderBy(item => item.Key, StringComparer.Ordinal),
        message);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void RequireThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "SquadOnAks.slnx")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate SquadOnAks.slnx.");
}

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed class CapturingLogger : ILogger<DisposableFixtureWorkspace>, ILogger
{
    public List<(int EventId, LogLevel Level, string Message)> Events { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Events.Add((eventId.Id, logLevel, formatter(state, exception)));
}
