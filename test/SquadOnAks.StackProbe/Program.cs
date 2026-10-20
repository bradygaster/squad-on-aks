using AGUI.Abstractions;
using AGUI.Client;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Squad.Agents.AI;

const string Mission =
    "Maintain this .NET API. Implement small features, test them, and review changes before presenting the result.";
const string Refinement =
    "Refine the proposed cast: make the tester focus on integration tests, add accessibility review to the reviewer's charter, and remove roles that do not contribute to this API. Do not create files yet.";
const string Confirmation =
    "I confirm this refined proposal. Create the Squad using the native Init Mode confirmation path now.";
const string PostCastTurn =
    "In one sentence, identify the active Squad members and confirm that the cast is ready for work. Do not modify files.";

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project test/SquadOnAks.StackProbe -- <initialized-workspace>");
    return 2;
}

string workspace = Path.GetFullPath(args[0]);
string agentDefinition = Path.Combine(workspace, ".github", "agents", "squad.agent.md");
string copilotCliPath = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH") ?? "copilot";
if (!File.Exists(agentDefinition))
{
    throw new FileNotFoundException(
        "Native Squad agent definition is required; refusing generic-agent fallback.",
        agentDefinition);
}

Console.WriteLine($"Squad agent definition: {agentDefinition}");
Console.WriteLine($"Copilot CLI: {copilotCliPath}");
Console.WriteLine("Coordinator selection: --agent squad (generic fallback disabled)");

Dictionary<string, string> baseline = Snapshot(workspace);

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAGUIServer();
builder.Services.AddKeyedSingleton<AgentSessionStore>("Squad", new InMemoryAgentSessionStore());
builder.Services.AddSquadAgent(options =>
{
    options.SquadFolderPath = workspace;
    options.Cwd = workspace;
    options.AgentName = "Squad";
    options.CliPath = copilotCliPath;
    options.CliArgs.Add("--yolo");
    options.CliArgs.Add("--agent");
    options.CliArgs.Add("squad");
});

WebApplication app = builder.Build();
AIAgent squad = app.Services.GetRequiredService<AIAgent>();
app.MapAGUIServer("/ag-ui", squad);

await app.StartAsync();
try
{
    string address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!
        .Addresses.Single();
    using var httpClient = new HttpClient { BaseAddress = new Uri(address) };
    var httpOptions = new AGUIChatClientOptions(httpClient, "ag-ui");
    var capture = new CapturingTransport(httpOptions.Transport);
    using var client = new AGUIChatClient(new AGUIChatClientOptions { Transport = capture });
    AIAgent aguiAgent = client.AsAIAgent(
        name: "native-casting-probe",
        description: "AG-UI client for the native Squad casting boundary.");
    AgentSession aguiSession = await aguiAgent.CreateSessionAsync();

    var conversation = new ProbeConversation(aguiAgent, aguiSession, capture);

    string proposal;
    try
    {
        proposal = await conversation.SendAsync(Mission);
    }
    catch (Exception aguiFailure)
    {
        try
        {
            AgentSession diagnosticSession = await squad.CreateSessionAsync();
            await foreach (AgentResponseUpdate _ in squad.RunStreamingAsync(
                "Enter native Init Mode and propose a team for a small .NET API. Do not create files.",
                diagnosticSession))
            {
            }
        }
        catch (Exception directFailure)
        {
            throw new InvalidOperationException(
                $"AG-UI failed before RUN_STARTED. Direct SquadAgent diagnostic also failed: {directFailure}",
                aguiFailure);
        }

        throw new InvalidOperationException(
            "Direct SquadAgent streaming succeeded, but the AG-UI HTTP adapter failed before RUN_STARTED.",
            aguiFailure);
    }
    RequireStreamedText("proposal", proposal);
    AssertUnchanged(workspace, baseline, "proposal");

    string refinedProposal = await conversation.SendAsync(Refinement);
    RequireStreamedText("refinement", refinedProposal);
    AssertUnchanged(workspace, baseline, "refinement");

    string confirmation = await conversation.SendAsync(Confirmation);
    RequireStreamedText("confirmation", confirmation);
    AssertNativeCast(workspace, baseline);

    string postCast = await conversation.SendAsync(PostCastTurn);
    RequireStreamedText("post-cast turn", postCast);

    Console.WriteLine($"AG-UI thread: {conversation.ThreadId}");
    Console.WriteLine($"AG-UI runs: {string.Join(" -> ", conversation.RunIds)}");
    Console.WriteLine("PASS: native proposal, refinement, confirmation, and post-cast turn crossed Squad.Agents.AI and AG-UI.");
    return 0;
}
finally
{
    await app.StopAsync();
}

static void RequireStreamedText(string phase, string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new InvalidOperationException($"{phase} produced no streamed assistant text.");
    }

    Console.WriteLine($"{phase}: streamed {text.Length} characters");
}

static Dictionary<string, string> Snapshot(string workspace) =>
    Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories)
        .Select(path => (
            FullPath: path,
            RelativePath: Path.GetRelativePath(workspace, path).Replace('\\', '/')))
        .Where(file => IsNativeHarnessPath(file.RelativePath))
        .ToDictionary(
            file => file.RelativePath,
            file => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file.FullPath))),
            StringComparer.Ordinal);

static bool IsNativeHarnessPath(string path) =>
    path is ".gitattributes" or "meet-the-squad.md" or ".squad/team.md" or ".squad/routing.md" ||
    path.StartsWith(".squad/agents/", StringComparison.Ordinal) &&
        (path.EndsWith("/charter.md", StringComparison.Ordinal) ||
         path.EndsWith("/history.md", StringComparison.Ordinal)) ||
    path.StartsWith(".squad/casting/", StringComparison.Ordinal) &&
        path.EndsWith(".json", StringComparison.Ordinal);

static void AssertUnchanged(string workspace, Dictionary<string, string> expected, string phase)
{
    Dictionary<string, string> actual = Snapshot(workspace);
    string[] changes = ChangedPaths(expected, actual);
    if (changes.Length != 0)
    {
        throw new InvalidOperationException(
            $"{phase} wrote files before confirmation: {string.Join(", ", changes)}");
    }

    Console.WriteLine($"{phase}: filesystem unchanged");
}

static void AssertNativeCast(string workspace, Dictionary<string, string> baseline)
{
    Dictionary<string, string> actual = Snapshot(workspace);
    string[] changes = ChangedPaths(baseline, actual);
    if (changes.Length == 0)
    {
        throw new InvalidOperationException("Confirmation did not write the native Squad harness.");
    }

    string[] nonNativeChanges = changes
        .Where(path =>
            !path.StartsWith(".squad/", StringComparison.Ordinal) &&
            path is not ".gitattributes" and not "meet-the-squad.md")
        .ToArray();
    if (nonNativeChanges.Length != 0)
    {
        throw new InvalidOperationException(
            $"Confirmation wrote non-Squad paths: {string.Join(", ", nonNativeChanges)}");
    }

    RequireFile(workspace, ".squad/team.md");
    RequireFile(workspace, ".squad/routing.md");

    string[] charters = Directory.GetFiles(Path.Combine(workspace, ".squad", "agents"), "charter.md", SearchOption.AllDirectories);
    string[] histories = Directory.GetFiles(Path.Combine(workspace, ".squad", "agents"), "history.md", SearchOption.AllDirectories);
    string[] castingState = Directory.GetFiles(Path.Combine(workspace, ".squad", "casting"), "*.json", SearchOption.TopDirectoryOnly);
    if (charters.Length == 0 || histories.Length == 0 || castingState.Length == 0)
    {
        throw new InvalidOperationException(
            $"Incomplete native harness: charters={charters.Length}, histories={histories.Length}, casting state={castingState.Length}.");
    }

    Console.WriteLine(
        $"confirmation: native changes={changes.Length}, charters={charters.Length}, histories={histories.Length}, casting state files={castingState.Length}");
}

static string[] ChangedPaths(
    IReadOnlyDictionary<string, string> before,
    IReadOnlyDictionary<string, string> after) =>
    before.Keys
        .Union(after.Keys, StringComparer.Ordinal)
        .Where(path =>
            !before.TryGetValue(path, out string? oldHash) ||
            !after.TryGetValue(path, out string? newHash) ||
            oldHash != newHash)
        .Order(StringComparer.Ordinal)
        .ToArray();

static void RequireFile(string workspace, string relativePath)
{
    string path = Path.Combine(workspace, relativePath);
    if (!File.Exists(path) || new FileInfo(path).Length == 0)
    {
        throw new InvalidOperationException($"Native Squad file is missing or empty: {relativePath}");
    }
}

internal sealed class ProbeConversation(
    AIAgent agent,
    AgentSession session,
    CapturingTransport capture)
{
    private readonly List<ChatMessage> _messages = [];
    private string? _previousRunId;

    public string? ThreadId { get; private set; }

    public List<string> RunIds { get; } = [];

    public async Task<string> SendAsync(string prompt)
    {
        _messages.Add(new ChatMessage(ChatRole.User, prompt));
        var response = new System.Text.StringBuilder();
        ThreadId ??= Guid.NewGuid().ToString("N");
        var options = new ChatOptions
        {
            RawRepresentationFactory = _ => new RunAgentInput
            {
                ThreadId = ThreadId,
                RunId = Guid.NewGuid().ToString("N"),
                ParentRunId = _previousRunId,
            },
        };

        var runOptions = new ChatClientAgentRunOptions { ChatOptions = options };
        capture.Reset();
        await foreach (AgentResponseUpdate agentUpdate in agent.RunStreamingAsync(_messages, session, runOptions))
        {
            ChatResponseUpdate update = agentUpdate.AsChatResponseUpdate();
            if (!string.IsNullOrEmpty(update.Text))
            {
                response.Append(update.Text);
            }
        }

        RunStartedEvent? started = capture.RunStarted;
        if (started is null)
        {
            throw new InvalidOperationException(
                $"AG-UI did not emit RUN_STARTED. Events: {string.Join(", ", capture.EventTypes)}");
        }

        if (!string.Equals(ThreadId, started.ThreadId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AG-UI changed thread from '{ThreadId}' to '{started.ThreadId}'.");
        }

        _previousRunId = started.RunId;
        RunIds.Add(started.RunId);

        string text = response.ToString();
        _messages.Add(new ChatMessage(ChatRole.Assistant, text));
        return text;
    }
}

internal sealed class CapturingTransport(IAGUITransport inner) : IAGUITransport
{
    public RunStartedEvent? RunStarted { get; private set; }

    public List<string> EventTypes { get; } = [];

    public void Reset()
    {
        RunStarted = null;
        EventTypes.Clear();
    }

    public async IAsyncEnumerable<BaseEvent> SendAsync(
        RunAgentInput input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (BaseEvent item in inner.SendAsync(input, cancellationToken))
        {
            EventTypes.Add(item is RunErrorEvent error
                ? $"{nameof(RunErrorEvent)}({error.Code}: {error.Message})"
                : item.GetType().Name);
            if (item is RunStartedEvent started)
            {
                RunStarted = started;
            }

            yield return item;
        }
    }
}
