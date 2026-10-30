using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SquadOnAks.Web.Work;

// The pinned AG-UI adapter captures its agent at mapping time; dispatch all session operations per request.
internal sealed class AppRoutingAgent(AIAgent first, IHttpContextAccessor accessor) : DelegatingAIAgent(first)
{
    private AIAgent Selected => accessor.HttpContext?.RequestServices.GetRequiredKeyedService<AIAgent>("Daydream") ?? InnerAgent;

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
        Selected.CreateSessionAsync(cancellationToken);

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        Selected.SerializeSessionAsync(session, jsonSerializerOptions, cancellationToken);

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        Selected.DeserializeSessionAsync(serializedState, jsonSerializerOptions, cancellationToken);

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default) =>
        Selected.RunAsync(messages, session, options, cancellationToken);

    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default) =>
        Selected.RunStreamingAsync(messages, session, options, cancellationToken);
}
