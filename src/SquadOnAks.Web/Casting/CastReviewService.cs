using System.Text.RegularExpressions;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Casting;

public sealed partial class CastReviewService(DisposableFixtureWorkspace workspace)
{
    private readonly object gate = new();
    private const string ActivationMarker = "soaks-cast-active";
    private bool activated = File.Exists(workspace.StatePath(ActivationMarker));

    public CastStatus GetStatus()
    {
        lock (gate)
        {
            if (!File.Exists(workspace.PathPolicy.ResolveWorkspacePath(".squad/team.md")))
            {
                SetActivated(false);
                return new("Needs cast", false, false);
            }

            return activated
                ? new("Ready", true, true)
                : new("Cast review", true, false);
        }
    }

    public IReadOnlyList<CastReviewFile> GetReviewFiles()
    {
        lock (gate)
        {
            EnsureCastExists();
            return GetAllowedPaths()
                .Select(path => new CastReviewFile(
                    path,
                    File.ReadAllText(workspace.PathPolicy.ResolveHarnessPath(path))))
                .ToArray();
        }
    }

    public CastReviewResult Update(string path, string content)
    {
        lock (gate)
        {
            if (activated)
            {
                return CastReviewResult.Rejected(
                    "This Squad is already active. Team configuration is read-only; send new work through Next wave.");
            }
            EnsureCastExists();
            string normalized = Normalize(path);
            if (!GetAllowedPaths().Contains(normalized, StringComparer.Ordinal))
            {
                return CastReviewResult.Rejected(
                    $"Rejected non-allow-listed cast review path: {path}");
            }

            string fullPath;
            try
            {
                fullPath = workspace.PathPolicy.ResolveHarnessPath(normalized);
            }
            catch (InvalidOperationException exception)
            {
                return CastReviewResult.Rejected(exception.Message);
            }

            File.WriteAllText(fullPath, content);
            SetActivated(false);
            return CastReviewResult.Accepted($"Saved {normalized}");
        }
    }

    public CastActivationResult Activate()
    {
        lock (gate)
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                SetActivated(false);
                return new(false, "Cast review", errors);
            }

            SetActivated(true);
            return new(true, "Ready", []);
        }
    }

    private void SetActivated(bool value)
    {
        activated = value;
        string marker = workspace.StatePath(ActivationMarker);
        try
        {
            if (value)
            {
                File.WriteAllText(marker, DateTimeOffset.UtcNow.ToString("O"));
            }
            else if (File.Exists(marker))
            {
                File.Delete(marker);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Activation still holds for this run; it just won't survive a restart.
        }
    }

    public IReadOnlyList<string> Validate()
    {
        lock (gate)
        {
            var errors = new List<string>();
            string teamPath = workspace.PathPolicy.ResolveWorkspacePath(".squad/team.md");
            string routingPath = workspace.PathPolicy.ResolveWorkspacePath(".squad/routing.md");

            if (!File.Exists(teamPath) || string.IsNullOrWhiteSpace(File.ReadAllText(teamPath)))
            {
                errors.Add("team.md is missing or empty.");
                return errors;
            }

            string team = File.ReadAllText(teamPath);
            if (!team.Contains("## Members", StringComparison.Ordinal))
            {
                errors.Add("team.md must contain the native '## Members' roster section.");
            }

            string[] charters = ExtractActiveCharterPaths(team);
            if (charters.Length == 0)
            {
                errors.Add("team.md must reference at least one active agent charter.");
            }

            foreach (string charter in charters)
            {
                string fullPath;
                try
                {
                    fullPath = workspace.PathPolicy.ResolveHarnessPath(charter);
                }
                catch (InvalidOperationException)
                {
                    errors.Add($"team.md references an invalid charter path: {charter}");
                    continue;
                }

                if (!File.Exists(fullPath) || string.IsNullOrWhiteSpace(File.ReadAllText(fullPath)))
                {
                    errors.Add($"Active charter is missing or empty: {charter}");
                }
            }

            if (!File.Exists(routingPath) || string.IsNullOrWhiteSpace(File.ReadAllText(routingPath)))
            {
                errors.Add("routing.md is missing or empty.");
            }

            string agentsRoot = workspace.PathPolicy.ResolveWorkspacePath(".squad/agents");
            if (!Directory.Exists(agentsRoot) ||
                Directory.GetFiles(agentsRoot, "history.md", SearchOption.AllDirectories).Length == 0)
            {
                errors.Add("Native agent histories are missing.");
            }

            string castingRoot = workspace.PathPolicy.ResolveWorkspacePath(".squad/casting");
            if (!Directory.Exists(castingRoot) ||
                Directory.GetFiles(castingRoot, "*.json", SearchOption.TopDirectoryOnly).Length == 0)
            {
                errors.Add("Native casting state is missing.");
            }

            return errors;
        }
    }

    private string[] GetAllowedPaths()
    {
        string teamPath = workspace.PathPolicy.ResolveWorkspacePath(".squad/team.md");
        if (!File.Exists(teamPath))
        {
            return [];
        }

        return
        [
            ".squad/team.md",
            ".squad/routing.md",
            .. ExtractActiveCharterPaths(File.ReadAllText(teamPath))
        ];
    }

    private void EnsureCastExists()
    {
        if (!File.Exists(workspace.PathPolicy.ResolveWorkspacePath(".squad/team.md")))
        {
            throw new InvalidOperationException(
                "Native Squad files do not exist yet. Confirm the proposal before cast review.");
        }
    }

    private static string[] ExtractActiveCharterPaths(string team) =>
        CharterPath()
            .Matches(team)
            .Select(match => Normalize(match.Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();

    [GeneratedRegex(
        @"\.squad/agents/[a-zA-Z0-9][a-zA-Z0-9-]*/charter\.md",
        RegexOptions.CultureInvariant)]
    private static partial Regex CharterPath();
}

public sealed record CastStatus(string State, bool HasCast, bool CanTakeRequests);

public sealed record CastReviewFile(string Path, string Content);

public sealed record CastReviewEdit(string Path, string Content);

public sealed record CastReviewResult(bool Success, string Message)
{
    public static CastReviewResult Accepted(string message) => new(true, message);

    public static CastReviewResult Rejected(string message) => new(false, message);
}

public sealed record CastActivationResult(
    bool Success,
    string State,
    IReadOnlyList<string> Errors);
