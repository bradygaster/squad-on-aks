using System.Diagnostics;
using System.IO.Compression;
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
var publishGate = new PublishGateHandler();
WebApplication app = SquadOnAksApplication.Build(
    ["--urls", "http://127.0.0.1:0", "--COPILOT_TOKEN", "review-copilot-token",
        "--GITHUB_TOKEN", "review-github-token"],
    builder =>
    {
        builder.Services.AddHttpClient(GitHubPublisher.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => publishGate);
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

    await VerifyCastingEvidenceDoesNotValidateAsync(client, app, scriptedClient);
    await VerifyPublicationReservationAsync(client, app, publishGate);
    using (HttpResponseMessage export = await client.GetAsync("/workspace/export.zip",
        HttpCompletionOption.ResponseHeadersRead))
    {
        export.EnsureSuccessStatusCode();
        using var zip = new ZipArchive(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
        Require(zip.GetEntry("tests/run.sh") is not null &&
            !zip.Entries.Any(entry => entry.FullName.StartsWith(".git/")),
            "HTTP archive must finalize correctly without private metadata.");
        Console.WriteLine("PASS bounded ZIP export through the real asynchronous HTTP response");
    }
    await VerifyIncrementalStreamingAsync(client, scriptedClient);
    await VerifyRetainedContextAsync(client);
    await VerifyBusyAndCancellationAsync(client, scriptedClient);
    await VerifyCompletedBeforeCancellationAsync(client, scriptedClient);
    await VerifyDisconnectSurvivalAsync(client, scriptedClient);
    await VerifyFailureAndRecoveryAsync(client);
    await VerifyCancellationCleanupAsync(client, app, scriptedClient);
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

static async Task VerifyCastingEvidenceDoesNotValidateAsync(
    HttpClient client, WebApplication app, LifecycleChatClient scripted)
{
    WorkEvidenceStore evidence = app.Services.GetRequiredService<WorkEvidenceStore>();
    scripted.PlanningEvidence = evidence;
    try
    {
        foreach (string phase in new[] { "casting", "cast-review" })
        {
            evidence.Reset();
            using HttpResponseMessage response = await SendAsync(
                client, "trace planning", [Message("user", "trace planning")], phase: phase);
            response.EnsureSuccessStatusCode();
            string stream = await response.Content.ReadAsStringAsync();
            Require(!stream.Contains("RUN_ERROR", StringComparison.Ordinal),
                $"{phase} specialist activity must not trigger application validation.");
            Require(evidence.HasSpecialistActivity &&
                !evidence.GetSnapshot().Items.Any(item => item.Kind == "validation"),
                $"{phase} must retain native activity without executing application tests.");
        }
        Require(app.Services.GetRequiredService<WorkWaveStore>().List().Count == 0,
            "Planning and team setup must not create a work wave.");
        Console.WriteLine("PASS traced planning/team setup never executes application validation or creates a wave");
    }
    finally
    {
        scripted.PlanningEvidence = null;
        evidence.Reset();
        app.Services.GetRequiredService<WorkLifecycle>().SetCastingState(SquadLifecycleStates.NeedsCast);
    }
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

    DisposableFixtureWorkspace workspace = app.Services.GetRequiredService<DisposableFixtureWorkspace>();
    string git = Path.Combine(workspace.RootPath, ".git");
    string savedGit = Path.Combine(workspace.RootPath, "saved-git");
    Directory.Move(git, savedGit);
    try
    {
        await VerifyUnhealthyAsync("missing repository");
    }
    finally
    {
        Directory.Move(savedGit, git);
    }

    string generated = Path.Combine(workspace.RootPath, "readiness-regression.html");
    await File.WriteAllTextAsync(generated, "<h1>Generated application</h1>");
    try
    {
        using HttpResponseMessage edited = await client.GetAsync("/health/ready");
        Require(edited.StatusCode == HttpStatusCode.OK, "Application edits must not invalidate readiness.");
        Require(string.IsNullOrEmpty(await edited.Content.ReadAsStringAsync()), "Edited workspace probe exposed details.");
    }
    finally
    {
        File.Delete(generated);
    }

    using HttpResponseMessage missingPage = await client.GetAsync("/readiness-regression-missing-page");
    Require(missingPage.StatusCode == HttpStatusCode.NotFound, "Unknown UI paths must retain 404.");
    Require((await missingPage.Content.ReadAsStringAsync()).Contains("Not found", StringComparison.OrdinalIgnoreCase),
        "Unknown UI paths must retain their not-found page.");

    app.Services.GetRequiredService<WorkLifecycle>().BeginShutdown();
    await VerifyUnhealthyAsync("shutdown");
    await app.StopAsync();
    WorkSnapshot snapshot = app.Services.GetRequiredService<WorkLifecycle>().GetSnapshot();
    Require(snapshot.State == SquadLifecycleStates.Replacing, "Graceful shutdown did not enter Replacing.");

    async Task VerifyUnhealthyAsync(string scenario)
    {
        using HttpResponseMessage unhealthy = await client.GetAsync("/health/ready");
        Require(unhealthy.StatusCode == HttpStatusCode.ServiceUnavailable, $"{scenario}: readiness must return 503.");
        Require(string.IsNullOrEmpty(await unhealthy.Content.ReadAsStringAsync()),
            $"{scenario}: unhealthy readiness must not render a UI error page or expose details.");
        using HttpResponseMessage stillLive = await client.GetAsync("/health/live");
        Require(stillLive.StatusCode == HttpStatusCode.OK, $"{scenario}: liveness must remain healthy.");
        Require(string.IsNullOrEmpty(await stillLive.Content.ReadAsStringAsync()), $"{scenario}: liveness exposed details.");
    }
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
    HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
    string phase = "work")
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
    request.Headers.Add("X-Squad-Phase", phase);
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
            WaveSummary[] waves = (await client.GetFromJsonAsync<WaveSummary[]>("/waves"))!;
            if (waves.Length > 0)
            {
                WaveDetail detail = (await client.GetFromJsonAsync<WaveDetail>($"/waves/{waves[^1].Number}"))!;
                Require(detail.Summary.State == expectedResult.ToString() &&
                    detail.Evidence.Last().State == expectedResult.ToString(),
                    "Idle admission must not become visible before terminal evidence and wave finalization.");
            }
            return;
        }

        await Task.Delay(25);
    }

    WorkSnapshot? final = await client.GetFromJsonAsync<WorkSnapshot>(
        SquadOnAksApplication.WorkStatusEndpoint);
    throw new InvalidOperationException(
        $"Expected {expectedState}/{expectedResult}, observed {final?.State}/{final?.TerminalResult}.");
}

static async Task VerifyPublicationReservationAsync(
            HttpClient client, WebApplication app, PublishGateHandler gate)
{
    SecretRedactor redactor = app.Services.GetRequiredService<SecretRedactor>();
    Require(redactor.Redact("review-copilot-token review-github-token") == "[REDACTED] [REDACTED]",
        "Application registration must redact both configured tokens.");
    Task<HttpResponseMessage> publish = client.PostAsJsonAsync("/publish",
        new PublishRequest("test-owner", "test-repo", true, null));
    await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
    try
    {
        using HttpResponseMessage competingPublish = await client.PostAsJsonAsync("/publish",
            new PublishRequest("test-owner", "another-repo", true, null));
        Require(competingPublish.StatusCode == HttpStatusCode.Conflict,
            "Concurrent publication must be rejected while Git is reserved.");
        using HttpResponseMessage competingWork = await SendAsync(client, "competing", [Message("user", "competing")]);
        Require(competingWork.StatusCode == HttpStatusCode.Conflict,
            "Work admission must share publication's exclusive reservation.");
    }
    finally
    {
        gate.Release.TrySetResult();
    }
    using HttpResponseMessage failedPublish = await publish;
    Require(failedPublish.StatusCode == HttpStatusCode.UnprocessableEntity,
        "Scripted publication failure must be surfaced.");
    Require(!app.Services.GetRequiredService<WorkLifecycle>().GetSnapshot().IsBusy,
        "Publication failure must release the reservation.");
    WorkLifecycle lifecycle = app.Services.GetRequiredService<WorkLifecycle>();
    WorkLifecycle.WorkLease lease = lifecycle.TryBegin("work")!;
    Require(lease is not null && lifecycle.TryBegin("publish") is null,
        "Publication must also reject an existing work lease.");
    lease!.Complete(WorkTerminalResult.Completed, "Reservation test finished.");
    Console.WriteLine("PASS exclusive publish/work admission and failure release; both configured tokens redacted");
}

static async Task VerifyCancellationCleanupAsync(
    HttpClient client, WebApplication app, LifecycleChatClient scripted)
{
    string root = app.Services.GetRequiredService<DisposableFixtureWorkspace>().RootPath;
    string script = Path.Combine(root, "tests", "run.sh");
    string original = await File.ReadAllTextAsync(script);
    try
    {
        await File.WriteAllTextAsync(script, "printf ran > cancellation-validation-ran\nsleep 30\n");
        scripted.ResetSignals();
        Task<HttpResponseMessage> run = SendAsync(client, "block", [Message("user", "block")]);
        await scripted.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using HttpResponseMessage cancel = await client.PostAsync("/work/cancel", null);
        using HttpResponseMessage response = await run.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(client, "Ready", WorkTerminalResult.Cancelled);
        Require(!File.Exists(Path.Combine(root, "cancellation-validation-ran")),
            "Cancellation cleanup must checkpoint without launching generated validation.");
        WaveSummary[] summaries = (await client.GetFromJsonAsync<WaveSummary[]>("/waves"))!;
        WaveDetail cancelled = (await client.GetFromJsonAsync<WaveDetail>($"/waves/{summaries[^1].Number}"))!;
        app.Services.GetRequiredService<WorkEvidenceStore>().Reset();
        WaveDetail saved = (await client.GetFromJsonAsync<WaveDetail>($"/waves/{summaries[^1].Number}"))!;
        Require(saved.Evidence.Count == cancelled.Evidence.Count && saved.Evidence.Last().State == "Cancelled",
            "Next admission/reset must not erase the finished wave's terminal evidence.");
        Console.WriteLine("PASS cancellation checkpoint-only cleanup and finalized immutable wave evidence before admission");
    }
    finally
    {
        await File.WriteAllTextAsync(script, original);
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class PublishGateHandler : HttpMessageHandler
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
        return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"message\":\"Scripted publish unavailable\"}") };
    }
}

internal sealed class LifecycleChatClient(
    DisposableFixtureWorkspace? workspace = null,
    WorkEvidenceStore? evidence = null) : IChatClient
{
    public WorkEvidenceStore? PlanningEvidence { get; set; }

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
        if (prompt.StartsWith("trace planning", StringComparison.Ordinal))
        {
            PlanningEvidence!.RecordNative("SubagentDispatched", "Delegated", "PLANNER", "Review the app brief.");
            PlanningEvidence.RecordNative("SubagentCompleted", "Succeeded", "PLANNER", "Planning complete.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "App name: Arcade Test\nProposal ready.");
            yield break;
        }
        if (prompt.Contains("explicitly confirm", StringComparison.OrdinalIgnoreCase))
        {
            bool garden = history.Any(message => message.Text.Contains("Plan my garden", StringComparison.OrdinalIgnoreCase));
            evidence?.RecordNative("SubagentStarted", "Active", garden ? "FERN" : "FLIGHT", "Create the app team.");
            CreateCast(garden);
            if (prompt.Contains("For our neighborhood.", StringComparison.Ordinal))
                await Task.Delay(2500, cancellationToken);
            evidence?.RecordNative("SubagentCompleted", "Succeeded", garden ? "FERN" : "FLIGHT", "Team setup complete.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Native cast created.");
            yield break;
        }

        if (!prompt.Contains("When you finish,", StringComparison.Ordinal) &&
            (prompt.Contains("App name:", StringComparison.Ordinal) ||
            prompt.Contains("Maintain this route-planning", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("Refine the proposal", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("Refine it again", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("Plan my garden", StringComparison.OrdinalIgnoreCase)))
        {
            bool garden = prompt.Contains("Plan my garden", StringComparison.OrdinalIgnoreCase);
            evidence?.RecordNative("SubagentDispatched", "Delegated", "PLANNER", "Review the app brief.");
            if (evidence is not null && prompt.Contains("Maintain this route-planning", StringComparison.OrdinalIgnoreCase))
                await Task.Delay(2500, cancellationToken);
            evidence?.RecordNative("SubagentCompleted", "Succeeded", "PLANNER", "Planning complete.");
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                (prompt.Contains("App name:", StringComparison.Ordinal) ? $"App name: **{(garden ? "Garden Guide" : "Route Buddy")}**\n" : "") +
                (prompt.Contains("App name:", StringComparison.Ordinal) && !garden &&
                    !history.Any(message => message.Text.Contains("For our neighborhood.", StringComparison.Ordinal))
                    ? "App question: Who will use this app?\n"
                    : !garden ? "App question: Do you confirm this cast so I can create the team files and wait for the first wave request?\n" : "") +
                (garden ? "Proposed cast: FERN lead, BLOOM designer, RETRO reviewer." :
                    "Proposed cast: FLIGHT lead, GNC implementation specialist, RETRO reviewer."));
            yield break;
        }

        if (prompt.Contains("known fixture defect", StringComparison.OrdinalIgnoreCase))
        {
            bool garden = history.Any(message => message.Text.Contains("Plan my garden", StringComparison.OrdinalIgnoreCase));
            string member = garden ? "FERN" : "GNC";
            evidence!.RecordNative("SubagentDispatched", "Delegated", member, "Repair the route ordering defect.");
            await Task.Delay(75, cancellationToken);
            evidence.RecordNative("SubagentStarted", "Active", member, "Specialist started.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Inspecting fixture...");
            await Task.Delay(100, cancellationToken);
            File.WriteAllText(
                workspace!.PathPolicy.ResolveWorkspacePath("src/flight-plan.sh"),
                "#!/bin/sh\n\nprintf '%s\\n' LAUNCH ORBIT LAND\n");
            Write("index.html", $"<!doctype html><html lang='en'><title>{(garden ? "Your garden" : "Your route planner")}</title><link rel='stylesheet' href='style.css'><h1>{(garden ? "Room to grow" : "Your next adventure")}</h1></html>");
            Write("style.css", "body { font-family: system-ui; }");
            evidence.RecordNative("AssistantMessage", "Progress", member, "Corrected the route order.");
            yield return new ChatResponseUpdate(ChatRole.Assistant, " repair complete.");
            if (garden)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    "\n\nFor the dreamer:\n- Your garden has a welcoming page.\n- Scope: inline CSS and JS in index.html, with no dependencies or network requests.");
            }
            evidence.RecordNative("SubagentCompleted", "Succeeded", member, "Specialist completed.");
            yield break;
        }

        if (prompt.Contains("contextual follow-up", StringComparison.OrdinalIgnoreCase))
        {
            evidence?.RecordNative("SubagentCompleted", "Succeeded", "RETRO", "Follow-up reviewed.");
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

    private void CreateCast(bool garden = false)
    {
        if (workspace is null)
        {
            return;
        }

        Write(".squad/team.md", garden ? """
            # Garden Squad

            ## Members

            - FERN: `.squad/agents/fern/charter.md`
            - BLOOM: `.squad/agents/bloom/charter.md`
            """ : """
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
        if (garden)
        {
            Write(".squad/agents/fern/charter.md", "# FERN\n\nSquad lead.\n");
            Write(".squad/agents/fern/history.md", "# FERN history\n");
            Write(".squad/agents/bloom/charter.md", "# BLOOM\n\nExperience designer.\n");
            Write(".squad/agents/bloom/history.md", "# BLOOM history\n");
        }
        Write(".squad/casting/state.json", "{}\n");
    }

    private void Write(string path, string content)
    {
        string fullPath = workspace!.PathPolicy.ResolveWorkspacePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
