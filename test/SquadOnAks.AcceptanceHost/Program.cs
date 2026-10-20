using SquadOnAks.AcceptanceHost;
using SquadOnAks.AcceptanceHost.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ScriptedWorkspace>();
builder.Services.AddSingleton<ScriptedAcceptanceAgent>();
builder.Services.AddSingleton<AcceptanceRunCoordinator>();

WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>();
app.MapAcceptanceHarness();

app.Run();
