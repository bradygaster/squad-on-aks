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

    public void RecordFile(string category, string path, string diff, string status = "Changed") =>
        Add(category, status, "Committed at the end of this wave", path: path, diff: diff);

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

public sealed partial class WorkEvidenceCollector(
    DisposableFixtureWorkspace workspace,
    WorkEvidenceStore evidence,
    SecretRedactor secretRedactor,
    ILogger<WorkEvidenceCollector> logger)
{
    private const int MaxDiffCharacters = 200_000;

    public async Task<string?> GetHeadAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult head = await RunAsync("git", ["rev-parse", "HEAD"], cancellationToken);
        return head.ExitCode == 0 ? head.Output.Trim() : null;
    }

    public async Task CaptureAsync(CancellationToken cancellationToken = default) =>
        await CaptureAsync(null, cancellationToken);

    public async Task CaptureAsync(WorkWave? wave, CancellationToken cancellationToken = default)
    {
        string? baseCommit = wave?.BaseCommit ?? await GetHeadAsync(cancellationToken);
        string? commit = await CheckpointAsync(
            wave is null
                ? "Squad on AKS checkpoint"
                : $"Wave {wave.Number}: {FirstLine(wave.Objective)}",
            cancellationToken);
        string? head = commit ?? await GetHeadAsync(cancellationToken);

        var changes = new List<WaveFileChange>();
        if (baseCommit is not null && head is not null && baseCommit != head)
        {
            ProcessResult names = await RunAsync(
                "git",
                ["diff", "--no-renames", "--name-status", baseCommit, head],
                cancellationToken);
            if (names.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not inspect workspace changes: {names.Output}");
            }

            foreach (string line in names.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string path = parts[1].Trim();
                ProcessResult diff = await RunAsync(
                    "git",
                    ["diff", "--no-ext-diff", "--no-renames", "--unified=3", baseCommit, head, "--", path],
                    cancellationToken);
                if (diff.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Could not inspect {path}: {diff.Output}");
                }

                string diffText = diff.Output.Trim();
                if (diffText.Length > MaxDiffCharacters)
                {
                    diffText = diffText[..MaxDiffCharacters] + "\n… diff truncated …";
                }

                if (WorkspacePathPolicy.IsSquadRuntimePath(path))
                {
                    continue;
                }

                string category = WorkspacePathPolicy.IsSquadTeamPath(path)
                    ? "harness"
                    : "application";
                evidence.RecordFile(category, path, secretRedactor.Redact(diffText)!, StatusName(parts[0].Trim()));
                changes.Add(new(path, StatusName(parts[0].Trim()), category));
            }
        }

        wave?.SetCheckpoint(commit, changes);

        if (!File.Exists(Path.Combine(workspace.RootPath, "tests", "run.sh")))
        {
            return;
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

    public async Task<string?> CheckpointAsync(string message, CancellationToken cancellationToken = default)
    {
        ProcessResult add = await RunAsync("git", ["add", "-A"], cancellationToken);
        if (add.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not stage workspace changes: {add.Output}");
        }

        ProcessResult staged = await RunAsync(
            "git",
            ["diff", "--cached", "--quiet"],
            cancellationToken);
        if (staged.ExitCode == 0)
        {
            return null;
        }

        ProcessResult commit = await RunAsync(
            "git",
            ["-c", "user.name=Squad on AKS", "-c", "user.email=squad@squad-on-aks.invalid",
                "commit", "--quiet", "--no-verify", "-m", message],
            cancellationToken);
        if (commit.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not checkpoint workspace changes: {commit.Output}");
        }

        return await GetHeadAsync(cancellationToken);
    }

    private static string FirstLine(string value)
    {
        string line = LeadingWaveLabel().Replace(value.Split('\n', 2)[0].Trim(), string.Empty);
        return line.Length > 72 ? line[..69] + "…" : line;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^wave\s*\d+\s*[:.\-–—]\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LeadingWaveLabel();

    private static string StatusName(string status) => status switch
    {
        "A" => "Added",
        "D" => "Deleted",
        "M" => "Modified",
        _ => "Changed"
    };

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
