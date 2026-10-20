using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SquadOnAks.AcceptanceHost;

internal static class AcceptanceHarness
{
    public static void MapAcceptanceHarness(this WebApplication app)
    {
        app.MapGet("/harness/state", (ScriptedWorkspace workspace) =>
            Results.Ok(new { state = workspace.State }));

        app.MapPost("/harness/reset", (ScriptedWorkspace workspace, AcceptanceRunCoordinator coordinator) =>
        {
            coordinator.Cancel();
            workspace.Reset();
            return Results.NoContent();
        });

        app.MapPost("/harness/malformed", (ScriptedWorkspace workspace) =>
        {
            workspace.MakeMalformed();
            return Results.NoContent();
        });

        app.MapGet("/harness/files", (ScriptedWorkspace workspace) =>
            Results.Ok(workspace.Snapshot()));

        app.MapPost("/harness/files", (WorkspaceEdit edit, ScriptedWorkspace workspace) =>
        {
            WorkspaceWriteResult result = workspace.TryWrite(edit.Path, edit.Content);
            return result.Accepted
                ? Results.Ok(new { message = result.Message })
                : Results.BadRequest(new { message = result.Message });
        });

        app.MapPost("/harness/activate", (ScriptedWorkspace workspace) =>
        {
            string? validationError = workspace.TryActivate();
            return validationError is null
                ? Results.Ok(new { state = workspace.State })
                : Results.UnprocessableEntity(new { message = validationError });
        });

        app.MapPost("/harness/cancel", (AcceptanceRunCoordinator coordinator) =>
        {
            bool requested = coordinator.Cancel();
            return Results.Ok(new { requested });
        });

        app.MapPost("/ag-ui", async (
            AgUiRunRequest request,
            HttpContext context,
            ScriptedAcceptanceAgent agent,
            AcceptanceRunCoordinator coordinator) =>
        {
            CancellationTokenSource? run = coordinator.TryBegin(context.RequestAborted);
            if (run is null)
            {
                return Results.Conflict(new
                {
                    state = "Busy",
                    message = "Another browser or turn already owns the active conversation."
                });
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";

            try
            {
                await WriteAgUiEventAsync(
                    context,
                    AgUiStreamEvent.RunStarted(request.ThreadId, request.RunId));

                await foreach (AcceptanceEvent item in agent.RunAsync(request, run.Token))
                {
                    foreach (AgUiStreamEvent streamEvent in AgUiStreamEvent.FromAcceptanceEvent(
                        request.ThreadId,
                        request.RunId,
                        item))
                    {
                        await WriteAgUiEventAsync(context, streamEvent);
                    }
                }

                await WriteAgUiEventAsync(
                    context,
                    AgUiStreamEvent.RunFinished(request.ThreadId, request.RunId));
            }
            finally
            {
                coordinator.End(run);
            }

            return Results.Empty;
        });
    }

    private static async Task WriteAgUiEventAsync(
        HttpContext context,
        AgUiStreamEvent streamEvent)
    {
        await context.Response.WriteAsync(
            $"data: {JsonSerializer.Serialize(streamEvent, AgUiJsonContext.Default.AgUiStreamEvent)}\n\n",
            context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}

internal sealed record AgUiRunRequest(
    string ThreadId,
    string RunId,
    IReadOnlyList<AgUiInputMessage> Messages,
    JsonElement ForwardedProps)
{
    public string Action =>
        ForwardedProps.TryGetProperty("action", out JsonElement action)
            ? action.GetString() ?? string.Empty
            : string.Empty;

    public string? Prompt => Messages.LastOrDefault()?.Content;
}

internal sealed record AgUiInputMessage(string Id, string Role, string Content);

internal sealed record WorkspaceEdit(string Path, string Content);

internal sealed record AcceptanceEvent(
    string Type,
    string? State = null,
    string? Text = null,
    string? Actor = null,
    string? Path = null,
    string? Category = null);

internal sealed record AgUiStreamEvent(
    string Type,
    string? ThreadId = null,
    string? RunId = null,
    string? MessageId = null,
    string? Role = null,
    string? Delta = null,
    string? Name = null,
    JsonElement? Value = null)
{
    public static AgUiStreamEvent RunStarted(string threadId, string runId) =>
        new("RUN_STARTED", ThreadId: threadId, RunId: runId);

    public static AgUiStreamEvent RunFinished(string threadId, string runId) =>
        new("RUN_FINISHED", ThreadId: threadId, RunId: runId);

    public static IEnumerable<AgUiStreamEvent> FromAcceptanceEvent(
        string threadId,
        string runId,
        AcceptanceEvent item)
    {
        if (item.Type == "message")
        {
            string messageId = Guid.NewGuid().ToString("N");
            yield return new("TEXT_MESSAGE_START", MessageId: messageId, Role: "assistant");
            yield return new("TEXT_MESSAGE_CONTENT", MessageId: messageId, Delta: item.Text);
            yield return new("TEXT_MESSAGE_END", MessageId: messageId);
            yield break;
        }

        JsonElement value = item.Type switch
        {
            "state" => JsonSerializer.SerializeToElement(new { state = item.State }),
            "activity" => JsonSerializer.SerializeToElement(new { actor = item.Actor, text = item.Text }),
            "evidence" => JsonSerializer.SerializeToElement(
                new { category = item.Category, path = item.Path, text = item.Text }),
            _ => JsonSerializer.SerializeToElement(new { text = item.Text })
        };

        yield return new(
            "CUSTOM",
            ThreadId: threadId,
            RunId: runId,
            Name: item.Type,
            Value: value);
    }
}

[JsonSerializable(typeof(AgUiStreamEvent))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class AgUiJsonContext : JsonSerializerContext;

internal sealed record WorkspaceWriteResult(bool Accepted, string Message);

internal sealed class AcceptanceRunCoordinator
{
    private readonly object gate = new();
    private CancellationTokenSource? currentRun;

    public CancellationTokenSource? TryBegin(CancellationToken requestAborted)
    {
        lock (gate)
        {
            if (currentRun is not null)
            {
                return null;
            }

            currentRun = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            return currentRun;
        }
    }

    public bool Cancel()
    {
        lock (gate)
        {
            if (currentRun is null)
            {
                return false;
            }

            currentRun.Cancel();
            return true;
        }
    }

    public void End(CancellationTokenSource run)
    {
        lock (gate)
        {
            if (ReferenceEquals(currentRun, run))
            {
                currentRun.Dispose();
                currentRun = null;
            }
        }
    }
}

internal sealed class ScriptedAcceptanceAgent(ScriptedWorkspace workspace)
{
    public async IAsyncEnumerable<AcceptanceEvent> RunAsync(
        AgUiRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        switch (request.Action)
        {
            case "propose":
                workspace.SetState("Casting");
                yield return new("state", State: "Casting");
                await Delay(cancellationToken);
                yield return new("message", Text: "Proposed cast: FLIGHT lead, GNC API specialist, RETRO reviewer.");
                yield return new("evidence", Text: "No harness files exist before confirmation.", Category: "harness");
                break;

            case "refine":
                yield return new("message", Text: $"Refined proposal: {request.Prompt}");
                break;

            case "confirm":
                workspace.ConfirmHarness();
                yield return new("state", State: "Cast review");
                foreach (string path in workspace.HarnessPaths)
                {
                    yield return new("evidence", Text: "Generated harness file", Path: path, Category: "harness");
                }

                break;

            case "work":
                workspace.RequireActive();
                workspace.SetState("Active");
                yield return new("state", State: "Active");
                yield return new("activity", Text: "Specialist started", Actor: "GNC");
                await Delay(cancellationToken);
                yield return new("message", Text: "Streaming repair result: inspected fixture...");
                await Delay(cancellationToken);
                yield return new("activity", Text: "Review complete", Actor: "RETRO");
                workspace.AddApplicationChange("src/Fixture/Repair.cs");
                yield return new("evidence", Text: "Application change", Path: "src/Fixture/Repair.cs", Category: "application");
                yield return new("message", Text: "repair complete");
                break;

            case "follow-up":
                workspace.RequireActive();
                yield return new(
                    "message",
                    Text: $"Context retained for {request.ThreadId}: the earlier fixture repair passed review.");
                break;

            case "long-running":
                workspace.RequireActive();
                workspace.SetState("Active");
                yield return new("state", State: "Active");
                yield return new("activity", Text: "Long-running specialist started", Actor: "GNC");
                bool cancelled = false;
                for (int index = 0; index < 30; index++)
                {
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                    }

                    if (cancelled)
                    {
                        workspace.SetState("Cancelling");
                        yield return new("state", State: "Cancelling");
                        yield return new("message", Text: "Run stopped after cancellation was requested.");
                        break;
                    }

                    yield return new("message", Text: $"working {index + 1}");
                }

                break;

            case "failure":
                workspace.SetState("Failed");
                yield return new("state", State: "Failed");
                yield return new("message", Text: "Scripted agent failure remains visible.");
                break;

            case "disconnect":
                yield return new("state", State: "Disconnected");
                yield return new("message", Text: "The browser disconnected; no success or cancellation was inferred.");
                break;

            case "replacement":
                workspace.SetState("Replacing");
                yield return new("state", State: "Replacing");
                await Delay(cancellationToken);
                workspace.Reset();
                yield return new("state", State: "Needs cast");
                yield return new("message", Text: "Replacement started with a pristine workspace.");
                break;

            default:
                yield return new("message", Text: $"Unknown scripted action '{request.Action}'.");
                break;
        }
    }

    private static Task Delay(CancellationToken cancellationToken) =>
        Task.Delay(75, cancellationToken);
}

internal sealed partial class ScriptedWorkspace
{
    private readonly Dictionary<string, string> harnessFiles = new(StringComparer.Ordinal);
    private readonly List<string> applicationChanges = [];

    public string State { get; private set; } = "Starting";

    public IEnumerable<string> HarnessPaths => harnessFiles.Keys.Order();

    public void Reset()
    {
        harnessFiles.Clear();
        applicationChanges.Clear();
        State = "Needs cast";
    }

    public void SetState(string state) => State = state;

    public void ConfirmHarness()
    {
        harnessFiles.Clear();
        harnessFiles[".squad/team.md"] = "FLIGHT\nGNC\nRETRO\n";
        harnessFiles[".squad/routing.md"] = "work: GNC\nreview: RETRO\n";
        harnessFiles[".squad/agents/gnc/charter.md"] = "# GNC\nAPI specialist\n";
        harnessFiles[".squad/agents/retro/charter.md"] = "# RETRO\nReviewer\n";
        State = "Cast review";
    }

    public WorkspaceWriteResult TryWrite(string path, string content)
    {
        string normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment is "." or "..") ||
            !IsAllowedHarnessPath(normalized))
        {
            return new(false, $"Rejected non-allow-listed path: {path}");
        }

        harnessFiles[normalized] = content;
        return new(true, $"Saved {normalized}");
    }

    public void MakeMalformed()
    {
        if (harnessFiles.Count == 0)
        {
            ConfirmHarness();
        }

        harnessFiles[".squad/routing.md"] = "work: UNKNOWN\n";
        State = "Cast review";
    }

    public string? TryActivate()
    {
        if (!harnessFiles.TryGetValue(".squad/team.md", out string? team) ||
            !team.Contains("GNC", StringComparison.Ordinal) ||
            !team.Contains("RETRO", StringComparison.Ordinal))
        {
            return "Activation blocked: roster must contain active specialists.";
        }

        if (!harnessFiles.TryGetValue(".squad/routing.md", out string? routing) ||
            routing.Contains("UNKNOWN", StringComparison.Ordinal))
        {
            return "Activation blocked: routing references an inactive member.";
        }

        string[] requiredCharters =
        [
            ".squad/agents/gnc/charter.md",
            ".squad/agents/retro/charter.md"
        ];

        if (requiredCharters.Any(path => !harnessFiles.ContainsKey(path)))
        {
            return "Activation blocked: every active member requires a charter.";
        }

        State = "Ready";
        return null;
    }

    public void RequireActive()
    {
        if (State is not ("Ready" or "Active"))
        {
            throw new InvalidOperationException("The scripted Squad must be activated before work starts.");
        }
    }

    public void AddApplicationChange(string path) => applicationChanges.Add(path);

    public object Snapshot() => new
    {
        state = State,
        harnessFiles = harnessFiles.Keys.Order().ToArray(),
        applicationChanges = applicationChanges.ToArray()
    };

    private static bool IsAllowedHarnessPath(string path) =>
        path is ".squad/team.md" or ".squad/routing.md" ||
        ActiveCharterPath().IsMatch(path);

    [GeneratedRegex(@"^\.squad/agents/(gnc|retro)/charter\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex ActiveCharterPath();
}
