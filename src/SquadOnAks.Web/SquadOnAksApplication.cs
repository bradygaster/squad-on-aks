using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Squad.Agents.AI;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Components;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web;

public static class SquadOnAksApplication
{
    public const string AgentEndpoint = "/ag-ui";
    public const string CopilotTokenKey = "COPILOT_TOKEN";
    public const string WorkspacePathKey = "SQUAD_WORKSPACE_PATH";
    public const string WorkspaceDiagnosticsEndpoint = "/diagnostics/workspace";
    public const string CastStatusEndpoint = "/casting/status";
    public const string CastReviewEndpoint = "/casting/review";
    public const string CastActivationEndpoint = "/casting/activate";
    public const string WorkStatusEndpoint = "/work/status";
    public const string WorkCancellationEndpoint = "/work/cancel";

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
        builder.WebHost.UseStaticWebAssets();
        builder.AddServiceDefaults();
        builder.Services.AddAGUIServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<WorkLifecycle>();
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
        WorkLifecycle lifecycle = app.Services.GetRequiredService<WorkLifecycle>();
        if (fixtureWorkspace is not null)
        {
            lifecycle.SetCastingState(
                app.Services.GetRequiredService<CastReviewService>().GetStatus().State);
        }

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
        var lifecycleAgent = new WorkLifecycleAgent(
            squad,
            app.Services.GetRequiredService<IHttpContextAccessor>(),
            app.Services.GetRequiredService<ILogger<WorkLifecycleAgent>>());
        app.MapAGUIServer(AgentEndpoint, lifecycleAgent)
            .AddEndpointFilter(async (context, next) =>
            {
                string phase = context.HttpContext.Request.Headers["X-Squad-Phase"].ToString();
                WorkLifecycle.WorkLease? lease = lifecycle.TryBegin(
                    string.IsNullOrWhiteSpace(phase) ? "work" : phase);
                if (lease is null)
                {
                    return Results.Conflict(new
                    {
                        state = "Busy",
                        message = "Another work request is already active on this Squad pod."
                    });
                }

                context.HttpContext.Items[WorkLifecycle.LeaseItemKey] = lease;
                try
                {
                    return await next(context);
                }
                catch
                {
                    lease.Complete(WorkTerminalResult.Failed, "The AG-UI request failed before the run completed.");
                    throw;
                }
            });
        app.MapGet(WorkStatusEndpoint, () => Results.Ok(lifecycle.GetSnapshot()));
        app.MapPost(WorkCancellationEndpoint, () => Results.Ok(lifecycle.Cancel()));
        if (fixtureWorkspace is not null)
        {
            app.MapGet(
                CastStatusEndpoint,
                (CastReviewService review) => Results.Ok(review.GetStatus()));
            app.MapGet(
                CastReviewEndpoint,
                (CastReviewService review) => Results.Ok(review.GetReviewFiles()));
            app.MapPut(
                CastReviewEndpoint,
                (CastReviewEdit edit, CastReviewService review) =>
                {
                    CastReviewResult result = review.Update(edit.Path, edit.Content);
                    return result.Success
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                });
            app.MapPost(
                CastActivationEndpoint,
                (CastReviewService review) =>
                {
                    CastActivationResult result = review.Activate();
                    if (result.Success)
                    {
                        lifecycle.SetCastingState("Ready");
                    }

                    return result.Success
                        ? Results.Ok(result)
                        : Results.UnprocessableEntity(result);
                });
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
        builder.Services.AddSingleton<CastReviewService>();
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
                CliPath = builder.Configuration["COPILOT_CLI_PATH"] ?? "copilot",
                Cwd = workspace.RootPath,
                GitHubToken = copilotToken
            };
            options.CliArgs.Add("--yolo");
            options.CliArgs.Add("--agent");
            options.CliArgs.Add("squad");

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
