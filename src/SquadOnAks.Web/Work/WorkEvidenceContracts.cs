namespace SquadOnAks.Web.Work;

public static class WorkEvidenceKinds
{
    public const string Delegation = "delegation";
    public const string Progress = "progress";
    public const string Change = "change";
    public const string Validation = "validation";
}

public static class WorkEvidenceCategories
{
    public const string Activity = "activity";
    public const string Application = "application";
    public const string Harness = "harness";
    public const string Validation = "validation";
}

public sealed record WorkEvidenceEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    string Kind,
    string Category,
    string Status,
    string Summary,
    string Source,
    string? Actor = null,
    string? Path = null,
    string? Diff = null,
    string? Command = null,
    int? ExitCode = null,
    string? Output = null);

public sealed record WorkEvidenceBatch(
    IReadOnlyList<WorkEvidenceEvent> Events,
    long LastSequence);

public sealed class WorkEvidenceStore
{
    private const int MaximumEvents = 200;
    private readonly object gate = new();
    private readonly List<WorkEvidenceEvent> events = [];
    private long nextSequence;

    public void BeginRun()
    {
        lock (gate)
        {
            events.Clear();
            nextSequence = 0;
        }
    }

    public WorkEvidenceEvent Add(
        string kind,
        string category,
        string status,
        string summary,
        string source,
        string? actor = null,
        string? path = null,
        string? diff = null,
        string? command = null,
        int? exitCode = null,
        string? output = null)
    {
        lock (gate)
        {
            var item = new WorkEvidenceEvent(
                ++nextSequence,
                DateTimeOffset.UtcNow,
                kind,
                category,
                status,
                summary,
                source,
                actor,
                path,
                diff,
                command,
                exitCode,
                output);
            events.Add(item);
            if (events.Count > MaximumEvents)
            {
                events.RemoveRange(0, events.Count - MaximumEvents);
            }

            return item;
        }
    }

    public WorkEvidenceBatch ReadAfter(long sequence)
    {
        lock (gate)
        {
            WorkEvidenceEvent[] result = events
                .Where(item => item.Sequence > sequence)
                .ToArray();
            return new(result, nextSequence);
        }
    }
}
