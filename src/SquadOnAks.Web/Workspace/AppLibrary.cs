using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public sealed record LibraryApp(string Id, string? Title, string State, bool HasCast, bool PreviewAvailable);

/// <summary>Each app owns a long-lived service container; selecting never redirects another app's work.</summary>
public sealed class AppLibrary(IServiceProvider rootServices, ServiceDescriptor agentRegistration) : IAsyncDisposable
{
    public const string FirstAppId = "first";
    private readonly object gate = new();
    private readonly Dictionary<string, ServiceProvider> apps = new(StringComparer.Ordinal);
    private string RegistryPath => rootServices.GetRequiredService<DisposableFixtureWorkspace>()
        .StatePath("daydream-library.json");
    private List<string>? savedIds;

    private List<string> LoadIds() => savedIds ??= File.Exists(RegistryPath)
        ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RegistryPath))
            ?? throw new InvalidOperationException("The saved app library is unreadable.")
        : [];

    public IReadOnlyList<LibraryApp> List()
    {
        lock (gate)
        {
            return new[] { FirstAppId }.Concat(LoadIds()).Select(id => Describe(id, Find(id)!)).ToArray();
        }
    }

    public LibraryApp Create()
    {
        lock (gate)
        {
            string id = Guid.NewGuid().ToString("N");
            ServiceProvider services = CreateRuntime(id, create: true);
            try
            {
                List<string> ids = [.. LoadIds(), id];
                string temporary = RegistryPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(ids));
                File.Move(temporary, RegistryPath, overwrite: true);
                savedIds = ids;
                apps.Add(id, services);
                return Describe(id, services);
            }
            catch
            {
                services.Dispose();
                throw;
            }
        }
    }

    public IServiceProvider? Find(string id)
    {
        lock (gate)
        {
            if (id == FirstAppId) return rootServices;
            if (!Guid.TryParseExact(id, "N", out _) || !LoadIds().Contains(id, StringComparer.Ordinal))
                return null;
            if (!apps.TryGetValue(id, out ServiceProvider? services))
            {
                services = CreateRuntime(id, create: false);
                apps.Add(id, services);
            }
            return services;
        }
    }

    private ServiceProvider CreateRuntime(string id, bool create)
    {
        DisposableFixtureWorkspace first = rootServices.GetRequiredService<DisposableFixtureWorkspace>();
        IWebHostEnvironment environment = rootServices.GetRequiredService<IWebHostEnvironment>();
        string source = Path.Combine(environment.ContentRootPath, "FixtureSource", first.Template);
        if (!Directory.Exists(source))
            source = Path.Combine(environment.ContentRootPath, "src", "SquadOnAks.Web", "FixtureSource", first.Template);
        var workspace = new DisposableFixtureWorkspace(source,
            first.StatePath(Path.Combine("daydream-apps", id)),
            rootServices.GetRequiredService<ILogger<DisposableFixtureWorkspace>>());
        if (create) workspace.Reset();
        else if (!workspace.TryResume())
            throw new InvalidOperationException("A saved app could not be reopened.");

        var services = new ServiceCollection();
        services.AddSingleton(rootServices.GetRequiredService<ILoggerFactory>());
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(rootServices.GetRequiredService<IHttpContextAccessor>());
        services.AddSingleton(rootServices.GetRequiredService<IHttpClientFactory>());
        services.AddSingleton(rootServices.GetRequiredService<SecretRedactor>());
        services.AddSingleton(rootServices.GetRequiredService<IFixtureValidationRunner>());
        services.AddSingleton(rootServices.GetRequiredService<GitHubPublisherSettings>());
        services.AddSingleton(workspace);
        services.AddSingleton<WorkLifecycle>();
        services.AddSingleton<WorkEvidenceStore>();
        services.AddSingleton<WorkWaveStore>();
        services.AddSingleton<CastReviewService>();
        services.AddSingleton<OriginalIntentStore>();
        services.AddSingleton<WorkEvidenceCollector>();
        services.AddSingleton<WorkspaceArtifacts>();
        services.AddSingleton(new AppIdentity(id));
        services.AddSingleton<AppDeploymentStateStore>();
        services.AddSingleton(rootServices.GetRequiredService<OnlineHostingSettings>());
        services.AddSingleton(rootServices.GetRequiredService<IOnlineCluster>());
        services.AddSingleton(rootServices.GetRequiredService<IDeployedAppReadinessProbe>());
        services.AddSingleton<AppOnlineService>();
        services.AddSingleton<GitHubPublisher>();
        ((IServiceCollection)services).Add(agentRegistration);
        services.AddKeyedSingleton<AgentSessionStore>("Squad", (_, _) => new InMemoryAgentSessionStore());
        services.AddKeyedSingleton<AIAgent>("Daydream", (provider, _) =>
            new WorkLifecycleAgent(
                provider.GetRequiredService<AIAgent>(),
                provider.GetRequiredService<IHttpContextAccessor>(),
                provider.GetRequiredService<WorkEvidenceStore>(),
                provider.GetRequiredService<WorkEvidenceCollector>(),
                provider.GetRequiredService<SecretRedactor>(),
                provider.GetRequiredService<ILogger<WorkLifecycleAgent>>(),
                provider.GetRequiredService<WorkWaveStore>(),
                provider.GetRequiredService<OriginalIntentStore>()));
        ServiceProvider runtime = services.BuildServiceProvider();
        runtime.GetRequiredService<WorkWaveStore>().UseStorage(workspace.StatePath("daydream-waves.json"));
        runtime.GetRequiredService<WorkLifecycle>().SetCastingState(runtime.GetRequiredService<CastReviewService>().GetStatus().State);
        return runtime;
    }

    private static LibraryApp Describe(string id, IServiceProvider services)
    {
        string path = services.GetRequiredService<DisposableFixtureWorkspace>().StatePath("daydream-title.txt");
        CastStatus cast = services.GetRequiredService<CastReviewService>().GetStatus();
        return new(id, File.Exists(path) ? File.ReadAllText(path).Trim() : null,
            services.GetRequiredService<WorkLifecycle>().GetSnapshot().State,
            cast.HasCast, services.GetRequiredService<WorkspaceArtifacts>().FindPreviewRoot() is not null);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        WorkLifecycle[] lifecycles;
        lock (gate)
        {
            lifecycles = apps.Values.Select(services => services.GetRequiredService<WorkLifecycle>()).ToArray();
            foreach (WorkLifecycle lifecycle in lifecycles) lifecycle.BeginShutdown();
        }
        foreach (WorkLifecycle lifecycle in lifecycles)
        {
            try { await lifecycle.WaitForIdleAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lifecycle.FailShutdown();
                rootServices.GetRequiredService<ILogger<AppLibrary>>()
                    .LogError("An app did not stop within the shutdown grace period.");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (ServiceProvider services in apps.Values) await services.DisposeAsync();
    }
}

public sealed class AppLibraryShutdownService(AppLibrary library) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => library.StopAsync(cancellationToken);
}

// Keep request-scoped ASP.NET services in the host, while app-owned services live for the app's lifetime.
internal sealed class AppRequestServices(IServiceProvider app, IServiceProvider request) : IServiceProvider, IKeyedServiceProvider
{
    public object? GetService(Type serviceType) => app.GetService(serviceType) ?? request.GetService(serviceType);

    public object? GetKeyedService(Type serviceType, object? serviceKey) =>
        app.GetKeyedService(serviceType, serviceKey) ?? request.GetKeyedService(serviceType, serviceKey);

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey) ??
        throw new InvalidOperationException($"No service for type '{serviceType}' and key '{serviceKey}' has been registered.");
}
