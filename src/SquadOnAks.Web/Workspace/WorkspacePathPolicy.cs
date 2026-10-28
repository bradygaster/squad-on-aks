using System.Text.RegularExpressions;

namespace SquadOnAks.Web.Workspace;

public sealed partial class WorkspacePathPolicy
{
    public static readonly EventId RejectedPathEvent = new(1001, "RejectedWorkspacePath");

    private readonly ILogger logger;
    private readonly string rootPrefix;
    private readonly StringComparison pathComparison;

    public WorkspacePathPolicy(string rootPath, ILogger logger)
    {
        RootPath = Path.GetFullPath(rootPath);
        this.logger = logger;
        rootPrefix = RootPath + Path.DirectorySeparatorChar;
        pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public string RootPath { get; }

    public string ResolveWorkspacePath(string relativePath)
    {
        if (!TryResolveContainedPath(relativePath, out string fullPath))
        {
            throw new InvalidOperationException(
                $"Rejected path outside the workspace root: {relativePath}");
        }

        return fullPath;
    }

    public string ResolveHarnessPath(string relativePath)
    {
        string normalized = Normalize(relativePath);
        if (!IsAllowedHarnessPath(normalized) ||
            !TryResolveContainedPath(normalized, out string fullPath))
        {
            logger.LogWarning(
                RejectedPathEvent,
                "Rejected non-allow-listed harness path {RequestedPath} for workspace {WorkspacePath}.",
                relativePath,
                RootPath);
            throw new InvalidOperationException(
                $"Rejected non-allow-listed harness path: {relativePath}");
        }

        return fullPath;
    }

    public string ResolveArtifactPath(string relativePath)
    {
        string normalized = Normalize(relativePath);
        if (normalized.Split('/').Any(segment => segment is ".git" or "node_modules") ||
            IsSquadRuntimePath(normalized))
        {
            LogRejected(relativePath);
            throw new InvalidOperationException("Private workspace metadata is not an artifact.");
        }
        return ResolveWorkspacePath(relativePath);
    }

    public static bool IsAllowedHarnessPath(string path)
    {
        string normalized = Normalize(path);
        return IsGeneratedTeamPath(normalized) ||
            normalized is ".github/agents/squad.agent.md" ||
            CoordinatorSkillPath().IsMatch(normalized);
    }

    public static bool IsGeneratedTeamPath(string path)
    {
        string normalized = Normalize(path);
        return normalized is ".gitattributes" or "meet-the-squad.md" or
            ".squad/team.md" or ".squad/routing.md" ||
            AgentHarnessPath().IsMatch(normalized) ||
            CastingStatePath().IsMatch(normalized);
    }

    // Squad's local session transcripts and stores are runtime state, never deliverables.
    public static readonly string[] SquadRuntimeExcludePatterns =
    [
        "/.squad/session-state/",
        "/.squad/session-store.db*"
    ];

    public static bool IsSquadRuntimePath(string path)
    {
        string normalized = Normalize(path);
        return normalized.StartsWith(".squad/session-state/", StringComparison.Ordinal) ||
            normalized.StartsWith(".squad/session-store.db", StringComparison.Ordinal);
    }

    public static bool IsSquadTeamPath(string path)
    {
        string normalized = Normalize(path);
        return IsGeneratedTeamPath(normalized) ||
            normalized.StartsWith(".squad/", StringComparison.Ordinal) ||
            normalized.StartsWith(".github/agents/", StringComparison.Ordinal) ||
            normalized.StartsWith(".github/skills/", StringComparison.Ordinal);
    }

    private bool TryResolveContainedPath(string relativePath, out string fullPath)
    {
        string normalized = Normalize(relativePath);
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment is "." or ".." or ""))
        {
            LogRejected(relativePath);
            fullPath = string.Empty;
            return false;
        }

        fullPath = Path.GetFullPath(
            Path.Combine(RootPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(rootPrefix, pathComparison) || ContainsReparsePoint(fullPath))
        {
            LogRejected(relativePath);
            fullPath = string.Empty;
            return false;
        }

        return true;
    }

    private bool ContainsReparsePoint(string fullPath)
    {
        string relativePath = Path.GetRelativePath(RootPath, fullPath);
        string current = RootPath;
        foreach (string segment in relativePath.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }
        }

        return false;
    }

    private void LogRejected(string requestedPath) =>
        logger.LogWarning(
            RejectedPathEvent,
            "Rejected path {RequestedPath} outside workspace root {WorkspacePath}.",
            requestedPath,
            RootPath);

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();

    [GeneratedRegex(
        @"^\.squad/agents/[a-z0-9][a-z0-9-]*/(charter|history)\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AgentHarnessPath();

    [GeneratedRegex(
        @"^\.squad/casting/[a-zA-Z0-9._-]+\.json$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CastingStatePath();

    [GeneratedRegex(
        @"^\.github/skills/coordinator-init-mode/(SKILL\.md|references/casting-reference\.md)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CoordinatorSkillPath();
}
