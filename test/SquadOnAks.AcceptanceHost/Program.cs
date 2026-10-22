using SquadOnAks.AcceptanceHost;
using SquadOnAks.AcceptanceHost.Components;
using SquadOnAks.Web.Work;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ScriptedWorkspace>();
builder.Services.AddSingleton<ScriptedAcceptanceAgent>();
builder.Services.AddSingleton<AcceptanceRunCoordinator>();
builder.Services.AddSingleton<WorkEvidenceStore>();

WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>();
app.MapAcceptanceHarness();

app.Run();
