using AGUI.Client;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.AI;
using Microsoft.Playwright;
using Squad.Agents.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("agui", client => client.BaseAddress = new Uri("http://localhost"));
builder.Services.AddAGUIServer();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSquadAgent(options => options.SquadFolderPath = ".");
builder.Services.AddChatClient(services => new AGUIChatClient(new(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("agui"),
    "ag-ui")));

WebApplication app = builder.Build();

MapAgUi(app, app.Services.GetRequiredService<AIAgent>());
app.MapRazorComponents<ProbeRoot>()
    .AddInteractiveServerRenderMode();
_ = typeof(IPlaywright);

static IEndpointConventionBuilder MapAgUi(WebApplication app, AIAgent agent) =>
    app.MapAGUIServer("/ag-ui", agent);

internal sealed class ProbeRoot : ComponentBase;
