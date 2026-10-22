using System.Collections.Concurrent;
using System.Diagnostics;
using Squad.Agents.AI;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed class SquadTraceEvidenceAdapter(WorkEvidenceStore evidence)
{
    private readonly ConcurrentDictionary<string, string> agentNames =
        new(StringComparer.Ordinal);

    public void Record(SquadAgentTraceEvent trace)
    {
        switch (trace.Kind)
        {
            case SquadAgentTraceEventKind.SubagentDispatched:
                string persona = FirstValue(
                    trace.DispatchedPersonaName,
                    trace.SubagentDisplayName,
                    trace.SubagentName,
                    "Specialist");
                RememberAgent(trace, persona);
                evidence.Add(
                    WorkEvidenceKinds.Delegation,
                    WorkEvidenceCategories.Activity,
                    "delegated",
                    $"Delegated to {persona}.",
                    "squad.subagent",
                    persona);
                break;

            case SquadAgentTraceEventKind.SubagentStarted:
                string startedAgent = ResolveAgent(trace);
                evidence.Add(
                    WorkEvidenceKinds.Progress,
                    WorkEvidenceCategories.Activity,
                    "active",
                    $"{startedAgent} started.",
                    "squad.subagent",
                    startedAgent);
                break;

            case SquadAgentTraceEventKind.AssistantMessage when trace.SdkAgentId is not null:
                string progressAgent = ResolveAgent(trace);
                string progress = string.IsNullOrWhiteSpace(trace.Content)
                    ? $"Requested tools: {string.Join(", ", trace.RequestedToolNames)}"
                    : trace.Content;
                evidence.Add(
                    WorkEvidenceKinds.Progress,
                    WorkEvidenceCategories.Activity,
                    "working",
                    progress,
                    "squad.subagent",
                    progressAgent);
                break;

            case SquadAgentTraceEventKind.SubagentCompleted:
                string completedAgent = ResolveAgent(trace);
                evidence.Add(
                    WorkEvidenceKinds.Progress,
                    WorkEvidenceCategories.Activity,
                    "success",
                    $"{completedAgent} completed.",
                    "squad.subagent",
                    completedAgent);
                break;

            case SquadAgentTraceEventKind.SubagentFailed:
                string failedAgent = ResolveAgent(trace);
                evidence.Add(
                    WorkEvidenceKinds.Progress,
                    WorkEvidenceCategories.Activity,
                    "failure",
                    $"{failedAgent} failed.",
                    "squad.subagent",
                    failedAgent);
                break;
        }
    }

    private void RememberAgent(SquadAgentTraceEvent trace, string persona)
    {
        string? key = trace.ToolCallId ?? trace.SdkAgentId;
        if (!string.IsNullOrWhiteSpace(key))
        {
            agentNames[key] = persona;
        }
    }

    private string ResolveAgent(SquadAgentTraceEvent trace)
    {
        foreach (string? key in new[] { trace.ToolCallId, trace.SdkAgentId })
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                agentNames.TryGetValue(key, out string? persona))
            {
                return persona;
            }
        }

        return FirstValue(trace.SubagentDisplayName, trace.SubagentName, "Specialist");
    }

    private static string FirstValue(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;
}

public sealed class FixtureEvidenceCollector(
    DisposableFixtureWorkspace workspace,
    WorkEvidenceStore evidence,
    ILogger<FixtureEvidenceCollector> logger)
{
    public async Task CollectAsync(CancellationToken cancellationToken)
    {
        foreach (WorkspaceFileChange change in workspace.GetChanges())
        {
            evidence.Add(
                WorkEvidenceKinds.Change,
                change.IsHarness
                    ? WorkEvidenceCategories.Harness
                    : WorkEvidenceCategories.Application,
                "changed",
                change.IsHarness ? "Harness change" : "Application change",
                "workspace",
                path: change.Path,
                diff: change.Diff);
        }

        ProcessResult result = await RunAsync(
            DisposableFixtureWorkspace.FixtureTestCommand,
            cancellationToken);
        evidence.Add(
            WorkEvidenceKinds.Validation,
            WorkEvidenceCategories.Validation,
            result.ExitCode == 0 ? "success" : "failure",
            result.ExitCode == 0 ? "Validation passed." : "Validation failed.",
            "process",
            command: DisposableFixtureWorkspace.FixtureTestCommand,
            exitCode: result.ExitCode,
            output: result.Output);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Fixture validation failed with exit code {result.ExitCode}.");
        }
    }

    private async Task<ProcessResult> RunAsync(
        string command,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("sh")
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start fixture validation.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));
        logger.LogInformation(
            "Fixture validation command {Command} exited with {ExitCode}.",
            command,
            process.ExitCode);
        return new(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
