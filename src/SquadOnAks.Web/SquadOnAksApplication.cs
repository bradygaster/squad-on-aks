using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Squad.Agents.AI;
using SquadOnAks.Web.Components;

namespace SquadOnAks.Web;

public static class SquadOnAksApplication
{
    public const string AgentEndpoint = "/ag-ui";
    public const string CopilotTokenKey = "COPILOT_TOKEN";
    public const string WorkspacePathKey = "SQUAD_WORKSPACE_PATH";

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
            RegisterSquadAgent(builder);
        }
        else
        {
            configureAgent(builder);
        }

        WebApplication app = builder.Build();

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

        return app;
    }

    private static void RegisterSquadAgent(WebApplicationBuilder builder)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        string workspacePath = Path.GetFullPath(
            RequireValue(builder.Configuration, WorkspacePathKey));

        if (!Directory.Exists(workspacePath))
        {
            throw new InvalidOperationException(
                $"Required configuration '{WorkspacePathKey}' points to a missing directory: {workspacePath}");
        }

        builder.Services.AddSquadAgent(options =>
        {
            options.AgentName = "Squad";
            options.SquadFolderPath = workspacePath;
            options.Cwd = workspacePath;
            options.GitHubToken = copilotToken;
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
