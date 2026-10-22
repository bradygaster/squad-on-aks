using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Squad.Agents.AI;
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

try
{
    Dictionary<string, string> sourceBefore = Snapshot(fixtureSource);
    var workspace = new DisposableFixtureWorkspace(
        fixtureSource,
        workspaceRoot,
        logger);

    workspace.Reset();

    EqualSnapshots(sourceBefore, Snapshot(workspaceRoot), "Startup copy must match fixture source.");
    EqualSnapshots(sourceBefore, Snapshot(fixtureSource), "Startup must not mutate fixture source.");

    FixtureWorkspaceDiagnostics diagnostics = workspace.GetDiagnostics();
    Require(diagnostics.State == DisposableFixtureWorkspace.InitialState, "Initial state must be Needs cast.");
    Require(diagnostics.HarnessFiles.Count == 0, "Initial workspace must contain no generated team files.");
    Require(!Directory.Exists(Path.Combine(workspaceRoot, ".squad")), "Initial workspace must not contain .squad.");
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

    Directory.CreateDirectory(Path.Combine(workspaceRoot, ".squad"));
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, ".squad", "team.md"), "generated");

    var restartedWorkspace = new DisposableFixtureWorkspace(
        fixtureSource,
        workspaceRoot,
        logger);
    restartedWorkspace.Reset();

    EqualSnapshots(sourceBefore, Snapshot(workspaceRoot), "Restart must restore a clean fixture.");
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

    Console.WriteLine("PASS disposable fixture workspace");
    Console.WriteLine("PASS native uncast state");
    Console.WriteLine("PASS deterministic failing and passing fixture tests");
    Console.WriteLine("PASS containment rejection and logging");
    Console.WriteLine("PASS clean restart reset");
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

static Dictionary<string, string> Snapshot(string path) =>
    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
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
