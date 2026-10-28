using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Squad.Agents.AI;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Components;
using SquadOnAks.Web.Diagnostics;
using SquadOnAks.Web.Health;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web;

public static class SquadOnAksApplication
{
    public const string AgentEndpoint = "/ag-ui";
    public const string CopilotTokenKey = "COPILOT_TOKEN";
    public const string ContainerProbeModeKey = "SQUAD_CONTAINER_PROBE_MODE";
    public const string WorkspacePathKey = "SQUAD_WORKSPACE_PATH";
    public const string WorkspaceTemplateKey = "SQUAD_WORKSPACE_TEMPLATE";
    public const string WorkspaceDiagnosticsEndpoint = "/diagnostics/workspace";
    public const string CastStatusEndpoint = "/casting/status";
    public const string CastReviewEndpoint = "/casting/review";
    public const string CastActivationEndpoint = "/casting/activate";
    public const string WorkStatusEndpoint = "/work/status";
    public const string WorkCancellationEndpoint = "/work/cancel";
    public const string WorkEvidenceEndpoint = "/work/evidence";
    public const string WavesEndpoint = "/waves";
    public const string PreviewPortKey = "SQUAD_PREVIEW_PORT";
    public const string PreviewUrlKey = "SQUAD_PREVIEW_URL";
    public const string GitHubTokenKey = "GITHUB_TOKEN";

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
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(SquadAgentDiagnostics.ActivitySourceName));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<WorkLifecycle>();
        builder.Services.AddHostedService<WorkLifecycleShutdownService>();
        var evidence = new WorkEvidenceStore();
        builder.Services.AddSingleton(evidence);
        var waves = new WorkWaveStore(evidence);
        builder.Services.AddSingleton(waves);
        builder.Services.AddHttpClient(GitHubPublisher.HttpClientName);
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        bool useTestAgent = configureAgent is not null;
        bool useContainerProbeAgent =
            builder.Configuration.GetValue<bool>(ContainerProbeModeKey);
        builder.Services.AddSingleton(new SecretRedactor(string.Empty));
        builder.Services.AddSingleton(new ProductionReadinessSettings(
            builder.Configuration.GetValue(
                "SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY",
                !useTestAgent)));
        RegisterFixtureWorkspace(builder, allowTemporaryPath: useTestAgent);
        if (configureAgent is not null)
        {
            configureAgent(builder);
        }
        else if (useContainerProbeAgent)
        {
            RegisterContainerProbeAgent(builder, evidence);
        }
        else
        {
            RegisterSquadAgent(builder, evidence);
        }

        WebApplication app = builder.Build();
        int? previewPort = builder.Configuration.GetValue<int?>(PreviewPortKey);
        if (previewPort is not null)
        {
            app.Use(async (context, next) =>
            {
                if (context.Connection.LocalPort != previewPort)
                {
                    await next(context);
                    return;
                }

                await ServePreviewAsync(context);
            });
        }
        DisposableFixtureWorkspace? fixtureWorkspace =
            app.Services.GetService<DisposableFixtureWorkspace>();
        if (fixtureWorkspace is not null)
        {
            if (useTestAgent ||
                builder.Configuration.GetValue<bool>("SQUAD_INITIALIZE_WORKSPACE_ON_START"))
            {
                fixtureWorkspace.Reset();
            }
            else
            {
                fixtureWorkspace.ValidateInitialized();
            }
        }
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
            evidence,
            app.Services.GetService<WorkEvidenceCollector>(),
            app.Services.GetRequiredService<SecretRedactor>(),
            app.Services.GetRequiredService<ILogger<WorkLifecycleAgent>>(),
            waves);
        app.MapAGUIServer(AgentEndpoint, lifecycleAgent)
            .AddEndpointFilter(async (context, next) =>
            {
                string phase = context.HttpContext.Request.Headers["X-Squad-Phase"].ToString();
                WorkLifecycle.WorkLease? lease = lifecycle.TryBegin(
                    string.IsNullOrWhiteSpace(phase) ? "work" : phase);
                if (lease is null)
                {
                    bool recasting = lifecycle.IsCastActive && phase is "casting" or "cast-review";
                    return Results.Conflict(new
                    {
                        state = recasting ? "Cast active" : "Busy",
                        message = recasting
                            ? "This Squad is already active. Send new work through Next wave; recasting is not supported."
                            : "Another work request is already active on this Squad pod."
                    });
                }

                if (phase == "work")
                {
                    evidence.Reset();
                    evidence.RecordState("Active", "A Squad turn is running.");
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
        app.MapGet(WavesEndpoint, () => Results.Ok(waves.List()));
        app.MapGet(
            WavesEndpoint + "/{number:int}",
            (int number) => waves.Get(number) is { } wave ? Results.Ok(wave) : Results.NotFound());
        app.MapPost(WorkCancellationEndpoint, () => Results.Ok(lifecycle.Cancel()));
        app.MapGet(
            WorkEvidenceEndpoint,
            (long? after) => Results.Ok(evidence.GetSnapshot(after ?? 0)));
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
            MapArtifactEndpoints(app, lifecycle, previewPort, builder.Configuration[PreviewUrlKey]);
        }

        return app;
    }

    private static void MapArtifactEndpoints(
        WebApplication app,
        WorkLifecycle lifecycle,
        int? previewPort,
        string? previewUrl)
    {
        app.MapGet("/workspace/files", (WorkspaceArtifacts artifacts) => Results.Ok(artifacts.ListFiles()));
        app.MapGet("/workspace/file", (string path, WorkspaceArtifacts artifacts) =>
        {
            try
            {
                return artifacts.ReadFile(path) is { } file ? Results.Ok(file) : Results.NotFound();
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { message = "That path is outside the workspace." });
            }
        });
        app.MapGet("/workspace/raw", (string path, WorkspaceArtifacts artifacts, HttpContext context) =>
        {
            try
            {
                if (artifacts.ResolveRaw(path) is not { } raw)
                {
                    return Results.NotFound();
                }

                ApplyUntrustedContentHeaders(context.Response);
                return Results.File(raw.FullPath, raw.ContentType);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { message = "That path is outside the workspace." });
            }
        });
        app.MapGet("/workspace/export.zip", async (WorkspaceArtifacts artifacts, HttpContext context) =>
        {
            context.Response.ContentType = "application/zip";
            context.Response.Headers.ContentDisposition =
                $"attachment; filename=\"squad-workspace-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip\"";
            context.Response.Headers.CacheControl = "no-store";
            await using var buffer = new MemoryStream();
            await artifacts.WriteZipAsync(buffer, context.RequestAborted);
            buffer.Position = 0;
            context.Response.ContentLength = buffer.Length;
            await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
        });
        app.MapGet("/workspace/preview", (WorkspaceArtifacts artifacts) =>
        {
            string? root = artifacts.FindPreviewRoot();
            return Results.Ok(new
            {
                enabled = previewPort is not null,
                available = previewPort is not null && root is not null,
                root,
                port = previewPort,
                url = previewUrl
            });
        });
        app.MapGet("/publish/owners", (GitHubPublisher publisher, CancellationToken cancellationToken) =>
            publisher.GetOwnersAsync(cancellationToken));
        app.MapPost("/publish", async (
            PublishRequest request,
            GitHubPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            if (lifecycle.GetSnapshot().IsBusy)
            {
                return Results.Conflict(new PublishResult(
                    false, null, null, request.Private ?? true,
                    "Wait for the active wave to finish before publishing."));
            }

            PublishResult result = await publisher.PublishAsync(request, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
        });
    }

    private static async Task ServePreviewAsync(HttpContext context)
    {
        WorkspaceArtifacts? artifacts = context.RequestServices.GetService<WorkspaceArtifacts>();
        if (artifacts is null ||
            !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (artifacts.ResolvePreview(context.Request.Path.Value) is not { } file)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                artifacts.FindPreviewRoot() is null
                    ? "The Squad has not produced an index.html yet."
                    : "Not found in the generated app.");
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.ContentType = file.ContentType;
        await context.Response.SendFileAsync(file.FullPath);
    }

    private static void ApplyUntrustedContentHeaders(HttpResponse response)
    {
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'";
        response.Headers.CacheControl = "no-store";
    }

    private static void RegisterFixtureWorkspace(
        WebApplicationBuilder builder,
        bool allowTemporaryPath)
    {
        string? configuredPath = builder.Configuration[WorkspacePathKey];
        string workspacePath = string.IsNullOrWhiteSpace(configuredPath) && allowTemporaryPath
            ? Path.Combine(
                Path.GetTempPath(),
                "squad-on-aks",
                $"acceptance-{Environment.ProcessId}-{Guid.NewGuid():N}")
            : Path.GetFullPath(RequireValue(builder.Configuration, WorkspacePathKey));
        string template = DisposableFixtureWorkspace.ResolveTemplateName(
            builder.Configuration[WorkspaceTemplateKey]);
        string fixtureSourcePath = Path.Combine(
            builder.Environment.ContentRootPath,
            "FixtureSource",
            template);
        if (!Directory.Exists(fixtureSourcePath) && allowTemporaryPath)
        {
            fixtureSourcePath = Path.Combine(
                builder.Environment.ContentRootPath,
                "src",
                "SquadOnAks.Web",
                "FixtureSource",
                template);
        }

        builder.Services.AddSingleton(serviceProvider =>
            new DisposableFixtureWorkspace(
                fixtureSourcePath,
                workspacePath,
                serviceProvider.GetRequiredService<ILogger<DisposableFixtureWorkspace>>()));
        builder.Services.AddSingleton<CastReviewService>();
        builder.Services.AddSingleton<WorkEvidenceCollector>();
        builder.Services.AddSingleton<WorkspaceArtifacts>();
        builder.Services.AddSingleton(new GitHubPublisherSettings(
            builder.Configuration[GitHubTokenKey] is { Length: > 0 } githubToken
                ? githubToken
                : builder.Configuration[CopilotTokenKey]));
        builder.Services.AddSingleton<GitHubPublisher>();
        builder.Services.AddHealthChecks()
            .AddCheck<ProductionReadinessHealthCheck>(
                "squad-runtime",
                tags: ["ready"]);
    }

    private static void RegisterSquadAgent(
        WebApplicationBuilder builder,
        WorkEvidenceStore evidence)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        builder.Services.AddSingleton(new SecretRedactor(copilotToken));
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
                GitHubToken = copilotToken,
                OnSubagentTrace = trace => SquadTraceEvidence.Record(
                    evidence,
                    serviceProvider.GetRequiredService<SecretRedactor>(),
                    trace)
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

    private static void RegisterContainerProbeAgent(
        WebApplicationBuilder builder,
        WorkEvidenceStore evidence)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        builder.Services.AddSingleton(new SecretRedactor(copilotToken));
        builder.Services.AddSingleton<AIAgent>(serviceProvider =>
            new ContainerProbeChatClient(
                serviceProvider.GetRequiredService<DisposableFixtureWorkspace>(),
                evidence,
                copilotToken)
            .AsAIAgent(
                name: "Squad",
                description: "Deterministic production-container boundary probe."));
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
