using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
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
    public const string CastIntentEndpoint = "/casting/intent";
    public const string CastReviewEndpoint = "/casting/review";
    public const string CastActivationEndpoint = "/casting/activate";
    public const string CastTitleEndpoint = "/casting/title";
    private const string AppTitleFile = "daydream-title.txt";
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
        bool useTestAgent = configureAgent is not null;
        builder.WebHost.UseStaticWebAssets();
        builder.AddServiceDefaults();
        builder.Services.AddAGUIServer();
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(SquadAgentDiagnostics.ActivitySourceName));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<WorkLifecycle>();
        builder.Services.AddHostedService<WorkLifecycleShutdownService>();
        builder.Services.AddSingleton<WorkEvidenceStore>();
        builder.Services.AddSingleton<WorkWaveStore>();
        builder.Services.AddHttpClient(GitHubPublisher.HttpClientName);
        var onlineHosting = new OnlineHostingSettings(
            builder.Configuration["DAYDREAM_HOSTING"] == "cluster");
        builder.Services.AddSingleton(onlineHosting);
        builder.Services.AddSingleton<IOnlineCluster, OnlineCluster>();
        builder.Services.AddSingleton(new AppIdentity(AppLibrary.FirstAppId));
        if (useTestAgent)
        {
            builder.Services.AddSingleton<IFixtureValidationRunner, LocalFixtureValidationRunner>();
        }
        else if (onlineHosting.Available)
        {
            builder.Services.AddSingleton<IFixtureValidationJobClient, FixtureValidationJobClient>();
            builder.Services.AddSingleton(serviceProvider =>
                new FixtureValidationSettings(
                    builder.Configuration["SQUAD_WORKER_IMAGE"] ?? string.Empty,
                    Path.GetFullPath("/workspace"),
                    builder.Configuration["SQUAD_WORKSPACE_CLAIM"] ?? "daydream-workspace",
                    builder.Configuration["SQUAD_WORKER_IMAGE_PULL_SECRET"] is { Length: > 0 } pullSecret
                        ? pullSecret
                        : null));
            builder.Services.AddSingleton<IFixtureValidationRunner>(serviceProvider =>
                new KubernetesFixtureValidationRunner(
                    serviceProvider.GetRequiredService<IFixtureValidationJobClient>(),
                    serviceProvider.GetRequiredService<FixtureValidationSettings>(),
                    serviceProvider.GetRequiredService<ILogger<KubernetesFixtureValidationRunner>>()));
        }
        else
        {
            builder.Services.AddSingleton<IFixtureValidationRunner, UnavailableFixtureValidationRunner>();
        }
        builder.Services.AddSingleton<AppOnlineService>();
        builder.Services.AddHttpClient(OnlineEndpoints.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false, UseProxy = false
            });
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        bool useContainerProbeAgent =
            builder.Configuration.GetValue<bool>(ContainerProbeModeKey);
        builder.Services.AddSingleton(new SecretRedactor(
            builder.Configuration[CopilotTokenKey], builder.Configuration[GitHubTokenKey]));
        builder.Services.AddSingleton(new ProductionReadinessSettings(
            builder.Configuration.GetValue(
                "SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY",
                !useTestAgent)));
        RegisterFixtureWorkspace(builder, allowTemporaryPath: useTestAgent);
        builder.Services.AddSingleton<AppDeploymentStateStore>();
        if (configureAgent is not null)
        {
            configureAgent(builder);
        }
        else if (useContainerProbeAgent)
        {
            RegisterContainerProbeAgent(builder);
        }
        else
        {
            RegisterSquadAgent(builder);
        }

        builder.Services.AddKeyedSingleton<AIAgent>("Daydream", (services, _) =>
            new WorkLifecycleAgent(
                services.GetRequiredService<AIAgent>(),
                services.GetRequiredService<IHttpContextAccessor>(),
                services.GetRequiredService<WorkEvidenceStore>(),
                services.GetService<WorkEvidenceCollector>(),
                services.GetRequiredService<SecretRedactor>(),
                services.GetRequiredService<ILogger<WorkLifecycleAgent>>(),
                services.GetRequiredService<WorkWaveStore>(),
                services.GetService<OriginalIntentStore>()));
        ServiceDescriptor agentRegistration = builder.Services.Last(descriptor =>
            descriptor.ServiceType == typeof(AIAgent) && !descriptor.IsKeyedService);
        builder.Services.AddSingleton(services => new AppLibrary(services, agentRegistration));
        builder.Services.AddHostedService<AppLibraryShutdownService>();

        WebApplication app = builder.Build();
        int? previewPort = builder.Configuration.GetValue<int?>(PreviewPortKey);
        // Select before routing, so the existing endpoints resolve only this app's services.
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/apps", out PathString remainder) ||
                remainder.Value is not { Length: > 1 } value)
            {
                await next(context);
                return;
            }

            string id = value[1..].Split('/')[0];
            AppLibrary library = app.Services.GetRequiredService<AppLibrary>();
            IServiceProvider? services = library.Find(id);
            if (services is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            string prefix = "/apps/" + id;
            IServiceProvider originalServices = context.RequestServices;
            PathString originalPath = context.Request.Path;
            PathString originalBase = context.Request.PathBase;
            context.RequestServices = new AppRequestServices(services, originalServices);
            context.Request.Path = value.Length == id.Length + 1 ? "/" : value[(id.Length + 1)..];
            context.Request.PathBase = originalBase.Add(prefix);
            try
            {
                await next(context);
            }
            finally
            {
                context.RequestServices = originalServices;
                context.Request.Path = originalPath;
                context.Request.PathBase = originalBase;
            }
        });
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
            if (useTestAgent || builder.Configuration.GetValue<bool>("SQUAD_INITIALIZE_WORKSPACE_ON_START"))
            {
                // Keep the person's squad and apps across restarts; only start fresh when there's nothing to resume.
                if (!fixtureWorkspace.TryResume())
                {
                    fixtureWorkspace.Reset();
                }
            }
            else if (!fixtureWorkspace.TryResume() ||
                !Directory.Exists(fixtureWorkspace.PathPolicy.ResolveWorkspacePath(".squad")))
            {
                fixtureWorkspace.ValidateInitialized();
            }

            app.Services.GetRequiredService<WorkWaveStore>()
                .UseStorage(fixtureWorkspace.StatePath("daydream-waves.json"));
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
        app.Use(async (context, next) =>
        {
            if ((context.Request.Path == "/health/live" || context.Request.Path == "/health/ready") &&
                context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
            {
                statusCodePages.Enabled = false;
            }

            await next(context);
        });
        app.UseRouting();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapDefaultEndpoints();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.MapGet("/apps", (AppLibrary library) => Results.Ok(library.List()));
        app.MapOnlineEndpoints();
        app.MapAppDeploymentEndpoints();
        app.MapPost("/apps", (AppLibrary library) => Results.Ok(library.Create()));
        app.MapPost("/select", (HttpContext context) => Results.Ok(new { id = context.Request.PathBase.Value?.Split('/').Last() ?? AppLibrary.FirstAppId }));
        app.MapAGUIServer(AgentEndpoint, new AppRoutingAgent(
            app.Services.GetRequiredKeyedService<AIAgent>("Daydream"),
            app.Services.GetRequiredService<IHttpContextAccessor>()))
            .AddEndpointFilter(async (context, next) =>
            {
                WorkLifecycle lifecycle = context.HttpContext.RequestServices.GetRequiredService<WorkLifecycle>();
                WorkEvidenceStore evidence = context.HttpContext.RequestServices.GetRequiredService<WorkEvidenceStore>();
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
        app.MapGet(WorkStatusEndpoint, (WorkLifecycle lifecycle) => Results.Ok(lifecycle.GetSnapshot()));
        app.MapGet(WavesEndpoint, (WorkWaveStore waves) => Results.Ok(waves.List()));
        app.MapGet(
            WavesEndpoint + "/{number:int}",
            (int number, WorkWaveStore waves) => waves.Get(number) is { } wave ? Results.Ok(wave) : Results.NotFound());
        app.MapPost(WorkCancellationEndpoint, (WorkLifecycle lifecycle) => Results.Ok(lifecycle.Cancel()));
        app.MapGet(
            WorkEvidenceEndpoint,
            (long? after, WorkEvidenceStore evidence) => Results.Ok(evidence.GetSnapshot(after ?? 0)));
        if (fixtureWorkspace is not null)
        {
            app.MapGet(CastIntentEndpoint, (OriginalIntentStore intent, HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                try
                {
                    return Results.Ok(intent.Get());
                }
                catch (OriginalIntentException exception)
                {
                    return Results.Json(new { message = exception.Message }, statusCode: exception.StatusCode);
                }
            });
            app.MapGet(
                CastStatusEndpoint,
                (CastReviewService review, DisposableFixtureWorkspace workspace, HttpContext context) =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    CastStatus result = review.GetStatus();
                    return Results.Ok(new
                    {
                        result.State,
                        result.HasCast,
                        result.CanTakeRequests,
                        generation = workspace.Generation,
                        hostingMode = context.RequestServices.GetRequiredService<OnlineHostingSettings>().Mode,
                        appTitle = ReadAppTitle(workspace)
                    });
                });
            app.MapPut(CastTitleEndpoint, (AppTitleEdit edit, DisposableFixtureWorkspace workspace) =>
            {
                string title = (edit.Title ?? string.Empty).Trim();
                if (title.Length is 0 or > 40 || title.Any(char.IsControl))
                {
                    return Results.BadRequest(new { message = "Give the app a name of up to 40 characters." });
                }

                File.WriteAllText(workspace.StatePath(AppTitleFile), title);
                return Results.Ok(new { appTitle = title });
            });
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
                (CastReviewService review, WorkLifecycle lifecycle) =>
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
                (DisposableFixtureWorkspace workspace) => Results.Ok(workspace.GetDiagnostics()));
            MapArtifactEndpoints(app, previewPort, builder.Configuration[PreviewUrlKey]);
        }

        return app;
    }

    private static void MapArtifactEndpoints(
        WebApplication app,
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
            await artifacts.WriteZipAsync(context.Response.Body, context.RequestAborted);
        });
        app.MapGet("/workspace/preview", (WorkspaceArtifacts artifacts, HttpContext context) =>
        {
            string? root = artifacts.FindPreviewRoot();
            return Results.Ok(new
            {
                enabled = previewPort is not null,
                available = previewPort is not null && root is not null,
                root,
                port = previewPort,
                url = previewUrl is null ? null : previewUrl.TrimEnd('/') + context.Request.PathBase
            });
        });
        app.MapGet("/publish/owners", (GitHubPublisher publisher, CancellationToken cancellationToken) =>
            publisher.GetOwnersAsync(cancellationToken));
        app.MapPost("/publish", async (
            PublishRequest request,
            GitHubPublisher publisher,
            WorkLifecycle lifecycle,
            CancellationToken cancellationToken) =>
        {
            WorkLifecycle.WorkLease? reservation = lifecycle.TryBegin("publish");
            if (reservation is null)
            {
                return Results.Conflict(new PublishResult(
                    false, null, null, true,
                    "Wait for the active work or publication to finish before publishing."));
            }

            try
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, reservation.CancellationToken);
                PublishResult result = await publisher.PublishAsync(request, cancellation.Token);
                return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
            }
            finally
            {
                reservation.Complete(WorkTerminalResult.Completed, "Publication finished.");
            }
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
        context.Response.Headers.ContentSecurityPolicy = OnlineEndpoints.SandboxPolicy;
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
        builder.Services.AddSingleton<OriginalIntentStore>();
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

    private static void RegisterSquadAgent(WebApplicationBuilder builder)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        builder.Services.AddSingleton(new SecretRedactor(copilotToken, builder.Configuration[GitHubTokenKey]));
        builder.Services.AddKeyedSingleton<AgentSessionStore>(
            "Squad",
            (_, _) => new InMemoryAgentSessionStore());
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
                    serviceProvider.GetRequiredService<WorkEvidenceStore>(),
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

    private static void RegisterContainerProbeAgent(WebApplicationBuilder builder)
    {
        string copilotToken = RequireValue(builder.Configuration, CopilotTokenKey);
        builder.Services.AddSingleton(new SecretRedactor(copilotToken, builder.Configuration[GitHubTokenKey]));
        builder.Services.AddSingleton<AIAgent>(serviceProvider =>
            new ContainerProbeChatClient(
                serviceProvider.GetRequiredService<DisposableFixtureWorkspace>(),
                serviceProvider.GetRequiredService<WorkEvidenceStore>(),
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

    private static string? ReadAppTitle(DisposableFixtureWorkspace workspace)
    {
        string path = workspace.StatePath(AppTitleFile);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

public sealed record AppTitleEdit(string? Title);
