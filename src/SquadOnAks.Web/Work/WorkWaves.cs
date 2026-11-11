using System.Text;
using System.Text.Json;

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
    IReadOnlyList<string> Specialists,
    string? RuntimeState = null);

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

    internal WorkWave(WaveRecord record)
    {
        Number = record.Number;
        Objective = record.Objective;
        BaseCommit = record.BaseCommit;
        StartedAt = record.StartedAt;
        CompletedAt = record.CompletedAt ?? DateTimeOffset.UtcNow;
        State = record.CompletedAt is null ? "Cancelled" : record.State;
        Message = record.CompletedAt is null ? "Squad restarted before this wave finished." : record.Message;
        CommitSha = record.CommitSha;
        ChangedFiles = record.ChangedFiles ?? [];
        Evidence = record.Evidence ?? [];
        response.Append(record.Response);
    }

    internal WaveRecord ToRecord()
    {
        lock (gate)
        {
            // Keep the record small: people never see diffs or raw output, only what they add up to.
            return new(
                Number, Objective, BaseCommit, StartedAt, CompletedAt, State, Message, CommitSha,
                ChangedFiles,
                Evidence.Select(item => item with { Diff = null, Output = null }).ToArray(),
                response.ToString());
        }
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
                specialists,
                evidence.LastOrDefault(item => item.Kind == "runtime")?.State);
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

internal sealed record WaveRecord(
    int Number,
    string Objective,
    string? BaseCommit,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string State,
    string? Message,
    string? CommitSha,
    IReadOnlyList<WaveFileChange>? ChangedFiles,
    IReadOnlyList<WorkEvidenceItem>? Evidence,
    string? Response);

public sealed class WorkWaveStore(WorkEvidenceStore evidence)
{
    private readonly object gate = new();
    private readonly List<WorkWave> waves = [];
    private string? storagePath;

    /// <summary>Saves wave history beside the workspace so a restart doesn't erase what the squad delivered.</summary>
    public void UseStorage(string path)
    {
        lock (gate)
        {
            storagePath = path;
            waves.Clear();
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                WaveRecord[] records = JsonSerializer.Deserialize<WaveRecord[]>(File.ReadAllText(path)) ?? [];
                waves.AddRange(records.OrderBy(record => record.Number).Select(record => new WorkWave(record)));
            }
            catch (JsonException)
            {
                // A damaged history shouldn't stop the squad from working; new waves start a fresh record.
            }

            Save();
        }
    }

    private void Save()
    {
        if (storagePath is null || !Directory.Exists(Path.GetDirectoryName(storagePath)))
        {
            return;
        }

        try
        {
            string temporary = storagePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(waves.Select(wave => wave.ToRecord()).ToArray()));
            File.Move(temporary, storagePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The work itself still happened; only its saved copy is behind.
        }
    }

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
            Save();
            return wave;
        }
    }

    public void Complete(WorkWave wave, string state, string message)
    {
        wave.Complete(state, message, evidence.GetSnapshot().Items);
        lock (gate)
        {
            Save();
        }
    }

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
