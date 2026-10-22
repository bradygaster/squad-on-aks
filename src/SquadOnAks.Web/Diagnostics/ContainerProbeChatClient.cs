using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Diagnostics;

internal sealed class ContainerProbeChatClient(
    DisposableFixtureWorkspace workspace,
    WorkEvidenceStore evidence,
    string credential) : IChatClient
{
    private bool hasConversation;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The container probe requires streaming.");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string prompt = messages.Last().Text;
        if (prompt.Contains("explicitly confirm", StringComparison.OrdinalIgnoreCase))
        {
            CreateCast();
            hasConversation = true;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Native cast created.");
            yield break;
        }

        if (prompt.Contains("known fixture defect", StringComparison.OrdinalIgnoreCase))
        {
            evidence.RecordNative(
                "SubagentDispatched",
                "Delegated",
                "GNC",
                "Repair the route ordering defect.");
            evidence.RecordNative(
                "SubagentStarted",
                "Active",
                "GNC",
                "Specialist started.");
            await File.WriteAllTextAsync(
                workspace.PathPolicy.ResolveWorkspacePath("src/flight-plan.sh"),
                "#!/bin/sh\n\nprintf '%s\\n' LAUNCH ORBIT LAND\n",
                cancellationToken);
            evidence.RecordNative(
                "AssistantMessage",
                "Progress",
                "GNC",
                "Corrected the route order.");
            evidence.RecordNative(
                "SubagentCompleted",
                "Succeeded",
                "GNC",
                "Specialist completed.");
            hasConversation = true;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Fixture repair complete.");
            yield break;
        }

        if (prompt.Contains("contextual follow-up", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                $"retained-context:{hasConversation.ToString().ToLowerInvariant()}");
            yield break;
        }

        if (prompt.Contains("inspect clean room", StringComparison.OrdinalIgnoreCase))
        {
            bool harnessAbsent = !Directory.Exists(
                workspace.PathPolicy.ResolveWorkspacePath(".squad"));
            string application = await File.ReadAllTextAsync(
                workspace.PathPolicy.ResolveWorkspacePath("src/flight-plan.sh"),
                cancellationToken);
            ProcessResult validation = await RunFixtureTestsAsync(cancellationToken);
            bool clean = harnessAbsent &&
                application.Contains("LAND ORBIT LAUNCH", StringComparison.Ordinal) &&
                validation.ExitCode != 0 &&
                validation.Output.Contains("FAIL route order", StringComparison.Ordinal);
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                $"clean-room:{clean.ToString().ToLowerInvariant()};" +
                $"harness-absent:{harnessAbsent.ToString().ToLowerInvariant()};" +
                $"fixture-tests-exit:{validation.ExitCode}");
            yield break;
        }

        if (prompt.Contains("credential failure", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Container probe failure included credential {credential}.");
        }

        if (prompt.Contains("long-running", StringComparison.OrdinalIgnoreCase))
        {
            evidence.RecordNative(
                "SubagentStarted",
                "Active",
                "GNC",
                "Long-running specialist started.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Long-running work started.");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, "Unknown container probe request.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata)
            ? new ChatClientMetadata(nameof(ContainerProbeChatClient))
            : null;

    public void Dispose()
    {
    }

    private void CreateCast()
    {
        Write(".squad/team.md", """
            # Route Planner Squad

            ## Members

            - GNC: `.squad/agents/gnc/charter.md`
            - RETRO: `.squad/agents/retro/charter.md`
            """);
        Write(".squad/routing.md", "implementation: GNC\nreview: RETRO\n");
        Write(".squad/agents/gnc/charter.md", "# GNC\n\nImplementation specialist.\n");
        Write(".squad/agents/gnc/history.md", "# GNC history\n");
        Write(".squad/agents/retro/charter.md", "# RETRO\n\nReview specialist.\n");
        Write(".squad/agents/retro/history.md", "# RETRO history\n");
        Write(".squad/casting/state.json", "{}\n");
    }

    private void Write(string path, string content)
    {
        string fullPath = workspace.PathPolicy.ResolveWorkspacePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private async Task<ProcessResult> RunFixtureTestsAsync(
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("sh")
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("tests/run.sh");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not run fixture tests.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
