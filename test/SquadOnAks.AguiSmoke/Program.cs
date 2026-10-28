using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SquadOnAks.Web;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

VerifyShutdownStateMachine();
VerifyCastLifecycleBoundary();
VerifySecretRedaction();

if (args.Contains("--serve-browser", StringComparer.Ordinal))
{
    WebApplication browserApp = SquadOnAksApplication.Build(
        args.Where(argument => argument != "--serve-browser").ToArray(),
        builder =>
        {
            builder.Services.AddSingleton<AIAgent>(services =>
                new LifecycleChatClient(
                    services.GetRequiredService<DisposableFixtureWorkspace>(),
                    services.GetRequiredService<WorkEvidenceStore>())
                .AsAIAgent(
                    name: "Squad",
                    description: "Browser lifecycle acceptance agent."));
        });
    browserApp.Run();
    return;
}

var scriptedClient = new LifecycleChatClient();
WebApplication app = SquadOnAksApplication.Build(
    ["--urls", "http://127.0.0.1:0"],
    builder =>
    {
        AIAgent scriptedAgent = scriptedClient.AsAIAgent(
            name: "Squad",
            description: "Deterministic lifecycle integration agent.");
        builder.Services.AddSingleton(scriptedAgent);
    });

await app.StartAsync();
try
{
    string address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!
        .Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };

    await VerifyIncrementalStreamingAsync(client, scriptedClient);
    await VerifyRetainedContextAsync(client);
    await VerifyBusyAndCancellationAsync(client, scriptedClient);
    await VerifyCompletedBeforeCancellationAsync(client, scriptedClient);
    await VerifyDisconnectSurvivalAsync(client, scriptedClient);
    await VerifyFailureAndRecoveryAsync(client);
    app.Services.GetRequiredService<WorkLifecycle>().SetCastingState(SquadLifecycleStates.Ready);
    await VerifyRecastRejectionAsync(client);
    await VerifyHealthAndShutdownAsync(client, app);

    Console.WriteLine("PASS: real AG-UI lifecycle integration scenarios");
}
finally
{
    if (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        await app.StopAsync();
    }
}

static async Task VerifyIncrementalStreamingAsync(
    HttpClient client,
    LifecycleChatClient scriptedClient)
{
    scriptedClient.ResetSignals();
    using HttpResponseMessage response = await SendAsync(
        client,
        "slow",
        [Message("user", "slow")],
        HttpCompletionOption.ResponseHeadersRead);
    Require(response.StatusCode == HttpStatusCode.OK, "Incremental request was not accepted.");

    await scriptedClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
    using Stream stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);
    var stopwatch = Stopwatch.StartNew();
    string firstDelta = await ReadEventAsync(reader, "TEXT_MESSAGE_CONTENT");
    Require(firstDelta.Contains("first", StringComparison.Ordinal), "First streamed chunk was missing.");
    Require(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300), "First chunk was buffered until completion.");

    string secondDelta = await ReadEventAsync(reader, "TEXT_MESSAGE_CONTENT");
    Require(secondDelta.Contains("second", StringComparison.Ordinal), "Second streamed chunk was missing.");
    await WaitForStateAsync(client, "Ready", WorkTerminalResult.Completed);
}

static async Task VerifyRetainedContextAsync(HttpClient client)
{
    IReadOnlyList<object> messages =
    [
        Message("user", "first turn"),
        Message("assistant", "first response"),
        Message("user", "context")
    ];
    using HttpResponseMessage response = await SendAsync(client, "context", messages);
    string body = await response.Content.ReadAsStringAsync();
    Require(
        body.Contains("retained-context:true", StringComparison.Ordinal),
        "Follow-up request did not reach the native agent with retained thread context.");
}

static async Task VerifyBusyAndCancellationAsync(
    HttpClient client,
    LifecycleChatClient scriptedClient)
{
    scriptedClient.ResetSignals();
    Task<HttpResponseMessage> active = SendAsync(
        client,
        "block",
        [Message("user", "block")],
        HttpCompletionOption.ResponseHeadersRead);
    await scriptedClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

    using HttpResponseMessage conflict = await SendAsync(
        client,
        "competing",
        [Message("user", "competing")]);
    Require(conflict.StatusCode == HttpStatusCode.Conflict, "Concurrent request did not receive 409 Conflict.");
    using HttpResponseMessage live = await client.GetAsync("/health/live");
    using HttpResponseMessage ready = await client.GetAsync("/health/ready");
    Require(live.StatusCode == HttpStatusCode.OK, "Liveness failed during active work.");
    Require(ready.StatusCode == HttpStatusCode.OK, "Readiness failed during active work.");

    using HttpResponseMessage cancel = await client.PostAsync(
        SquadOnAksApplication.WorkCancellationEndpoint,
        null);
    cancel.EnsureSuccessStatusCode();
    CancelWorkResult? cancelResult = await cancel.Content.ReadFromJsonAsync<CancelWorkResult>();
    Require(cancelResult is { Requested: true, State: "Cancelling" }, "Cancellation did not enter Cancelling.");
    using HttpResponseMessage activeResponse = await active;
    _ = await activeResponse.Content.ReadAsStringAsync();
    await WaitForStateAsync(client, "Ready", WorkTerminalResult.Cancelled);
}

static async Task VerifyCompletedBeforeCancellationAsync(
    HttpClient client,
    LifecycleChatClient scriptedClient)
{
    scriptedClient.ResetSignals();
    Task<HttpResponseMessage> active = SendAsync(
        client,
        "complete-before-cancel",
        [Message("user", "complete-before-cancel")],
        HttpCompletionOption.ResponseHeadersRead);
    await scriptedClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

    using HttpResponseMessage cancel = await client.PostAsync(
        SquadOnAksApplication.WorkCancellationEndpoint,
        null);
    cancel.EnsureSuccessStatusCode();
    using HttpResponseMessage activeResponse = await active;
    _ = await activeResponse.Content.ReadAsStringAsync();
    await WaitForStateAsync(client, "Ready", WorkTerminalResult.CompletedBeforeCancellation);
}

static async Task VerifyDisconnectSurvivalAsync(
    HttpClient client,
    LifecycleChatClient scriptedClient)
{
    scriptedClient.ResetSignals();
    using HttpResponseMessage response = await SendAsync(
        client,
        "disconnect",
        [Message("user", "disconnect")],
        HttpCompletionOption.ResponseHeadersRead);
    await scriptedClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

    using Stream stream = await response.Content.ReadAsStreamAsync();
    using (var reader = new StreamReader(stream, leaveOpen: true))
    {
        _ = await ReadEventAsync(reader, "TEXT_MESSAGE_CONTENT");
    }

    response.Dispose();
    using HttpResponseMessage conflict = await SendAsync(
        client,
        "during-disconnect",
        [Message("user", "during-disconnect")]);
    Require(
        conflict.StatusCode == HttpStatusCode.Conflict,
        "Disconnected transport incorrectly released the active server run.");
    await WaitForStateAsync(client, "Ready", WorkTerminalResult.Completed);
}

static async Task VerifyFailureAndRecoveryAsync(HttpClient client)
{
    using HttpResponseMessage failed = await SendAsync(
        client,
        "fail",
        [Message("user", "fail")]);
    string failureStream = await failed.Content.ReadAsStringAsync();
    Require(
        failureStream.Contains("RUN_ERROR", StringComparison.Ordinal),
        "Agent exception was not visible as an AG-UI terminal error.");
    await WaitForStateAsync(client, "Failed", WorkTerminalResult.Failed);

    using HttpResponseMessage recovered = await SendAsync(
        client,
        "recover",
        [Message("user", "recover")]);
    recovered.EnsureSuccessStatusCode();
    _ = await recovered.Content.ReadAsStringAsync();
    await WaitForStateAsync(client, "Ready", WorkTerminalResult.Completed);
}

static async Task VerifyHealthAndShutdownAsync(HttpClient client, WebApplication app)
{
    using HttpResponseMessage live = await client.GetAsync("/health/live");
    using HttpResponseMessage ready = await client.GetAsync("/health/ready");
    Require(live.StatusCode == HttpStatusCode.OK, "Liveness must be healthy before shutdown.");
    Require(ready.StatusCode == HttpStatusCode.OK, "Readiness must be healthy before shutdown.");
    Require(string.IsNullOrEmpty(await live.Content.ReadAsStringAsync()), "Liveness exposed details.");
    Require(string.IsNullOrEmpty(await ready.Content.ReadAsStringAsync()), "Readiness exposed details.");

    await app.StopAsync();
    WorkSnapshot snapshot = app.Services.GetRequiredService<WorkLifecycle>().GetSnapshot();
    Require(snapshot.State == SquadLifecycleStates.Replacing, "Graceful shutdown did not enter Replacing.");
}

static void VerifyShutdownStateMachine()
{
    var lifecycle = new WorkLifecycle(SquadLifecycleStates.Ready);
    WorkLifecycle.WorkLease lease = lifecycle.TryBegin("work")
        ?? throw new InvalidOperationException("Could not begin shutdown state test.");
    Require(
        lifecycle.GetSnapshot().State == SquadLifecycleStates.Active,
        "Work did not enter Active before shutdown.");

    lifecycle.BeginShutdown();
    Require(
        lifecycle.GetSnapshot().State == SquadLifecycleStates.Cancelling,
        "Active shutdown did not enter Cancelling.");
    lease.Complete(WorkTerminalResult.Cancelled, "Stopped for replacement.");
    Require(
        lifecycle.GetSnapshot().State == SquadLifecycleStates.Replacing,
        "Completed shutdown did not enter Replacing.");

    var failed = new WorkLifecycle(SquadLifecycleStates.Ready);
    _ = failed.TryBegin("work")
        ?? throw new InvalidOperationException("Could not begin failed shutdown state test.");
    failed.BeginShutdown();
    failed.FailShutdown();
    Require(
        failed.GetSnapshot().State == SquadLifecycleStates.Failed,
        "Timed-out shutdown did not remain visibly Failed.");
}

static void VerifyCastLifecycleBoundary()
{
    var lifecycle = new WorkLifecycle(SquadLifecycleStates.NeedsCast);
    WorkLifecycle.WorkLease proposal = lifecycle.TryBegin("casting")
        ?? throw new InvalidOperationException("Initial casting must be allowed.");
    proposal.Complete(WorkTerminalResult.Completed, "Proposed.");
    WorkLifecycle.WorkLease confirmation = lifecycle.TryBegin("cast-review")
        ?? throw new InvalidOperationException("Confirmation must be allowed before activation.");
    confirmation.Complete(WorkTerminalResult.Completed, "Confirmed.");
    lifecycle.SetCastingState(SquadLifecycleStates.Ready);
    Require(lifecycle.TryBegin("casting") is null, "Active Squad accepted recasting.");
    Require(lifecycle.TryBegin("cast-review") is null, "Active Squad accepted reconfirmation.");
    WorkLifecycle.WorkLease work = lifecycle.TryBegin("work")
        ?? throw new InvalidOperationException("Active Squad must accept work.");
    work.Complete(WorkTerminalResult.Failed, "Visible failure.");
    Require(lifecycle.TryBegin("casting") is null, "Work failure unlocked recasting.");
    Require(lifecycle.TryBegin("cast-review") is null, "Work failure unlocked reconfirmation.");
    WorkLifecycle.WorkLease recovery = lifecycle.TryBegin("work")
        ?? throw new InvalidOperationException("Failed work must remain retryable.");
    recovery.Complete(WorkTerminalResult.Cancelled, "Cancelled.");
    Require(lifecycle.TryBegin("casting") is null, "Cancellation unlocked recasting.");
}

static async Task VerifyRecastRejectionAsync(HttpClient client)
{
    foreach (string phase in new[] { "casting", "cast-review" })
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ag-ui");
        request.Headers.Add("X-Squad-Phase", phase);
        request.Content = JsonContent.Create(new
        {
            threadId = Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            messages = new[] { Message("user", "Do not recast an active Squad.") },
            state = new { },
            tools = Array.Empty<object>(),
            context = Array.Empty<object>(),
            forwardedProps = new { }
        });
        using HttpResponseMessage response = await client.SendAsync(request);
        Require(response.StatusCode == HttpStatusCode.Conflict, $"{phase} was not rejected.");
        Require(
            (await response.Content.ReadAsStringAsync()).Contains("already active", StringComparison.Ordinal),
            $"{phase} rejection did not explain the lifecycle boundary.");
    }
}

static void VerifySecretRedaction()
{
    const string secret = "issue13-redaction-sentinel";
    var redactor = new SecretRedactor(secret);
    string redacted = redactor.Redact($"failure contained {secret}")!;
    Require(!redacted.Contains(secret, StringComparison.Ordinal), "Secret redaction retained the credential.");
    Require(
        redacted.Contains(SecretRedactor.Replacement, StringComparison.Ordinal),
        "Secret redaction did not mark the removed credential.");
}

static async Task<HttpResponseMessage> SendAsync(
    HttpClient client,
    string prompt,
    IReadOnlyList<object> messages,
    HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, SquadOnAksApplication.AgentEndpoint)
    {
        Content = JsonContent.Create(new
        {
            threadId = "lifecycle-thread",
            runId = Guid.NewGuid().ToString("N"),
            messages,
            state = new { },
            tools = Array.Empty<object>(),
            context = Array.Empty<object>(),
            forwardedProps = new { prompt }
        })
    };
    request.Headers.Add("X-Squad-Phase", "work");
    return await client.SendAsync(request, completion);
}

static object Message(string role, string content) => new
{
    id = Guid.NewGuid().ToString("N"),
    role,
    content
};

static async Task<string> ReadEventAsync(StreamReader reader, string eventType)
{
    while (await reader.ReadLineAsync() is { } line)
    {
        if (!line.StartsWith("data: ", StringComparison.Ordinal))
        {
            continue;
        }

        using JsonDocument document = JsonDocument.Parse(line[6..]);
        JsonElement root = document.RootElement;
        if (root.GetProperty("type").GetString() == eventType)
        {
            return root.TryGetProperty("delta", out JsonElement delta)
                ? delta.GetString() ?? string.Empty
                : line;
        }
    }

    throw new InvalidOperationException($"AG-UI stream ended before {eventType}.");
}

static async Task WaitForStateAsync(
    HttpClient client,
    string expectedState,
    WorkTerminalResult expectedResult)
{
    var timeout = Stopwatch.StartNew();
    while (timeout.Elapsed < TimeSpan.FromSeconds(3))
    {
        WorkSnapshot? snapshot = await client.GetFromJsonAsync<WorkSnapshot>(
            SquadOnAksApplication.WorkStatusEndpoint);
        if (snapshot?.State == expectedState && snapshot.TerminalResult == expectedResult)
        {
            return;
        }

        await Task.Delay(25);
    }

    WorkSnapshot? final = await client.GetFromJsonAsync<WorkSnapshot>(
        SquadOnAksApplication.WorkStatusEndpoint);
    throw new InvalidOperationException(
        $"Expected {expectedState}/{expectedResult}, observed {final?.State}/{final?.TerminalResult}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class LifecycleChatClient(
    DisposableFixtureWorkspace? workspace = null,
    WorkEvidenceStore? evidence = null) : IChatClient
{
    public TaskCompletionSource Started { get; private set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ResetSignals() =>
        Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Lifecycle coverage requires streaming.");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatMessage[] history = messages.ToArray();
        string prompt = history.Last().Text;
        if (prompt.Contains("explicitly confirm", StringComparison.OrdinalIgnoreCase))
        {
            CreateCast();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Native cast created.");
            yield break;
        }

        if (prompt.Contains("Maintain this route-planning", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("Refine the proposal", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("Refine it again", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                "Proposed cast: FLIGHT lead, GNC implementation specialist, RETRO reviewer.");
            yield break;
        }

        if (prompt.Contains("known fixture defect", StringComparison.OrdinalIgnoreCase))
        {
            evidence!.RecordNative("SubagentDispatched", "Delegated", "GNC", "Repair the route ordering defect.");
            await Task.Delay(75, cancellationToken);
            evidence.RecordNative("SubagentStarted", "Active", "GNC", "Specialist started.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Inspecting fixture...");
            await Task.Delay(100, cancellationToken);
            File.WriteAllText(
                workspace!.PathPolicy.ResolveWorkspacePath("src/flight-plan.sh"),
                "#!/bin/sh\n\nprintf '%s\\n' LAUNCH ORBIT LAND\n");
            evidence.RecordNative("AssistantMessage", "Progress", "GNC", "Corrected the route order.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, " repair complete.");
            evidence.RecordNative("SubagentCompleted", "Succeeded", "GNC", "Specialist completed.");
            yield break;
        }

        if (prompt.Contains("contextual follow-up", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                $"retained-context:{history.Any(message => message.Text.Contains("repair complete", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()}");
            yield break;
        }

        if (prompt.Contains("long-running", StringComparison.OrdinalIgnoreCase))
        {
            Started.TrySetResult();
            evidence!.RecordNative("SubagentDispatched", "Delegated", "GNC", "Run cancellable work.");
            evidence.RecordNative("SubagentStarted", "Active", "GNC", "Long-running specialist started.");
            evidence.RecordNative("AssistantMessage", "Progress", "GNC",
                "**Verification in progress**\n\n1. Run `tests/run.sh`.\n2. Review results.\n\n<script>window.progressInjected = true</script>");
            for (int repeat = 0; repeat < 3; repeat++)
            {
                evidence.RecordNative("AssistantMessage", "Progress", null, "Requested tools: bash");
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Long-running work started.");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }

        if (prompt.Contains("visible failure", StringComparison.OrdinalIgnoreCase))
        {
            evidence!.RecordNative("SubagentDispatched", "Delegated", "RETRO", "Exercise failure rendering.");
            evidence.RecordNative("SubagentFailed", "Failed", "RETRO", "Scripted specialist failure.");
            throw new InvalidOperationException("scripted visible failure");
        }

        switch (prompt)
        {
            case "normal":
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Incremental ");
                await Task.Delay(300, cancellationToken);
                yield return new ChatResponseUpdate(ChatRole.Assistant, "response.");
                break;

            case "long work":
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Long work started.");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                break;

            case "slow":
                Started.TrySetResult();
                yield return new ChatResponseUpdate(ChatRole.Assistant, "first ");
                await Task.Delay(350, cancellationToken);
                yield return new ChatResponseUpdate(ChatRole.Assistant, "second");
                break;

            case "context":
                bool retained = history.Any(message =>
                    message.Role == ChatRole.Assistant &&
                    message.Text.Contains("first response", StringComparison.Ordinal));
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    $"retained-context:{retained.ToString().ToLowerInvariant()}");
                break;

            case "block":
                Started.TrySetResult();
                yield return new ChatResponseUpdate(ChatRole.Assistant, "started");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                break;

            case "complete-before-cancel":
                Started.TrySetResult();
                yield return new ChatResponseUpdate(ChatRole.Assistant, "already complete");
                var released = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(released.SetResult))
                {
                    await released.Task;
                }

                break;

            case "disconnect":
                Started.TrySetResult();
                yield return new ChatResponseUpdate(ChatRole.Assistant, "server continues");
                await Task.Delay(500);
                yield return new ChatResponseUpdate(ChatRole.Assistant, " after disconnect");
                break;

            case "fail":
                yield return new ChatResponseUpdate(ChatRole.Assistant, "visible prefix");
                throw new InvalidOperationException("scripted visible failure");

            default:
                yield return new ChatResponseUpdate(ChatRole.Assistant, "recovered");
                break;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata)
            ? new ChatClientMetadata(nameof(LifecycleChatClient))
            : null;

    public void Dispose()
    {
    }

    private void CreateCast()
    {
        if (workspace is null)
        {
            return;
        }

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
        string fullPath = workspace!.PathPolicy.ResolveWorkspacePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
