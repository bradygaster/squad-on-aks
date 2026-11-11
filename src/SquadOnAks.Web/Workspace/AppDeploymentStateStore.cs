using System.Text.Json;

namespace SquadOnAks.Web.Workspace;

public enum AppDeploymentPhase
{
    Building,
    Built,
    Deploying,
    Deployed,
    Failed,
    /// <summary>The built revision is running its user journeys in a throwaway check namespace (appended: phases serialize by value).</summary>
    Checking
}

public sealed record AppDeploymentRevision(
    string RunId,
    AppDeploymentPhase Phase,
    string? ArtifactDigest,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string? Reason = null);

public sealed record AppDeploymentSnapshot(
    string AppId,
    string Namespace,
    string ReleaseName,
    IReadOnlyList<AppDeploymentRevision> Revisions);

public sealed class AppDeploymentStateStore(DisposableFixtureWorkspace workspace, AppIdentity identity)
{
    private sealed record PersistedState(
        string AppId,
        string Namespace,
        string ReleaseName,
        List<AppDeploymentRevision> Revisions);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object gate = new();
    private sealed record CheckFailure(string RunId, string Detail);

    private string StatePath => workspace.StatePath("soaks-deployments.json");
    // What the last failed journey check found, for the team's next wave only (never served to the UI).
    private string CheckFailurePath => workspace.StatePath("soaks-check-failure.json");
    private string Namespace => AppWorkerManifest.NamespaceName(identity.Id);
    private string ReleaseName => StaticAppManifest.Name(identity.Id);

    public AppDeploymentSnapshot GetSnapshot()
    {
        lock (gate)
        {
            PersistedState state = Load();
            return new(state.AppId, state.Namespace, state.ReleaseName, state.Revisions.ToArray());
        }
    }

    public AppDeploymentRevision Begin(string runId)
    {
        RequireRunId(runId);
        lock (gate)
        {
            PersistedState state = Load();
            if (state.Revisions.Any(revision => revision.RunId == runId))
            {
                throw new InvalidOperationException("A deployment run with this identifier already exists.");
            }

            if (state.Revisions.LastOrDefault() is { Phase: AppDeploymentPhase.Building or AppDeploymentPhase.Built or AppDeploymentPhase.Checking or AppDeploymentPhase.Deploying })
            {
                throw new InvalidOperationException("The previous deployment must be reconciled before another wave can start.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            var revision = new AppDeploymentRevision(runId, AppDeploymentPhase.Building, null, now, now);
            state.Revisions.Add(revision);
            Save(state);
            return revision;
        }
    }

    public AppDeploymentRevision RecordBuilt(string runId, string artifactDigest) =>
        Transition(runId, AppDeploymentPhase.Building, AppDeploymentPhase.Built, artifactDigest);

    public AppDeploymentRevision BeginCheck(string runId) =>
        Transition(runId, AppDeploymentPhase.Built, AppDeploymentPhase.Checking);

    /// <summary>Starts the real deploy after the journey check passed (or directly, for an app without journeys).</summary>
    public AppDeploymentRevision BeginDeploy(string runId) =>
        Transition(runId, AppDeploymentPhase.Built, AppDeploymentPhase.Deploying, alsoFrom: AppDeploymentPhase.Checking);

    public void RecordCheckFailure(string runId, string detail)
    {
        RequireRunId(runId);
        lock (gate)
        {
            string temporary = CheckFailurePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new CheckFailure(runId, detail.Length > 6000 ? detail[..6000] : detail)));
            File.Move(temporary, CheckFailurePath, overwrite: true);
        }
    }

    /// <summary>What the latest revision's failed journey check found; null once a newer revision exists.</summary>
    public string? LatestCheckFailure()
    {
        lock (gate)
        {
            if (!File.Exists(CheckFailurePath)) return null;
            CheckFailure? failure;
            try { failure = JsonSerializer.Deserialize<CheckFailure>(File.ReadAllText(CheckFailurePath)); }
            catch (JsonException) { return null; }
            AppDeploymentRevision? latest = Load().Revisions.LastOrDefault();
            return latest is { Phase: AppDeploymentPhase.Failed, Reason: AppDeploymentFailure.JourneysFailed } &&
                latest.RunId == failure?.RunId ? failure.Detail : null;
        }
    }

    public AppDeploymentRevision Complete(string runId) =>
        Transition(runId, AppDeploymentPhase.Deploying, AppDeploymentPhase.Deployed);

    public AppDeploymentRevision Fail(string runId, string? reason = null)
    {
        lock (gate)
        {
            PersistedState state = Load();
            int index = FindRevision(state, runId);
            AppDeploymentRevision current = state.Revisions[index];
            if (current.Phase is AppDeploymentPhase.Deployed or AppDeploymentPhase.Failed)
            {
                throw new InvalidOperationException("A completed deployment cannot be changed.");
            }

            AppDeploymentRevision failed = current with
            {
                Phase = AppDeploymentPhase.Failed,
                UpdatedAt = DateTimeOffset.UtcNow,
                Reason = AppDeploymentFailure.IsKnown(reason) ? reason : null
            };
            state.Revisions[index] = failed;
            Save(state);
            return failed;
        }
    }

    private AppDeploymentRevision Transition(
        string runId,
        AppDeploymentPhase expected,
        AppDeploymentPhase next,
        string? artifactDigest = null,
        AppDeploymentPhase? alsoFrom = null)
    {
        if (artifactDigest is not null)
        {
            RequireArtifactDigest(artifactDigest);
        }

        lock (gate)
        {
            PersistedState state = Load();
            int index = FindRevision(state, runId);
            AppDeploymentRevision current = state.Revisions[index];
            if (current.Phase != expected && current.Phase != alsoFrom)
            {
                throw new InvalidOperationException(
                    $"Deployment run {runId} is {current.Phase}; expected {expected}.");
            }

            AppDeploymentRevision updated = current with
            {
                Phase = next,
                ArtifactDigest = artifactDigest ?? current.ArtifactDigest,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            state.Revisions[index] = updated;
            Save(state);
            return updated;
        }
    }

    private PersistedState Load()
    {
        if (!File.Exists(StatePath))
        {
            return new(identity.Id, Namespace, ReleaseName, []);
        }

        PersistedState state = JsonSerializer.Deserialize<PersistedState>(
            File.ReadAllText(StatePath))
            ?? throw new InvalidOperationException("The app's deployment history could not be read.");
        if (state.AppId != identity.Id || state.Namespace != Namespace ||
            state.ReleaseName != ReleaseName || state.Revisions is null)
        {
            throw new InvalidOperationException("The saved deployment history does not belong to this app.");
        }

        if (state.Revisions.Any(revision => revision is null) ||
            state.Revisions.Select(revision => revision.RunId).Distinct(StringComparer.Ordinal).Count() != state.Revisions.Count ||
            state.Revisions.Any(revision =>
                !Enum.IsDefined(revision.Phase) || !IsValidRunId(revision.RunId) ||
                revision.UpdatedAt < revision.StartedAt ||
                revision.ArtifactDigest is not null && !IsValidArtifactDigest(revision.ArtifactDigest) ||
                revision.Phase is AppDeploymentPhase.Built or AppDeploymentPhase.Checking or AppDeploymentPhase.Deploying or AppDeploymentPhase.Deployed &&
                    revision.ArtifactDigest is null))
        {
            throw new InvalidOperationException("The saved deployment history is invalid.");
        }

        return state;
    }

    private void Save(PersistedState state)
    {
        string temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporary, StatePath, overwrite: true);
    }

    private static int FindRevision(PersistedState state, string runId)
    {
        int index = state.Revisions.FindIndex(revision => revision.RunId == runId);
        return index >= 0
            ? index
            : throw new InvalidOperationException("The deployment run was not found.");
    }

    private static void RequireRunId(string runId)
    {
        if (!IsValidRunId(runId))
        {
            throw new ArgumentException("A lowercase worker run identifier of at most 16 characters is required.", nameof(runId));
        }
    }

    private static bool IsValidRunId(string? runId) =>
        !string.IsNullOrWhiteSpace(runId) && runId.Length <= 16 &&
        runId.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    private static void RequireArtifactDigest(string digest)
    {
        if (!IsValidArtifactDigest(digest))
        {
            throw new ArgumentException("The artifact must be identified by a lowercase sha256 digest.", nameof(digest));
        }
    }

    private static bool IsValidArtifactDigest(string? digest) =>
        digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal) &&
        digest.AsSpan(7).ToArray().All(character => char.IsAsciiDigit(character) || character is (>= 'a' and <= 'f'));
}
