using System.Diagnostics;
using System.Text;
using Squad.Agents.AI;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed record WorkEvidenceItem(
    long Sequence,
    DateTimeOffset Timestamp,
    string Kind,
    string State,
    string Source,
    string? Actor = null,
    string? Summary = null,
    string? Path = null,
    string? Diff = null,
    string? Command = null,
    int? ExitCode = null,
    string? Output = null);

public sealed record WorkEvidenceSnapshot(
    IReadOnlyList<WorkEvidenceItem> Items,
    long LastSequence);

public sealed class WorkEvidenceStore
{
    private readonly object gate = new();
    private readonly List<WorkEvidenceItem> items = [];
    private long sequence;

    public void Reset()
    {
        lock (gate)
        {
            items.Clear();
            sequence = 0;
        }
    }

    public void RecordNative(
        string nativeEvent,
        string state,
        string? actor,
        string? summary)
    {
        Add(
            "specialist",
            state,
            $"Squad.Agents.AI native trace · squad.subagent · {nativeEvent}",
            actor,
            summary);
    }

    public void RecordState(string state, string summary) =>
        Add("state", state, "Work lifecycle", summary: summary);

    public void RecordFile(string category, string path, string diff) =>
        Add(category, "Changed", "Disposable workspace", path: path, diff: diff);

    public void RecordValidation(string command, int exitCode, string output) =>
        Add(
            "validation",
            exitCode == 0 ? "Passed" : "Failed",
            "Allow-listed fixture validation",
            command: command,
            exitCode: exitCode,
            output: output);

    public WorkEvidenceSnapshot GetSnapshot(long after = 0)
    {
        lock (gate)
        {
            WorkEvidenceItem[] snapshot = items
                .Where(item => item.Sequence > after)
                .ToArray();
            return new(snapshot, sequence);
        }
    }

    public bool HasSpecialistActivity
    {
        get
        {
            lock (gate)
            {
                return items.Any(item => item.Kind == "specialist");
            }
        }
    }

    private void Add(
        string kind,
        string state,
        string source,
        string? actor = null,
        string? summary = null,
        string? path = null,
        string? diff = null,
        string? command = null,
        int? exitCode = null,
        string? output = null)
    {
        lock (gate)
        {
            items.Add(new(
                ++sequence,
                DateTimeOffset.UtcNow,
                kind,
                state,
                source,
                actor,
                summary,
                path,
                diff,
                command,
                exitCode,
                output));
        }
    }
}

public sealed class WorkEvidenceCollector(
    DisposableFixtureWorkspace workspace,
    WorkEvidenceStore evidence,
    SecretRedactor secretRedactor,
    ILogger<WorkEvidenceCollector> logger)
{
    public async Task CaptureAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult status = await RunAsync(
            "git",
            ["status", "--short", "--untracked-files=all"],
            cancellationToken);
        if (status.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not inspect workspace changes: {status.Output}");
        }

        foreach (string line in status.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = line.Length > 3 ? line[3..].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string diffText;
            if (line.StartsWith("?? ", StringComparison.Ordinal))
            {
                string[] addedLines = await File.ReadAllLinesAsync(
                    workspace.PathPolicy.ResolveWorkspacePath(path),
                    cancellationToken);
                diffText = secretRedactor.Redact(string.Join(
                    Environment.NewLine,
                    new[] { "--- /dev/null", $"+++ b/{path}" }
                        .Concat(addedLines.Select(value => $"+{value}"))))!;
            }
            else
            {
                ProcessResult diff = await RunAsync(
                    "git",
                    ["diff", "--no-ext-diff", "--unified=3", "HEAD", "--", path],
                    cancellationToken);
                if (diff.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Could not inspect {path}: {diff.Output}");
                }

                diffText = secretRedactor.Redact(diff.Output.Trim())!;
            }

            string category = WorkspacePathPolicy.IsGeneratedTeamPath(path)
                ? "harness"
                : "application";
            evidence.RecordFile(category, path, diffText);
        }

        ProcessResult validation = await RunAsync(
            "sh",
            ["tests/run.sh"],
            cancellationToken);
        evidence.RecordValidation(
            DisposableFixtureWorkspace.FixtureTestCommand,
            validation.ExitCode,
            secretRedactor.Redact(validation.Output.Trim())!);
    }

    private async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.TrimEnd()));
        logger.LogInformation(
            "Evidence command {Command} exited {ExitCode}.",
            fileName,
            process.ExitCode);
        return new(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}

public static class SquadTraceEvidence
{
    public static void Record(
        WorkEvidenceStore evidence,
        SecretRedactor secretRedactor,
        SquadAgentTraceEvent trace)
    {
        string? actor = trace.DispatchedPersonaName
            ?? trace.SubagentDisplayName
            ?? trace.SubagentName;
        string? summary = trace.Kind switch
        {
            SquadAgentTraceEventKind.SubagentDispatched =>
                trace.DispatchedPersonaDescription ?? trace.DispatchedPrompt,
            SquadAgentTraceEventKind.AssistantMessage when !string.IsNullOrWhiteSpace(trace.Content) =>
                trace.Content,
            SquadAgentTraceEventKind.AssistantMessage when trace.RequestedToolNames.Count > 0 =>
                $"Requested tools: {string.Join(", ", trace.RequestedToolNames)}",
            _ => null
        };
        string? state = trace.Kind switch
        {
            SquadAgentTraceEventKind.SubagentDispatched => "Delegated",
            SquadAgentTraceEventKind.SubagentSelected => "Selected",
            SquadAgentTraceEventKind.SubagentStarted => "Active",
            SquadAgentTraceEventKind.AssistantMessage when trace.SdkAgentId is not null => "Progress",
            SquadAgentTraceEventKind.SubagentCompleted => "Succeeded",
            SquadAgentTraceEventKind.SubagentFailed => "Failed",
            _ => null
        };

        if (state is not null)
        {
            evidence.RecordNative(
                trace.Kind.ToString(),
                state,
                secretRedactor.Redact(actor),
                secretRedactor.Redact(summary));
        }
    }
}
