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

            var run = new ActiveRun(new CancellationTokenSource());
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

    internal sealed class ActiveRun(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

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
    ILogger<WorkLifecycleAgent> logger)
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
        try
        {
            await foreach (AgentResponseUpdate update in InnerAgent.RunStreamingAsync(
                messages,
                session,
                options,
                lease.CancellationToken))
            {
                await writer.WriteAsync(update);
            }

            WorkTerminalResult result = lease.CancellationRequested
                ? WorkTerminalResult.CompletedBeforeCancellation
                : WorkTerminalResult.Completed;
            if (result == WorkTerminalResult.Completed &&
                evidenceCollector is not null &&
                evidence.HasSpecialistActivity)
            {
                await evidenceCollector.CaptureAsync();
            }
            string message = result == WorkTerminalResult.CompletedBeforeCancellation
                ? "Work completed before cancellation took effect."
                : "Work completed.";
            lease.Complete(result, message);
            evidence.RecordState(result.ToString(), message);
            writer.TryComplete();
        }
        catch (OperationCanceledException) when (lease.CancellationRequested)
        {
            lease.Complete(WorkTerminalResult.Cancelled, "Work stopped after cancellation was requested.");
            evidence.RecordState("Cancelled", "Work stopped after cancellation was requested.");
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            string message = secretRedactor.Redact(exception.Message)!;
            logger.LogError("Squad work failed: {Failure}", message);
            lease.Complete(WorkTerminalResult.Failed, message);
            evidence.RecordState("Failed", message);
            writer.TryComplete(new InvalidOperationException(message));
        }
    }
}
