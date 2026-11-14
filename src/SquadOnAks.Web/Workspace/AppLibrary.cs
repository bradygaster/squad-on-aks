using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public sealed record AppDeletion(bool Deleted, string Message, IReadOnlyList<PublishedRepository> Repositories);

public sealed record LibraryApp(string Id, string? Title, string State, bool HasCast, bool PreviewAvailable, string? RuntimeState = null,
    bool Imported = false, bool Deployable = true);

/// <summary>Each app owns a long-lived service container; selecting never redirects another app's work.</summary>
public sealed class AppLibrary(IServiceProvider rootServices, ServiceDescriptor agentRegistration) : IAsyncDisposable
{
    public const string FirstAppId = "first";
    private readonly object gate = new();
    private readonly Dictionary<string, ServiceProvider> apps = new(StringComparer.Ordinal);
    private string RegistryPath => rootServices.GetRequiredService<DisposableFixtureWorkspace>()
        .StatePath("soaks-library.json");
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

    /// <summary>
    /// Removes the app from the cluster (its namespace and everything in it), then its team, waves and files.
    /// GitHub repositories it published are not touched; they are returned so the person knows they remain.
    /// </summary>
    /// <param name="deferCleanup">Runs the release of the app's services and files once the caller no longer
    /// resolves anything from them (the request deleting the app still does until its response is written).</param>
    public async Task<AppDeletion?> DeleteAsync(string id, Action<Func<Task>> deferCleanup, CancellationToken cancellationToken)
    {
        if (id == FirstAppId) return new(false, "This app can’t be deleted.", []);
        if (Find(id) is not ServiceProvider services) return null;
        WorkLifecycle.WorkLease? lease = services.GetRequiredService<WorkLifecycle>().TryBegin("delete");
        if (lease is null) return new(false, "Stop the app’s current work before deleting it.", []);
        IReadOnlyList<PublishedRepository> repositories = services.GetRequiredService<GitHubPublisher>().ListPublished();
        try
        {
            await services.GetRequiredService<IAppNamespaceProvisioner>().DeleteAsync(id, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or IOException)
        {
            rootServices.GetRequiredService<ILogger<AppLibrary>>().LogError(exception, "Could not remove app {AppId} from the cluster.", id);
            // Nothing was removed, so the app returns to the state it was in.
            lease.Complete(WorkTerminalResult.Completed, "The app could not be removed from the cluster.");
            return new(false, "The app couldn’t be removed from the cluster, so nothing was deleted. Please try again.", repositories);
        }

        string root = services.GetRequiredService<DisposableFixtureWorkspace>().RootPath;
        lock (gate)
        {
            List<string> ids = [.. LoadIds().Where(saved => saved != id)];
            string temporary = RegistryPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(ids));
            File.Move(temporary, RegistryPath, overwrite: true);
            savedIds = ids;
            apps.Remove(id);
        }
        lease.Complete(WorkTerminalResult.Completed, "The app was deleted.");
        ILogger<AppLibrary> log = rootServices.GetRequiredService<ILogger<AppLibrary>>();
        deferCleanup(() =>
        {
            // Release the app's team session and files off the request path, bounded, so a slow or stuck
            // shutdown can never hold a server thread.
            _ = Task.Run(async () =>
            {
                Task disposal = services.DisposeAsync().AsTask();
                if (await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(30))) != disposal)
                    log.LogWarning("App {AppId} services did not stop within 30 seconds.", id);
                try { Directory.Delete(root, recursive: true); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The app is already gone from the library; a leftover folder is only disk space.
                    log.LogWarning(exception, "App {AppId} files were not fully removed.", id);
                }
            });
            return Task.CompletedTask;
        });
        return new(true, "The app was deleted.", repositories);
    }

    /// <summary>
    /// Starts an app from an existing GitHub repository. A Squad team already in the repository is used as it is
    /// (activated, so waves can start at once); otherwise the app is cast like a new one.
    /// </summary>
    public async Task<LibraryApp> ImportAsync(string owner, string name, CancellationToken cancellationToken)
    {
        GitHubRepositories github = rootServices.GetRequiredService<GitHubRepositories>();
        (GitHubRepositoryDetail detail, string cloneUrl) = await github.GetAsync(owner, name, cancellationToken);
        string id = Guid.NewGuid().ToString("N");
        DisposableFixtureWorkspace workspace = NewWorkspace(id);
        string[] parts = detail.FullName.Split('/', 2);
        await workspace.ImportAsync(new ImportedRepository(parts[0], parts[1], detail.FullName, cloneUrl, detail.HtmlUrl,
            detail.DefaultBranch, "squad/" + id[..8], detail.HasSquad), github.Token, cancellationToken);
        File.WriteAllText(workspace.StatePath("soaks-title.txt"), detail.FullName);
        lock (gate)
        {
            ServiceProvider services = CreateRuntime(id, workspace);
            try
            {
                CastReviewService cast = services.GetRequiredService<CastReviewService>();
                if (cast.GetStatus().HasCast)
                {
                    CastActivationResult activation = cast.Activate();
                    services.GetRequiredService<WorkLifecycle>().SetCastingState(activation.State);
                }
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

    private DisposableFixtureWorkspace NewWorkspace(string id)
    {
        DisposableFixtureWorkspace first = rootServices.GetRequiredService<DisposableFixtureWorkspace>();
        IWebHostEnvironment environment = rootServices.GetRequiredService<IWebHostEnvironment>();
        string source = Path.Combine(environment.ContentRootPath, "FixtureSource", first.Template);
        if (!Directory.Exists(source))
            source = Path.Combine(environment.ContentRootPath, "src", "SquadOnAks.Web", "FixtureSource", first.Template);
        return new DisposableFixtureWorkspace(source,
            first.StatePath(Path.Combine("soaks-apps", id)),
            rootServices.GetRequiredService<ILogger<DisposableFixtureWorkspace>>());
    }

    private ServiceProvider CreateRuntime(string id, bool create)
    {
        DisposableFixtureWorkspace workspace = NewWorkspace(id);
        if (create) workspace.Reset();
        else if (!workspace.TryResume())
            throw new InvalidOperationException("A saved app could not be reopened.");
        return CreateRuntime(id, workspace);
    }

    private ServiceProvider CreateRuntime(string id, DisposableFixtureWorkspace workspace)
    {

        var services = new ServiceCollection();
        services.AddSingleton(rootServices.GetRequiredService<ILoggerFactory>());
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(rootServices.GetRequiredService<IHttpContextAccessor>());
        services.AddSingleton(rootServices.GetRequiredService<IHttpClientFactory>());
        services.AddSingleton(rootServices.GetRequiredService<SecretRedactor>());
        services.AddSingleton(rootServices.GetRequiredService<IFixtureValidationRunner>());
        services.AddSingleton(rootServices.GetRequiredService<AppBuildCapabilities>());
        services.AddSingleton(rootServices.GetRequiredService<IAppRuntimeHost>());
        services.AddSingleton(rootServices.GetRequiredService<IAppBrowserAcceptance>());
        services.AddSingleton(rootServices.GetRequiredService<WorkerDependencyCatalog>());
        services.AddSingleton(rootServices.GetRequiredService<GitHubPublisherSettings>());
        services.AddSingleton(workspace);
        services.AddSingleton<WorkLifecycle>();
        services.AddSingleton<WorkEvidenceStore>();
        services.AddSingleton<WorkWaveStore>();
        services.AddSingleton<CastReviewService>();
        services.AddSingleton<OriginalIntentStore>();
        services.AddSingleton<WorkEvidenceCollector>();
        services.AddSingleton<AppBuildPolicy>();
        services.AddSingleton<ManagedAppRuntime>();
        services.AddSingleton<WorkspaceArtifacts>();
        services.AddSingleton(new AppIdentity(id));
        services.AddSingleton<AppDeploymentStateStore>();
        services.AddSingleton(rootServices.GetService<IAppWorkloadStatusReader>() ?? new UnavailableAppWorkloadStatusReader());
        services.AddSingleton<AppTopologyService>();
        services.AddSingleton(rootServices.GetRequiredService<AppDeploymentSettings>());
        services.AddSingleton(rootServices.GetRequiredService<IAppDeploymentJobClient>());
        services.AddSingleton(rootServices.GetRequiredService<IAppNamespaceProvisioner>());
        services.AddSingleton<KubernetesAppDeploymentRunner>();
        services.AddSingleton(rootServices.GetRequiredService<OnlineHostingSettings>());
        services.AddSingleton(rootServices.GetRequiredService<IOnlineCluster>());
        services.AddSingleton(rootServices.GetRequiredService<IDeployedAppReadinessProbe>());
        services.AddSingleton<AppOnlineService>();
        services.AddSingleton<GitHubPublisher>();
        ((IServiceCollection)services).Add(agentRegistration);
        services.AddKeyedSingleton<AgentSessionStore>("Squad", (_, _) => new InMemoryAgentSessionStore());
        services.AddKeyedSingleton<AIAgent>("App", (provider, _) =>
            new WorkLifecycleAgent(
                provider.GetRequiredService<AIAgent>(),
                provider.GetRequiredService<IHttpContextAccessor>(),
                provider.GetRequiredService<WorkEvidenceStore>(),
                provider.GetRequiredService<WorkEvidenceCollector>(),
                provider.GetRequiredService<SecretRedactor>(),
                provider.GetRequiredService<ILogger<WorkLifecycleAgent>>(),
                provider.GetRequiredService<WorkWaveStore>(),
                provider.GetRequiredService<OriginalIntentStore>(),
                provider.GetRequiredService<AppBuildPolicy>(),
                provider.GetRequiredService<WorkerDependencyCatalog>(),
                provider.GetRequiredService<AppDeploymentStateStore>()));
        ServiceProvider runtime = services.BuildServiceProvider();
        runtime.GetRequiredService<WorkWaveStore>().UseStorage(workspace.StatePath("soaks-waves.json"));
        runtime.GetRequiredService<WorkLifecycle>().SetCastingState(runtime.GetRequiredService<CastReviewService>().GetStatus().State);
        return runtime;
    }

    private static LibraryApp Describe(string id, IServiceProvider services)
    {
        DisposableFixtureWorkspace workspace = services.GetRequiredService<DisposableFixtureWorkspace>();
        string path = workspace.StatePath("soaks-title.txt");
        CastStatus cast = services.GetRequiredService<CastReviewService>().GetStatus();
        WorkspaceArtifacts artifacts = services.GetRequiredService<WorkspaceArtifacts>();
        bool preview;
        string state = services.GetRequiredService<WorkLifecycle>().GetSnapshot().State;
        try { preview = !artifacts.IsServerBacked() && artifacts.FindPreviewRoot() is not null; }
        catch (InvalidOperationException)
        {
            services.GetRequiredService<ILogger<AppLibrary>>().LogWarning("App {AppId} has an invalid build contract.", id);
            // The wave that produced it already reports "needs attention"; keep the real work state
            // (a fix wave may be running) instead of calling the whole app failed.
            preview = false;
        }
        return new(id, workspace.Imported?.FullName ?? (File.Exists(path) ? File.ReadAllText(path).Trim() : null),
            state, cast.HasCast, preview, services.GetRequiredService<ManagedAppRuntime>().GetStatus().State,
            Imported: workspace.Imported is not null, Deployable: !workspace.IsPlainRepository);
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
