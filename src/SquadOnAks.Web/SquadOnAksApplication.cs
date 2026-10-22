using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Squad.Agents.AI;
using SquadOnAks.Web.Components;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web;

public static class SquadOnAksApplication
{
    public const string AgentEndpoint = "/ag-ui";
    public const string CopilotTokenKey = "COPILOT_TOKEN";
    public const string WorkspacePathKey = "SQUAD_WORKSPACE_PATH";
    public const string WorkspaceDiagnosticsEndpoint = "/diagnostics/workspace";

    public static WebApplication Build(
        string[] args,
        Action<WebApplicationBuilder>? configureAgent = null)
    {
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SquadOnAksApplication).Assembly.GetName().Name
        };
        WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
        builder.AddServiceDefaults();
        builder.Services.AddAGUIServer();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        if (configureAgent is null)
        {
            RegisterFixtureWorkspaceAndSquadAgent(builder);
        }
        else
        {
            configureAgent(builder);
        }

        WebApplication app = builder.Build();
        DisposableFixtureWorkspace? fixtureWorkspace =
            app.Services.GetService<DisposableFixtureWorkspace>();
        fixtureWorkspace?.Reset();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapDefaultEndpoints();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        AIAgent squad = app.Services.GetRequiredService<AIAgent>();
        app.MapAGUIServer(AgentEndpoint, squad);
        if (fixtureWorkspace is not null)
        {
            app.MapGet(
                WorkspaceDiagnosticsEndpoint,
                () => Results.Ok(fixtureWorkspace.GetDiagnostics()));
        }

        return app;
    }

    private static void RegisterFixtureWorkspaceAndSquadAgent(WebApplicationBuilder builder)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        string workspacePath = Path.GetFullPath(
            RequireValue(builder.Configuration, WorkspacePathKey));
        string fixtureSourcePath = Path.Combine(
            builder.Environment.ContentRootPath,
            "FixtureSource",
            DisposableFixtureWorkspace.FixtureName);

        builder.Services.AddSingleton(serviceProvider =>
            new DisposableFixtureWorkspace(
                fixtureSourcePath,
                workspacePath,
                serviceProvider.GetRequiredService<ILogger<DisposableFixtureWorkspace>>()));
        builder.Services.AddKeyedSingleton<AgentSessionStore>(
            "Squad",
            new InMemoryAgentSessionStore());
        builder.Services.AddSingleton<AIAgent>(serviceProvider =>
        {
            DisposableFixtureWorkspace workspace =
                serviceProvider.GetRequiredService<DisposableFixtureWorkspace>();
            var options = new SquadAgentOptions
            {
                AgentName = "Squad",
                Cwd = workspace.RootPath,
                GitHubToken = copilotToken
            };

            return new SquadAgent(
                workspace.RootPath,
                options,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        });
    }

    private static string RequireValue(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required configuration '{key}' is missing or empty.");
        }

        return value;
    }
}
