using AGUI.Client;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SquadOnAks.Web;

const string ExpectedResponse = "Scripted Squad response crossed the in-process AG-UI boundary.";

WebApplication app = SquadOnAksApplication.Build(
    ["--urls", "http://127.0.0.1:0"],
    builder =>
    {
        IChatClient scriptedClient = new ScriptedChatClient(ExpectedResponse);
        AIAgent scriptedAgent = scriptedClient.AsAIAgent(
            name: "scripted-squad",
            description: "Deterministic AG-UI smoke agent.");
        builder.Services.AddSingleton(scriptedAgent);
    });

await app.StartAsync();
try
{
    string address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!
        .Addresses.Single();

    using var httpClient = new HttpClient { BaseAddress = new Uri(address) };
    using var client = new AGUIChatClient(
        new AGUIChatClientOptions(httpClient, SquadOnAksApplication.AgentEndpoint.TrimStart('/')));
    AIAgent agent = client.AsAIAgent(
        name: "ag-ui-smoke-client",
        description: "External AG-UI client used for scripted completion evidence.");
    AgentSession session = await agent.CreateSessionAsync();
    AgentResponse response = await agent.RunAsync("Prove the AG-UI stream.", session);

    if (!string.Equals(response.Text, ExpectedResponse, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Unexpected scripted AG-UI response: '{response.Text}'.");
    }

    Console.WriteLine($"PASS: {response.Text}");
}
finally
{
    await app.StopAsync();
}

internal sealed class ScriptedChatClient(string response) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, response);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata)
            ? new ChatClientMetadata(nameof(ScriptedChatClient))
            : null;

    public void Dispose()
    {
    }
}
