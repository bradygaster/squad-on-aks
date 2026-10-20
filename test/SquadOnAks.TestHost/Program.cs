using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using SquadOnAks.TestHost.Components;
using SquadOnAks.TestHost.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAGUIServer();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAGUIServer("/ag-ui", new ScriptedChatClient().AsAIAgent(name: "scripted"));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
