using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SquadOnAks.Web.Work;

public static class SquadLifecycleStates
{
    public const string Starting = "Starting";
    public const string NeedsCast = "Needs cast";
    public const string Casting = "Casting";
    public const string CastReview = "Cast review";
    public const string Ready = "Ready";
    public const string Active = "Active";
    public const string Cancelling = "Cancelling";
    public const string Failed = "Failed";
    public const string Replacing = "Replacing";
}

public sealed class WorkLifecycle
{
    public const string LeaseItemKey = "SquadOnAks.WorkLease";

    private readonly object gate = new();
    private ActiveRun? activeRun;
    private WorkSnapshot snapshot;
    private bool shuttingDown;
    private bool shutdownFailed;
    private bool castActivated;
    private TaskCompletionSource idle = CompletedIdle();

    public WorkLifecycle(string initialState = SquadLifecycleStates.Starting)
    {
        snapshot = new(initialState, null, null, false);
        castActivated = initialState == SquadLifecycleStates.Ready;
    }

    public WorkSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return snapshot;
        }
    }

    public bool IsShuttingDown
    {
        get
        {
            lock (gate)
            {
                return shuttingDown;
            }
        }
    }

    public bool IsCastActive
    {
        get
        {
            lock (gate)
            {
                return castActivated;
            }
        }
    }

    public void SetCastingState(string state)
    {
        lock (gate)
        {
            if (activeRun is null)
            {
                castActivated |= state == SquadLifecycleStates.Ready;
                snapshot = new(state, snapshot.TerminalResult, snapshot.Message, false);
            }
        }
    }

    public WorkLease? TryBegin(string phase)
    {
        lock (gate)
        {
            if (activeRun is not null || shuttingDown ||
                (castActivated && phase is "casting" or "cast-review"))
            {
                return null;
            }

            var run = new ActiveRun(new CancellationTokenSource(), phase);
            idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            run.SuccessState = phase switch
            {
                "casting" => SquadLifecycleStates.Casting,
                "cast-review" => SquadLifecycleStates.CastReview,
                "publish" or "delete" => snapshot.State,
                _ => SquadLifecycleStates.Ready
            };
            activeRun = run;
            string activeState = phase switch
            {
                "work" => SquadLifecycleStates.Active,
                "publish" => "Publishing",
                "delete" => "Deleting",
                _ => SquadLifecycleStates.Casting
            };
            snapshot = new(activeState, null,
                phase switch
                {
                    "publish" => "The workspace is reserved for publication.",
                    "delete" => "The app is being deleted.",
                    _ => "A Squad turn is running."
                }, true);
            return new WorkLease(this, run);
        }
    }

    public CancelWorkResult Cancel()
    {
        lock (gate)
        {
            if (activeRun is null)
            {
                return new(false, snapshot.State, snapshot.TerminalResult);
            }

            activeRun.CancellationRequested = true;
            snapshot = new(
                SquadLifecycleStates.Cancelling,
                null,
                "Cancellation was requested.",
                true);
            activeRun.Cancellation.Cancel();
            return new(true, snapshot.State, null);
        }
    }

    public void BeginShutdown()
    {
        lock (gate)
        {
            if (shuttingDown)
            {
                return;
            }

            shuttingDown = true;
            if (activeRun is null)
            {
                snapshot = new(
                    SquadLifecycleStates.Replacing,
                    snapshot.TerminalResult,
                    "The pod is shutting down for replacement.",
                    false);
                return;
            }

            activeRun.CancellationRequested = true;
            snapshot = new(
                SquadLifecycleStates.Cancelling,
                null,
                "The active run is stopping because the pod is shutting down.",
                true);
            activeRun.Cancellation.Cancel();
        }
    }

    public Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return idle.Task.WaitAsync(cancellationToken);
        }
    }

    public void FailShutdown()
    {
        lock (gate)
        {
            shutdownFailed = true;
            snapshot = new(
                SquadLifecycleStates.Failed,
                WorkTerminalResult.Failed,
                "The active run did not stop within the shutdown grace period.",
                activeRun is not null);
        }
    }

    private void Complete(ActiveRun run, WorkTerminalResult terminalResult, string message)
    {
        lock (gate)
        {
            if (!ReferenceEquals(activeRun, run))
            {
                return;
            }

            string state = terminalResult == WorkTerminalResult.Failed || shutdownFailed
                ? SquadLifecycleStates.Failed
                : shuttingDown
                    ? SquadLifecycleStates.Replacing
                    : run.SuccessState;
            snapshot = new(state, terminalResult, message, false);
            activeRun = null;
            run.Cancellation.Dispose();
            idle.TrySetResult();
        }
    }

    private static TaskCompletionSource CompletedIdle()
    {
        var source = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    internal sealed class ActiveRun(CancellationTokenSource cancellation, string phase)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public string Phase { get; } = phase;

        public bool CancellationRequested { get; set; }

        public string SuccessState { get; set; } = SquadLifecycleStates.Ready;
    }

    public sealed class WorkLease
    {
        private readonly WorkLifecycle owner;
        private readonly ActiveRun run;
        private int completed;

        internal WorkLease(WorkLifecycle owner, ActiveRun run)
        {
            this.owner = owner;
            this.run = run;
        }

        public CancellationToken CancellationToken => run.Cancellation.Token;

        public string Phase => run.Phase;

        public bool CancellationRequested => run.CancellationRequested;

        public void Complete(WorkTerminalResult terminalResult, string message)
        {
            if (Interlocked.Exchange(ref completed, 1) == 0)
            {
                owner.Complete(run, terminalResult, message);
            }
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkTerminalResult>))]
public enum WorkTerminalResult
{
    Cancelled,
    Completed,
    CompletedBeforeCancellation,
    Failed
}

public sealed record WorkSnapshot(
    string State,
    WorkTerminalResult? TerminalResult,
    string? Message,
    bool IsBusy);

public sealed record CancelWorkResult(
    bool Requested,
    string State,
    WorkTerminalResult? TerminalResult);

public sealed class WorkLifecycleAgent(
    AIAgent innerAgent,
    IHttpContextAccessor httpContextAccessor,
    WorkEvidenceStore evidence,
    WorkEvidenceCollector? evidenceCollector,
    Security.SecretRedactor secretRedactor,
    ILogger<WorkLifecycleAgent> logger,
    WorkWaveStore? waves = null,
    Casting.OriginalIntentStore? originalIntent = null,
    AppBuildPolicy? buildPolicy = null,
    WorkerDependencyCatalog? dependencies = null,
    Workspace.AppDeploymentStateStore? deployments = null,
    TimeSpan? stallLimit = null)
    : DelegatingAIAgent(innerAgent)
{
    /// <summary>A turn with no streamed reply and no team activity for this long is stopped instead of hanging.</summary>
    public static readonly TimeSpan DefaultStallLimit = TimeSpan.FromMinutes(15);
    private readonly TimeSpan stall = stallLimit ?? DefaultStallLimit;

    public static string StalledMessage(TimeSpan limit) =>
        $"The team stopped responding for {(limit.TotalMinutes >= 1 ? $"{limit.TotalMinutes:0} minutes" : $"{limit.TotalSeconds:0} seconds")}, " +
        "so this turn was stopped. The AI service may be busy; try again in a moment.";

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        WorkLifecycle.WorkLease lease = GetLease();
        var channel = Channel.CreateUnbounded<AgentResponseUpdate>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

        _ = ProduceAsync(messages, session, options, lease, channel.Writer);

        await foreach (AgentResponseUpdate update in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }

    internal const string CastingGuard =
        "\n\n(Squad on AKS casting phase: you may propose or refine the cast, and create the team files only after " +
        "a clear user confirmation. Do not write, scaffold, test, or run any project code or docs in this turn, " +
        "and do not start a first wave. Project work arrives later as separate wave requests.)";

    private static List<ChatMessage> AddCastingGuard(IEnumerable<ChatMessage> messages)
    {
        List<ChatMessage> guarded = [.. messages];
        int index = guarded.FindLastIndex(message => message.Role == ChatRole.User);
        if (index >= 0)
        {
            guarded[index] = new ChatMessage(ChatRole.User, guarded[index].Text + CastingGuard);
        }

        return guarded;
    }

    private WorkLifecycle.WorkLease GetLease()
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context?.Items[WorkLifecycle.LeaseItemKey] is WorkLifecycle.WorkLease lease)
        {
            return lease;
        }

        throw new InvalidOperationException("The AG-UI work request did not acquire a lifecycle lease.");
    }

    // The team never sees the last wave's check output in chat, so a "fix what failed" wave would
    // have to guess. Give the model (never the UI) a bounded, redacted tail of that output.
    public static string? PreviousValidationFailure(WorkWaveStore? waves, Security.SecretRedactor secretRedactor)
    {
        if (waves?.List().LastOrDefault() is not { ValidationState: "Failed" } previous) return null;
        WorkEvidenceItem? failed = waves.Get(previous.Number)?.Evidence
            .LastOrDefault(item => item.Kind == "validation" && item.State == "Failed" && !string.IsNullOrWhiteSpace(item.Output));
        if (failed is null) return null;
        string output = secretRedactor.Redact(failed.Output) ?? string.Empty;
        if (output.Length > 6000) output = output[^6000..];
        return $"The app's checks ({failed.Command ?? "tests/run.sh"}) failed at the end of the previous wave " +
            $"with exit code {failed.ExitCode}. Their output (the end of it, if long) was:\n```\n{output}\n```\n" +
            "If this wave is asked to fix the checks, fix the cause shown there, then rerun them.";
    }

    // The last attempt to put the app online failed its journeys in a throwaway copy: tell the team what the browser saw.
    public static string? PreviousCheckFailure(Workspace.AppDeploymentStateStore? deployments, Security.SecretRedactor secretRedactor)
    {
        (string Reason, string Detail)? failure;
        try { failure = deployments?.LatestFailure(); }
        catch (InvalidOperationException) { return null; }
        if (failure is not { } found || string.IsNullOrWhiteSpace(found.Detail)) return null;
        string detail = found.Detail;
        if (found.Reason == Workspace.AppDeploymentFailure.NotReady)
            return "The last attempt to put the app online deployed it, but it never became ready, so the previous revision " +
                "kept running. When the deployment gave up, these workloads (by AppHost resource) were not ready:\n```\n" +
                (secretRedactor.Redact(detail) ?? string.Empty) + "\n```\n" +
                "CrashLoopBackOff or restarts mean that project's process exits at startup: check its startup code, the " +
                "connection and service names it reads, and that it creates its own database if it doesn't exist. Running " +
                "without becoming ready means its /health/ready never answered 200 on port 8080 (for example it keeps retrying a " +
                "database that doesn't exist). When one project's health check depends on others, fix those first. Fix the " +
                "app so every workload becomes ready.";
        return "The last attempt to put the app online deployed it to a throwaway copy with an empty database and ran the " +
            "journeys from app.build.json in a real browser at the app's root URL. They failed:\n```\n" +
            (secretRedactor.Redact(detail) ?? string.Empty) + "\n```\n" +
            "An API answer listed above (for example 404 or 415) is often the cause: check that the front end calls the API " +
            "with the method, path and JSON content type the API expects. Fix the app so these journeys pass; the journeys " +
            "are the contract, so change them only if they are wrong.";
    }

    private async Task ProduceAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        WorkLifecycle.WorkLease lease,
        ChannelWriter<AgentResponseUpdate> writer)
    {
        WorkWave? wave = null;
        bool stalled = false;
        string? previousFailure = lease.Phase == "work"
            ? string.Join("\n\n", new[] { PreviousValidationFailure(waves, secretRedactor), PreviousCheckFailure(deployments, secretRedactor) }
                .OfType<string>()) is { Length: > 0 } context ? context : null
            : null;
        if (lease.Phase == "work" && waves is not null)
        {
            string objective = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            string? baseCommit = null;
            try
            {
                baseCommit = evidenceCollector is null ? null : await evidenceCollector.GetHeadAsync(lease.CancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning("Could not read the wave base commit: {Failure}", secretRedactor.Redact(exception.Message));
            }

            wave = waves.Begin(secretRedactor.Redact(objective) ?? string.Empty, baseCommit);
        }
        try
        {
            if (lease.Phase == "casting")
            {
                string? submittedIntent = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
                if (submittedIntent is not null)
                {
                    originalIntent?.Capture(submittedIntent);
                }
                messages = AddCastingGuard(messages);
            }
            else if (lease.Phase == "work" && httpContextAccessor.HttpContext?.Request.PathBase.HasValue == true)
            {
                messages = new[] { new ChatMessage(ChatRole.System,
                    "If the requested app is a static site, its preview is served below a per-app URL prefix. Use document-relative " +
                    "links and asset URLs, not URLs starting with '/', and keep a usable index.html in a supported static-site folder. " +
                    "For a server-backed app, preserve the requested architecture and real services. Do not replace it with a static " +
                    "mock or claim an informational landing page is the running application.") }.Concat(messages);
            }
            if (lease.Phase == "work" && buildPolicy is not null)
                messages = new[] { new ChatMessage(ChatRole.System, buildPolicy.Instructions() +
                    "\nFor local development npm commands, first run npm config set registry \"https://packagefeedproxy.microsoft.io/npm/\" " +
                    "and verify npm config get registry returns that exact value. This local-development rule does not apply in GitHub Actions. " +
                    "Validation/runtime remains offline; use its cached dependencies or prebuilt assets.") }.Concat(messages);
            if (previousFailure is not null)
                messages = new[] { new ChatMessage(ChatRole.System, previousFailure) }.Concat(messages);
            if (lease.Phase == "work" && dependencies is not null)
            {
                string hints = await dependencies.InstructionsAsync(lease.CancellationToken);
                if (hints.Length > 0) messages = new[] { new ChatMessage(ChatRole.System, hints) }.Concat(messages);
            }
            // A model call that never answers would otherwise leave the turn "in progress" indefinitely. Streamed
            // replies and the team's recorded activity both count as progress.
            using (var watchdog = CancellationTokenSource.CreateLinkedTokenSource(lease.CancellationToken))
            {
                long lastSequence = evidence.GetSnapshot(long.MaxValue).LastSequence;
                long lastActivity = Environment.TickCount64;
                TimeSpan interval = TimeSpan.FromMilliseconds(Math.Clamp(stall.TotalMilliseconds / 4, 50, 30_000));
                Task monitor = Task.Run(async () =>
                {
                    while (!watchdog.IsCancellationRequested)
                    {
                        try { await Task.Delay(interval, watchdog.Token); }
                        catch (OperationCanceledException) { return; }
                        long sequence = evidence.GetSnapshot(long.MaxValue).LastSequence;
                        if (sequence != lastSequence)
                        {
                            lastSequence = sequence;
                            Interlocked.Exchange(ref lastActivity, Environment.TickCount64);
                        }
                        if (Environment.TickCount64 - Interlocked.Read(ref lastActivity) > stall.TotalMilliseconds)
                        {
                            stalled = true;
                            await watchdog.CancelAsync();
                            return;
                        }
                    }
                }, CancellationToken.None);
                IAsyncEnumerator<AgentResponseUpdate> stream = InnerAgent.RunStreamingAsync(
                    messages,
                    session,
                    options,
                    watchdog.Token).GetAsyncEnumerator(watchdog.Token);
                // A hung model call may ignore cancellation: stop waiting on it rather than relying on it to return.
                Task stopped = Task.Delay(Timeout.Infinite, watchdog.Token);
                Task<bool>? abandoned = null;
                try
                {
                    while (true)
                    {
                        Task<bool> next = stream.MoveNextAsync().AsTask();
                        if (await Task.WhenAny(next, stopped) != next)
                        {
                            abandoned = next;
                            watchdog.Token.ThrowIfCancellationRequested();
                        }
                        if (!await next) break;
                        AgentResponseUpdate update = stream.Current;
                        Interlocked.Exchange(ref lastActivity, Environment.TickCount64);
                        wave?.AppendResponse(secretRedactor.Redact(update.Text));
                        await writer.WriteAsync(update);
                    }
                }
                finally
                {
                    await watchdog.CancelAsync();
                    await monitor;
                    if (abandoned is null) await stream.DisposeAsync();
                    else _ = abandoned.ContinueWith(_ => stream.DisposeAsync().AsTask(), TaskScheduler.Default).Unwrap()
                        .ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }
            }

            WorkTerminalResult result = lease.CancellationRequested
                ? WorkTerminalResult.CompletedBeforeCancellation
                : WorkTerminalResult.Completed;
            if (evidenceCollector is not null &&
                (wave is not null || (result == WorkTerminalResult.Completed && evidence.HasSpecialistActivity)))
            {
                if (lease.CancellationRequested)
                {
                    await TryCaptureAsync(wave);
                }
                else
                {
                    await evidenceCollector.CaptureAsync(wave, lease.CancellationToken, runValidation: wave is not null);
                }
            }
            string message = result == WorkTerminalResult.CompletedBeforeCancellation
                ? "Work completed before cancellation took effect."
                : "Work completed.";
            Finish(lease, wave, result, message);
            writer.TryComplete();
        }
        catch (Exception) when (stalled && !lease.CancellationRequested)
        {
            // A stalled turn fails with a fixed, plain message (never the service's error text).
            string message = StalledMessage(stall);
            logger.LogError("Squad work stalled: no reply or team activity for {Limit}.", stall);
            httpContextAccessor.HttpContext?.RequestServices?.GetService<Workspace.ManagedAppRuntime>()
                ?.Invalidate("Failed", "This wave stopped before the running app could be verified.");
            await TryCaptureAsync(wave);
            Finish(lease, wave, WorkTerminalResult.Failed, message);
            writer.TryComplete(new InvalidOperationException(message));
        }
        catch (OperationCanceledException) when (lease.CancellationRequested)
        {
            const string message = "Work stopped after cancellation was requested.";
            httpContextAccessor.HttpContext?.RequestServices.GetService<Workspace.ManagedAppRuntime>()
                ?.Invalidate("Cancelled", "The current wave was stopped before running-app checks completed.");
            await TryCaptureAsync(wave);
            Finish(lease, wave, WorkTerminalResult.Cancelled, message);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            string message = secretRedactor.Redact(exception.Message)!;
            Workspace.ManagedAppRuntime? runtime = httpContextAccessor.HttpContext?.RequestServices.GetService<Workspace.ManagedAppRuntime>();
            if (runtime?.GetStatus().State is not ("Failed" or "Blocked"))
                runtime?.Invalidate("Failed", "This wave failed before the running app could be verified.");
            logger.LogError("Squad work failed: {Failure}", message);
            await TryCaptureAsync(wave);
            Finish(lease, wave, WorkTerminalResult.Failed, message);
            writer.TryComplete(new InvalidOperationException(message));
        }
    }

    private async Task TryCaptureAsync(WorkWave? wave)
    {
        if (wave is null || evidenceCollector is null)
        {
            return;
        }

        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await evidenceCollector.CaptureAsync(wave, cleanup.Token, runValidation: false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Could not checkpoint wave {Wave}: {Failure}",
                wave.Number,
                secretRedactor.Redact(exception.Message));
        }
    }

    private void Finish(
        WorkLifecycle.WorkLease lease, WorkWave? wave, WorkTerminalResult result, string message)
    {
        try
        {
            evidence.RecordState(result.ToString(), message);
            if (wave is not null)
            {
                waves!.Complete(wave, result.ToString(), message);
            }
        }
        finally
        {
            lease.Complete(result, message);
        }
    }
}
