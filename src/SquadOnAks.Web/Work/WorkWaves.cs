using System.Text;

namespace SquadOnAks.Web.Work;

public sealed record WaveFileChange(string Path, string Status, string Category);

public sealed record WaveSummary(
    int Number,
    string Objective,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Message,
    string? CommitSha,
    string? BaseCommit,
    IReadOnlyList<WaveFileChange> ChangedFiles,
    string? ValidationState,
    IReadOnlyList<string> Specialists);

public sealed record WaveDetail(
    WaveSummary Summary,
    string Response,
    IReadOnlyList<WorkEvidenceItem> Evidence);

public sealed class WorkWave
{
    private readonly object gate = new();
    private readonly StringBuilder response = new();

    internal WorkWave(int number, string objective, string? baseCommit)
    {
        Number = number;
        Objective = objective;
        BaseCommit = baseCommit;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public int Number { get; }

    public string Objective { get; }

    public string? BaseCommit { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string State { get; private set; } = "Active";

    public string? Message { get; private set; }

    public string? CommitSha { get; private set; }

    public IReadOnlyList<WaveFileChange> ChangedFiles { get; private set; } = [];

    public IReadOnlyList<WorkEvidenceItem> Evidence { get; private set; } = [];

    public void AppendResponse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (gate)
        {
            response.Append(text);
        }
    }

    public void SetCheckpoint(string? commitSha, IReadOnlyList<WaveFileChange> changedFiles)
    {
        lock (gate)
        {
            CommitSha = commitSha;
            ChangedFiles = changedFiles;
        }
    }

    internal void Complete(string state, string message, IReadOnlyList<WorkEvidenceItem> evidence)
    {
        lock (gate)
        {
            State = state;
            Message = message;
            Evidence = evidence;
            CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    internal WaveSummary ToSummary(IReadOnlyList<WorkEvidenceItem> liveEvidence)
    {
        lock (gate)
        {
            IReadOnlyList<WorkEvidenceItem> evidence = CompletedAt is null ? liveEvidence : Evidence;
            string? validation = evidence.LastOrDefault(item => item.Kind == "validation")?.State;
            string[] specialists = evidence
                .Where(item => item.Kind == "specialist" && !string.IsNullOrWhiteSpace(item.Actor))
                .Select(item => item.Actor!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new(
                Number,
                Objective,
                State,
                StartedAt,
                CompletedAt,
                Message,
                CommitSha,
                BaseCommit,
                ChangedFiles,
                validation,
                specialists);
        }
    }

    internal WaveDetail ToDetail(IReadOnlyList<WorkEvidenceItem> liveEvidence)
    {
        lock (gate)
        {
            return new(
                ToSummary(liveEvidence),
                response.ToString(),
                CompletedAt is null ? liveEvidence : Evidence);
        }
    }
}

public sealed class WorkWaveStore(WorkEvidenceStore evidence)
{
    private readonly object gate = new();
    private readonly List<WorkWave> waves = [];

    public WorkWave Begin(string objective, string? baseCommit)
    {
        lock (gate)
        {
            string trimmed = objective.Trim();
            var wave = new WorkWave(
                waves.Count + 1,
                string.IsNullOrEmpty(trimmed) ? "(no objective)" : trimmed,
                baseCommit);
            waves.Add(wave);
            return wave;
        }
    }

    public void Complete(WorkWave wave, string state, string message) =>
        wave.Complete(state, message, evidence.GetSnapshot().Items);

    public IReadOnlyList<WaveSummary> List()
    {
        IReadOnlyList<WorkEvidenceItem> live = evidence.GetSnapshot().Items;
        lock (gate)
        {
            return waves.Select(wave => wave.ToSummary(live)).ToArray();
        }
    }

    public WaveDetail? Get(int number)
    {
        IReadOnlyList<WorkEvidenceItem> live = evidence.GetSnapshot().Items;
        lock (gate)
        {
            WorkWave? wave = waves.FirstOrDefault(item => item.Number == number);
            return wave?.ToDetail(live);
        }
    }
}
