using System.Text;
using System.Text.Json;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Casting;

public sealed record OriginalIntent(string Text, string Source);

public sealed class OriginalIntentException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class OriginalIntentStore(
    DisposableFixtureWorkspace workspace,
    ILogger<OriginalIntentStore> logger)
{
    private const int MaximumTextLength = 64 * 1024;
    private readonly object gate = new();

    public OriginalIntent Get()
    {
        lock (gate)
        {
            return Access(() => ReadSaved() ?? Recover());
        }
    }

    public void Capture(string text)
    {
        lock (gate)
        {
            Access(() =>
            {
                if (ReadSaved() is not null)
                {
                    return true;
                }

                // A legacy workspace must recover its original, not adopt a later refinement.
                if (Directory.Exists(workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state")))
                {
                    Recover();
                    return true;
                }

                ValidateText(text);
                Save(new(text, "submitted"));
                return true;
            });
        }
    }

    private OriginalIntent? ReadSaved()
    {
        string path = workspace.PathPolicy.ResolveWorkspacePath(".git/squad-original-intent.json");
        if (!File.Exists(path))
        {
            return null;
        }

        if (new FileInfo(path).Length > MaximumTextLength * 6L)
        {
            throw InvalidData();
        }

        OriginalIntent? intent = JsonSerializer.Deserialize<OriginalIntent>(File.ReadAllText(path));
        if (intent is null || intent.Source is not ("submitted" or "native-session"))
        {
            throw InvalidData();
        }
        ValidateText(intent.Text);
        return intent;
    }

    private OriginalIntent Recover()
    {
        string root = workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state");
        if (!Directory.Exists(root))
        {
            throw Missing();
        }

        OriginalIntent? candidate = null;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            // Each native session owns its events; never follow links or choose by mtime.
            string relative = Path.GetRelativePath(workspace.RootPath, Path.Combine(directory, "events.jsonl"));
            string path = workspace.PathPolicy.ResolveWorkspacePath(relative);
            if (!File.Exists(path))
            {
                continue;
            }

            using var reader = new StreamReader(path);
            int consumed = 0;
            while (ReadBoundedLine(reader) is { } line)
            {
                consumed += line.Length;
                if (consumed > MaximumTextLength * 16)
                {
                    throw InvalidData();
                }
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement item = document.RootElement;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw InvalidData();
                }
                if (!item.TryGetProperty("type", out JsonElement type) ||
                    type.ValueKind != JsonValueKind.String)
                {
                    throw InvalidData();
                }
                if (type.GetString() != "user.message")
                {
                    continue;
                }

                if (!item.TryGetProperty("data", out JsonElement data) ||
                    data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("content", out JsonElement content) ||
                    content.ValueKind != JsonValueKind.String)
                {
                    throw InvalidData();
                }

                string text = content.GetString()!;
                // Only the first user event identifies an initial casting session.
                if (text.EndsWith(WorkLifecycleAgent.CastingGuard, StringComparison.Ordinal))
                {
                    text = text[..^WorkLifecycleAgent.CastingGuard.Length];
                    ValidateText(text);
                    if (candidate is not null)
                    {
                        throw new OriginalIntentException(
                            "Multiple initial casting sessions exist. Original intent requires an explicit session selection.",
                            StatusCodes.Status409Conflict);
                    }
                    candidate = new(text, "native-session");
                }
                break;
            }
        }

        if (candidate is null)
        {
            throw Missing();
        }
        Save(candidate);
        return candidate;
    }

    private static string? ReadBoundedLine(StreamReader reader)
    {
        var line = new StringBuilder();
        while (reader.Read() is int character && character != -1)
        {
            if (character == '\n')
            {
                return line.ToString().TrimEnd('\r');
            }
            line.Append((char)character);
            if (line.Length > MaximumTextLength * 6)
            {
                throw InvalidData();
            }
        }
        return line.Length == 0 ? null : line.ToString();
    }

    private void Save(OriginalIntent intent)
    {
        string path = workspace.PathPolicy.ResolveWorkspacePath(".git/squad-original-intent.json");
        string temporary = workspace.PathPolicy.ResolveWorkspacePath(".git/squad-original-intent.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(intent));
        File.Move(temporary, path, overwrite: true);
    }

    private T Access<T>(Func<T> action)
    {
        try
        {
            string gitDirectory = workspace.PathPolicy.ResolveWorkspacePath(".git");
            if (!Directory.Exists(gitDirectory))
            {
                throw new OriginalIntentException(
                    "Original intent requires this app's standalone workspace Git directory; linked worktrees are not supported.",
                    StatusCodes.Status422UnprocessableEntity);
            }
            return action();
        }
        catch (OriginalIntentException exception)
        {
            logger.LogWarning("Original intent unavailable: {Reason}", exception.Message);
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            logger.LogWarning("Original intent data or contained path is invalid.");
            throw InvalidData();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Original intent storage could not be read or written.");
            throw new OriginalIntentException(
                "Original intent storage is unavailable. Retry after workspace storage is accessible.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static void ValidateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumTextLength)
        {
            throw InvalidData();
        }
    }

    private static OriginalIntentException Missing() => new(
        "No original casting intent was recorded for this workspace.",
        StatusCodes.Status404NotFound);

    private static OriginalIntentException InvalidData() => new(
        "Original intent data is malformed or unsafe. Restore the workspace's intent record or native session data.",
        StatusCodes.Status422UnprocessableEntity);
}
