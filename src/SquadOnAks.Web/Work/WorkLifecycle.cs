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
    private TaskCompletionSource idle = CompletedIdle();

    public WorkLifecycle(string initialState = SquadLifecycleStates.Starting)
    {
        snapshot = new(initialState, null, null, false);
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

    public void SetCastingState(string state)
    {
        lock (gate)
        {
            if (activeRun is null)
            {
                snapshot = new(state, snapshot.TerminalResult, snapshot.Message, false);
            }
        }
    }

    public WorkLease? TryBegin(string phase)
    {
        lock (gate)
        {
            if (activeRun is not null || shuttingDown)
            {
                return null;
            }

            var run = new ActiveRun(new CancellationTokenSource(), phase);
            idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            run.SuccessState = phase switch
            {
                "casting" => SquadLifecycleStates.Casting,
                "cast-review" => SquadLifecycleStates.CastReview,
                _ => SquadLifecycleStates.Ready
            };
            activeRun = run;
            string activeState = phase == "work"
                ? SquadLifecycleStates.Active
                : SquadLifecycleStates.Casting;
            snapshot = new(activeState, null, "A Squad turn is running.", true);
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
    WorkWaveStore? waves = null)
    : DelegatingAIAgent(innerAgent)
{
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

    private async Task ProduceAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        WorkLifecycle.WorkLease lease,
        ChannelWriter<AgentResponseUpdate> writer)
    {
        WorkWave? wave = null;
        if (lease.Phase == "work" && waves is not null)
        {
            string objective = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            string? baseCommit = null;
            try
            {
                baseCommit = evidenceCollector is null ? null : await evidenceCollector.GetHeadAsync();
            }
            catch (Exception exception)
            {
                logger.LogWarning("Could not read the wave base commit: {Failure}", secretRedactor.Redact(exception.Message));
            }

            wave = waves.Begin(secretRedactor.Redact(objective) ?? string.Empty, baseCommit);
        }
        else if (lease.Phase == "casting")
        {
            messages = AddCastingGuard(messages);
        }

        try
        {
            await foreach (AgentResponseUpdate update in InnerAgent.RunStreamingAsync(
                messages,
                session,
                options,
                lease.CancellationToken))
            {
                wave?.AppendResponse(secretRedactor.Redact(update.Text));
                await writer.WriteAsync(update);
            }

            WorkTerminalResult result = lease.CancellationRequested
                ? WorkTerminalResult.CompletedBeforeCancellation
                : WorkTerminalResult.Completed;
            if (evidenceCollector is not null &&
                (wave is not null || (result == WorkTerminalResult.Completed && evidence.HasSpecialistActivity)))
            {
                await evidenceCollector.CaptureAsync(wave);
            }
            string message = result == WorkTerminalResult.CompletedBeforeCancellation
                ? "Work completed before cancellation took effect."
                : "Work completed.";
            lease.Complete(result, message);
            evidence.RecordState(result.ToString(), message);
            CompleteWave(wave, result.ToString(), message);
            writer.TryComplete();
        }
        catch (OperationCanceledException) when (lease.CancellationRequested)
        {
            const string message = "Work stopped after cancellation was requested.";
            await TryCaptureAsync(wave);
            lease.Complete(WorkTerminalResult.Cancelled, message);
            evidence.RecordState("Cancelled", message);
            CompleteWave(wave, "Cancelled", message);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            string message = secretRedactor.Redact(exception.Message)!;
            logger.LogError("Squad work failed: {Failure}", message);
            await TryCaptureAsync(wave);
            lease.Complete(WorkTerminalResult.Failed, message);
            evidence.RecordState("Failed", message);
            CompleteWave(wave, "Failed", message);
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
            await evidenceCollector.CaptureAsync(wave);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Could not checkpoint wave {Wave}: {Failure}",
                wave.Number,
                secretRedactor.Redact(exception.Message));
        }
    }

    private void CompleteWave(WorkWave? wave, string state, string message)
    {
        if (wave is not null)
        {
            waves!.Complete(wave, state, message);
        }
    }
}
