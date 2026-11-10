using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public sealed record AppRuntimeSettings(
    IReadOnlyDictionary<string, string> Images,
    string? PostgresImage,
    TimeSpan StartupTimeout);
public sealed record AppRuntimeLease(
    string AppId, string Container, string Network, string DataVolume, string? DatabaseContainer,
    string? DatabaseVolume, Uri Origin, string ExportPath, bool Disposable, string? GatewayContainer = null,
    string? SharedDatabaseContainer = null);
public sealed record AppRuntimeStatus(string State, string Message, string? Url = null, string? SourceCommit = null);
internal sealed record SavedAppRuntime(AppRuntimeLease Lease, string Commit);

public interface IAppRuntimeHost
{
    Task<AppRuntimeLease> StartAsync(DisposableFixtureWorkspace workspace, string appId, AppBuildManifest manifest,
        bool disposable, CancellationToken cancellationToken, AppRuntimeLease? previous = null);
    Task WaitReadyAsync(AppRuntimeLease lease, AppRuntimeSpec spec, CancellationToken cancellationToken);
    Task RestartAsync(AppRuntimeLease lease, CancellationToken cancellationToken);
    Task StopAsync(AppRuntimeLease lease, CancellationToken cancellationToken, bool preserveDatabase = false);
}

public sealed class UnavailableAppRuntimeHost : IAppRuntimeHost
{
    private static InvalidOperationException Unavailable() =>
        new("Managed application runtime is not configured. A static preview is not a running server-backed app.");
    public Task<AppRuntimeLease> StartAsync(DisposableFixtureWorkspace workspace, string appId, AppBuildManifest manifest,
        bool disposable, CancellationToken cancellationToken, AppRuntimeLease? previous = null) => throw Unavailable();
    public Task WaitReadyAsync(AppRuntimeLease lease, AppRuntimeSpec spec, CancellationToken cancellationToken) => throw Unavailable();
    public Task RestartAsync(AppRuntimeLease lease, CancellationToken cancellationToken) => throw Unavailable();
    public Task StopAsync(AppRuntimeLease lease, CancellationToken cancellationToken, bool preserveDatabase = false) => throw Unavailable();
}

public sealed class DockerAppRuntimeHost(
    AppRuntimeSettings settings, IHttpClientFactory clients, ILogger<DockerAppRuntimeHost> logger,
    ILoggerFactory loggers) : IAppRuntimeHost
{
    private const string GatewayScript = """
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
        from http.client import HTTPConnection
        class Gateway(BaseHTTPRequestHandler):
            def forward(self):
                size = int(self.headers.get("Content-Length", "0"))
                if size < 0 or size > 1048576:
                    self.send_error(413)
                    return
                connection = HTTPConnection("application", 8080, timeout=10)
                try:
                    headers = {k:v for k,v in self.headers.items() if k.lower() not in ("host", "connection", "transfer-encoding")}
                    headers["Host"] = self.headers.get("Host", "localhost")
                    connection.request(self.command, self.path, self.rfile.read(size), headers)
                    response = connection.getresponse()
                    body = response.read(16777217)
                    if len(body) > 16777216:
                        self.send_error(502)
                        return
                    self.send_response(response.status)
                    for key,value in response.getheaders():
                        if key.lower() not in ("connection", "transfer-encoding", "content-length"):
                            self.send_header(key,value)
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    if self.command != "HEAD": self.wfile.write(body)
                except (OSError, TimeoutError):
                    self.send_error(503)
                finally: connection.close()
            do_GET = do_POST = do_HEAD = do_PUT = do_PATCH = do_DELETE = do_OPTIONS = forward
        ThreadingHTTPServer(("0.0.0.0", 8080), Gateway).serve_forever()
        """;
    public const string OwnerLabel = "daydream.runtime.owner";
    public const string AppLabel = "daydream.runtime.app-id";
    public const string HttpClientName = "DaydreamManagedRuntime";

    public async Task<AppRuntimeLease> StartAsync(
        DisposableFixtureWorkspace workspace, string appId, AppBuildManifest manifest,
        bool disposable, CancellationToken cancellationToken, AppRuntimeLease? previous = null)
    {
        if (!StaticAppManifest.ValidId(appId))
            throw new InvalidOperationException("Invalid managed runtime app identity.");
        if (manifest.Runtime is not { } spec ||
            !settings.Images.TryGetValue(manifest.ValidationProfile, out string? image))
            throw new InvalidOperationException("This app's managed runtime profile is not installed.");
        _ = new DockerFixtureValidationRunner(image, loggers.CreateLogger<DockerFixtureValidationRunner>());
        if (spec.Database == "postgres")
            _ = new DockerFixtureValidationRunner(settings.PostgresImage
                ?? throw new InvalidOperationException("The managed PostgreSQL runtime image is not installed."),
                loggers.CreateLogger<DockerFixtureValidationRunner>());

        string run = $"daydream-runtime-{Guid.NewGuid():N}";
        string data = disposable ? run + "-data" : $"daydream-runtime-{appId}-data";
        string? dbData = spec.Database == "postgres"
            ? disposable ? run + "-pgdata" : $"daydream-runtime-{appId}-pgdata"
            : null;
        string export = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, run));
        string relative = Path.GetRelativePath(workspace.RootPath, export);
        if (relative == "." || !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime source snapshots must be outside the application workspace.");
        var lease = new AppRuntimeLease(appId, run, run + "-network", data,
            dbData is null ? null : run + "-database", dbData, new Uri("http://127.0.0.1/"), export, disposable,
            run + "-gateway");
        if (!disposable && dbData is not null && previous is not null)
        {
            ValidateLease(previous);
            if (previous.AppId != appId || previous.Disposable || previous.DatabaseVolume != dbData)
                throw new InvalidOperationException("Database reuse requires the same application's persistent database.");
            string? database = previous.DatabaseContainer ?? previous.SharedDatabaseContainer;
            if (database is not null)
            {
                await RequireOwnedContainerAsync(database, appId, cancellationToken);
                lease = lease with { DatabaseContainer = null, SharedDatabaseContainer = database };
            }
        }
        try
        {
            Directory.CreateDirectory(export);
            string archivePath = Path.Combine(export, "source.zip");
            await using (FileStream archive = File.Create(archivePath))
                await new WorkspaceArtifacts(workspace).WriteZipAsync(archive, cancellationToken);
            string snapshot = Path.Combine(export, "source");
            await ZipFile.ExtractToDirectoryAsync(archivePath, snapshot, cancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                foreach (string directory in Directory.EnumerateDirectories(export, "*", SearchOption.AllDirectories).Prepend(export))
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                foreach (string file in Directory.EnumerateFiles(snapshot, "*", SearchOption.AllDirectories))
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            await ExecuteAsync(["network", "create", "--internal", "--label", $"{OwnerLabel}={appId}",
                "--label", $"{AppLabel}={appId}", lease.Network], cancellationToken);
            await EnsureVolumeAsync(data, appId, 1654, image, cancellationToken);
            var env = new Dictionary<string, string>();
            if (dbData is not null)
            {
                string password = disposable ? NewPassword() : ReadOrCreatePassword(workspace);
                await EnsureVolumeAsync(dbData, appId, 999, image, cancellationToken);
                if (lease.SharedDatabaseContainer is not null)
                    await ExecuteAsync(["network", "connect", "--alias", "database", lease.Network, lease.SharedDatabaseContainer], cancellationToken);
                else await ExecuteAsync(["run", "--detach", "--name", lease.DatabaseContainer!,
                    "--label", $"{OwnerLabel}={appId}", "--label", $"{AppLabel}={appId}",
                    "--pull", "never", "--network", lease.Network, "--network-alias", "database",
                    "--read-only", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                    "--user", "999:999", "--pids-limit", "128", "--memory", "512m", "--cpus", "1",
                    "--log-driver", "local", "--log-opt", "max-size=5m", "--log-opt", "max-file=2",
                    "--tmpfs", "/tmp:rw,nosuid,nodev,size=64m",
                    "--tmpfs", "/var/run/postgresql:rw,nosuid,nodev,size=16m,uid=999,gid=999",
                    "--mount", $"type=volume,source={dbData},target=/data",
                    "--env", "PGDATA=/data/pgdata", "--env", "POSTGRES_DB=application",
                    "--env", "POSTGRES_USER=application", "--env", "POSTGRES_PASSWORD",
                    settings.PostgresImage!], cancellationToken,
                    new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = password });
                env["ConnectionStrings__database"] =
                    $"Host=database;Port=5432;Database=application;Username=application;Password={password}";
            }
            var args = new List<string>
            {
                "run", "--detach", "--name", lease.Container,
                "--label", $"{OwnerLabel}={appId}", "--label", $"{AppLabel}={appId}",
                "--pull", "never", "--network", lease.Network, "--read-only",
                "--network-alias", "application",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "1654:1654",
                "--pids-limit", "256", "--memory", "2g", "--cpus", "2",
                "--log-driver", "local", "--log-opt", "max-size=5m", "--log-opt", "max-file=2",
                "--tmpfs", "/tmp:rw,nosuid,nodev,size=1g,mode=1777",
                "--mount", $"type=bind,source={snapshot},target=/source,readonly",
                "--mount", $"type=volume,source={data},target=/data",
                "--workdir", "/tmp",
                "--env", "HOME=/tmp/home", "--env", "DOTNET_CLI_HOME=/tmp/home",
                "--env", "SQUAD_APP_DATA=/data", "--env", "ASPNETCORE_URLS=http://0.0.0.0:8080"
            };
            foreach (string key in env.Keys) args.AddRange(["--env", key]);
            args.AddRange(["--entrypoint", "sh", image, "-c",
                "set -eu; unset COPILOT_TOKEN GITHUB_TOKEN; mkdir -p /tmp/home /tmp/app; " +
                "cp -R /source/. /tmp/app/; cd /tmp/app; exec sh tests/serve.sh"]);
            await ExecuteAsync(args, cancellationToken, env);
            await ExecuteAsync(["run", "--detach", "--name", lease.GatewayContainer!,
                "--label", $"{OwnerLabel}={appId}", "--label", $"{AppLabel}={appId}",
                "--pull", "never", "--network", "bridge", "--read-only",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "1654:1654",
                "--pids-limit", "64", "--memory", "128m", "--cpus", "0.5",
                "--log-driver", "local", "--log-opt", "max-size=5m", "--log-opt", "max-file=2",
                "--tmpfs", "/tmp:rw,nosuid,nodev,size=16m", "--publish", "127.0.0.1::8080",
                "--entrypoint", "python3", image, "-c", GatewayScript], cancellationToken);
            await ExecuteAsync(["network", "connect", lease.Network, lease.GatewayContainer!], cancellationToken);
            string port = (await ExecuteAsync(["port", lease.GatewayContainer!, "8080/tcp"], cancellationToken)).Trim();
            if (!port.StartsWith("127.0.0.1:", StringComparison.Ordinal) ||
                !int.TryParse(port["127.0.0.1:".Length..], out int number) || number is < 1 or > 65535)
                throw new InvalidOperationException("The runtime did not receive a loopback application port.");
            return lease with { Origin = new Uri($"http://127.0.0.1:{number}/") };
        }
        catch
        {
            await StopAsync(lease, CancellationToken.None, preserveDatabase: lease.SharedDatabaseContainer is not null);
            throw;
        }
    }

    private async Task EnsureVolumeAsync(string name, string appId, int user, string image, CancellationToken cancellationToken)
    {
        FixtureValidationResult existing = await DockerFixtureValidationRunner.RunDockerAsync(
            ["volume", "inspect", "--format", "{{json .Labels}}", name], cancellationToken);
        if (existing.ExitCode == 0)
        {
            if (!Owned(existing.Output, appId))
                throw new InvalidOperationException("An existing runtime data volume belongs to another application.");
        }
        else
        {
            if (!existing.Output.Contains("no such volume", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Could not inspect the application's runtime volume.");
            await ExecuteAsync(["volume", "create", "--label", $"{OwnerLabel}={appId}",
                "--label", $"{AppLabel}={appId}", name], cancellationToken);
        }
        // Only this fixed trusted initializer is root. Generated code is never mounted or executed here.
        await ExecuteAsync(["run", "--rm", "--pull", "never", "--network", "none", "--read-only",
            "--cap-drop", "ALL", "--cap-add", "CHOWN", "--security-opt", "no-new-privileges", "--user", "0",
            "--mount", $"type=volume,source={name},target=/data", "--entrypoint", "sh", image,
            "-c", $"chown {user}:{user} /data"], cancellationToken);
    }

    public async Task WaitReadyAsync(AppRuntimeLease lease, AppRuntimeSpec spec, CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        await RequireOwnedContainerAsync(lease.Container, lease.AppId, cancellationToken);
        await RequireOwnedContainerAsync(lease.GatewayContainer!, lease.AppId, cancellationToken);
        if ((lease.DatabaseContainer ?? lease.SharedDatabaseContainer) is { } database)
            await RequireOwnedContainerAsync(database, lease.AppId, cancellationToken);
        string port = (await ExecuteAsync(["port", lease.GatewayContainer!, "8080/tcp"], cancellationToken)).Trim();
        if (port != $"127.0.0.1:{lease.Origin.Port}")
            throw new InvalidOperationException("The saved runtime URL does not match its owned container.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.StartupTimeout);
        using HttpClient client = clients.CreateClient(HttpClientName);
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                bool ready = true;
                foreach (string path in spec.HealthPaths)
                {
                    if (!AppBuildManifest.ValidHttpPath(path)) throw new InvalidOperationException("Unsafe readiness path.");
                    try
                    {
                        using HttpResponseMessage response = await client.GetAsync(
                            new Uri(lease.Origin, path), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                        ready &= response.StatusCode == HttpStatusCode.OK;
                    }
                    catch (HttpRequestException) { ready = false; }
                    catch (OperationCanceledException) when (!timeout.IsCancellationRequested) { ready = false; }
                }
                if (ready) return;
                await Task.Delay(500, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The app did not become ready. Check its real API/database health and service startup.");
        }
    }

    public async Task RestartAsync(AppRuntimeLease lease, CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        await RequireOwnedContainerAsync(lease.Container, lease.AppId, cancellationToken);
        await ExecuteAsync(["restart", "--time", "10", lease.Container], cancellationToken);
    }

    public async Task StopAsync(AppRuntimeLease lease, CancellationToken cancellationToken, bool preserveDatabase = false)
    {
        ValidateLease(lease);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(30));
        var failures = new List<Exception>();
        string? database = lease.DatabaseContainer ?? lease.SharedDatabaseContainer;
        if (preserveDatabase && database is not null)
        {
            await RequireOwnedContainerAsync(database, lease.AppId, bounded.Token);
            FixtureValidationResult network = await DockerFixtureValidationRunner.RunDockerAsync(
                ["network", "inspect", "--format", "{{json .Labels}}", lease.Network], bounded.Token);
            if (network.ExitCode == 0)
            {
                if (!Owned(network.Output, lease.AppId))
                    throw new InvalidOperationException("Database reuse refused an unowned network.");
                using JsonDocument connections = JsonDocument.Parse(await ExecuteAsync(
                    ["inspect", "--format", "{{json .NetworkSettings.Networks}}", database], bounded.Token));
                if (connections.RootElement.TryGetProperty(lease.Network, out _))
                    await ExecuteAsync(["network", "disconnect", "--force", lease.Network, database], bounded.Token);
            }
            else if (!network.Output.Contains("no such", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Could not inspect the database's owned runtime network.");
        }
        foreach ((string kind, string? name) in new[]
        {
            ("container", lease.GatewayContainer),
            ("container", (string?)lease.Container), ("container", preserveDatabase ? null : database),
            ("network", (string?)lease.Network),
            ("volume", lease.Disposable ? lease.DataVolume : null),
            ("volume", lease.Disposable ? lease.DatabaseVolume : null)
        })
        {
            if (name is null) continue;
            try
            {
                FixtureValidationResult inspect = await DockerFixtureValidationRunner.RunDockerAsync(
                    [kind, "inspect", "--format", kind == "container" ? "{{json .Config.Labels}}" : "{{json .Labels}}", name],
                    bounded.Token);
                if (inspect.ExitCode != 0)
                {
                    if (!inspect.Output.Contains("no such", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Could not inspect an owned runtime resource for cleanup.");
                    continue;
                }
                if (!Owned(inspect.Output, lease.AppId))
                    throw new InvalidOperationException("Runtime cleanup refused an unowned resource.");
                await ExecuteAsync(kind == "container" ? ["rm", "--force", name] : [kind, "rm", name], bounded.Token);
            }
            catch (Exception exception)
            {
                logger.LogError("Managed runtime resource {Resource} could not be cleaned up.", name);
                failures.Add(exception);
            }
        }
        if (failures.Count != 0)
            throw new AggregateException("Owned runtime cleanup is incomplete; retained resources require reconciliation.", failures);
        if (Directory.Exists(lease.ExportPath)) Directory.Delete(lease.ExportPath, recursive: true);
    }

    private static bool Owned(string labels, string? appId = null)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(labels);
        return values?.TryGetValue(OwnerLabel, out string? owner) == true &&
            values.TryGetValue(AppLabel, out string? app) && owner == app &&
            StaticAppManifest.ValidId(app) && (appId is null || app == appId);
    }

    private static async Task RequireOwnedContainerAsync(string name, string appId, CancellationToken cancellationToken)
    {
        FixtureValidationResult result = await DockerFixtureValidationRunner.RunDockerAsync(
            ["inspect", "--format", "{{json .Config.Labels}}", name], cancellationToken);
        if (result.ExitCode != 0 || !Owned(result.Output, appId))
            throw new InvalidOperationException("The managed runtime container is unavailable or no longer owned.");
    }

    private static async Task<string> ExecuteAsync(
        IEnumerable<string> args, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? env = null)
    {
        FixtureValidationResult result = await DockerFixtureValidationRunner.RunDockerAsync(args, cancellationToken, env);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"A managed runtime {args.First()} operation failed (exit {result.ExitCode}). Check the configured images and Docker runtime.",
                new IOException(result.Output));
        return result.Output;
    }

    private static string NewPassword() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    internal static void ValidateLease(AppRuntimeLease lease)
    {
        if (!StaticAppManifest.ValidId(lease.AppId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(lease.Container, @"\Adaydream-runtime-[a-f0-9]{32}\z") ||
            lease.Network != lease.Container + "-network" ||
            lease.DataVolume != (lease.Disposable ? lease.Container : $"daydream-runtime-{lease.AppId}") + "-data" ||
            lease.DatabaseContainer is not null && lease.DatabaseContainer != lease.Container + "-database" ||
            lease.SharedDatabaseContainer is not null && (lease.Disposable || lease.DatabaseContainer is not null ||
                lease.DatabaseVolume is null ||
                !System.Text.RegularExpressions.Regex.IsMatch(lease.SharedDatabaseContainer, @"\Adaydream-runtime-[a-f0-9]{32}-database\z")) ||
            lease.GatewayContainer != lease.Container + "-gateway" ||
            lease.DatabaseVolume is not null && lease.DatabaseVolume !=
                (lease.Disposable ? lease.Container : $"daydream-runtime-{lease.AppId}") + "-pgdata" ||
            lease.ExportPath != Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, lease.Container)) ||
            lease.Origin.Scheme != "http" || lease.Origin.Host != "127.0.0.1" ||
            lease.Origin.AbsolutePath != "/" || lease.Origin.UserInfo.Length != 0 ||
            lease.Origin.Query.Length != 0 || lease.Origin.Fragment.Length != 0)
            throw new InvalidOperationException("The saved runtime resource identity is invalid; no resources were changed.");
    }
    private static string ReadOrCreatePassword(DisposableFixtureWorkspace workspace)
    {
        string path = workspace.StatePath("daydream-runtime-postgres-secret");
        if (File.Exists(path))
        {
            string password = File.ReadAllText(path).Trim();
            if (password.Length != 64 || password.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidOperationException("The app's saved runtime credential is invalid; existing database data has been preserved.");
            return password;
        }
        string created = NewPassword();
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (FileStream file = new(path, options))
        using (var writer = new StreamWriter(file)) writer.Write(created);
        return created;
    }
}

public sealed class ManagedAppRuntime(
    DisposableFixtureWorkspace workspace, AppIdentity identity, IAppRuntimeHost host,
    IAppBrowserAcceptance browser, WorkEvidenceStore evidence, ILogger<ManagedAppRuntime> logger,
    WorkWaveStore? waves = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string StatePath => workspace.StatePath("daydream-runtime.json");
    private volatile AppRuntimeStatus status = new("NotChecked", "The running application has not been checked.");
    private DateTimeOffset observedAt;

    public AppRuntimeStatus GetStatus() => status;

    public async Task<AppRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (status.State != "NotChecked" && status.State != "Ready" ||
            status.State == "Ready" && DateTimeOffset.UtcNow - observedAt < TimeSpan.FromSeconds(5)) return status;
        await gate.WaitAsync(cancellationToken);
        try
        {
            SavedAppRuntime? saved = ReadSaved();
            if (saved is null) return status;
            WaveSummary? latest = waves?.List().LastOrDefault();
            if (latest is not null && (latest.State != "Completed" || latest.ValidationState == "Failed" ||
                latest.CommitSha is not null && latest.CommitSha != saved.Commit))
                return status = new("NotVerified", "The current wave has not passed running-app checks.");
            if (!await MatchesSourceAsync(saved.Commit, cancellationToken))
                return status = new("NotVerified", "The current source differs from the verified running app. Start a wave to check it.");
            AppBuildManifest? manifest = AppBuildManifest.Read(workspace);
            if (manifest?.Runtime is null) return status;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await host.WaitReadyAsync(saved.Lease, manifest.Runtime, timeout.Token);
                status = new("Ready", "The previously verified app is running.", saved.Lease.Origin.AbsoluteUri, saved.Commit);
                observedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception exception) when (exception is InvalidOperationException or TimeoutException ||
                exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("The saved runtime for app {AppId} is unavailable.", identity.Id);
                status = new("Unavailable", "The previously verified app is no longer running. Start another wave to rebuild it.");
            }
            return status;
        }
        finally { gate.Release(); }
    }

    public async Task VerifyAndStartAsync(AppBuildManifest manifest, string commit, CancellationToken cancellationToken)
    {
        if (manifest.Runtime is not { } spec) return;
        if (host is UnavailableAppRuntimeHost)
        {
            // Cluster mode: the host never runs generated apps; the deployment's health checks do.
            Invalidate("NotChecked", "Build checks passed. The running app is checked when it goes online.");
            return;
        }
        await gate.WaitAsync(cancellationToken);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromMinutes(5));
        CancellationToken token = bounded.Token;
        try
        {
            Update("Starting", "Starting an isolated test application.");
            AppRuntimeLease test = await host.StartAsync(workspace, identity.Id, manifest, disposable: true, token);
            try
            {
                Update("Readiness", "Checking the real application and its services.");
                await host.WaitReadyAsync(test, spec, token);
                Update("Browser", "Playwright is testing the generated application's user journeys.");
                await browser.CheckAsync(test.Origin, spec.Journeys, mobile: false, token);
                if (spec.RestartJourneys is { Count: > 0 })
                {
                    Update("Persistence", "Restarting the test application and checking saved data in a fresh browser.");
                    await host.RestartAsync(test, token);
                    await host.WaitReadyAsync(test, spec, token);
                    await browser.CheckAsync(test.Origin, spec.RestartJourneys, mobile: false, token);
                }
                await browser.CheckAsync(test.Origin, [], mobile: true, token);
            }
            finally { await host.StopAsync(test, CancellationToken.None); }

            SavedAppRuntime? old = ReadSaved();
            Update("Starting", "Starting the verified app with its own persistent data.");
            AppRuntimeLease live = await host.StartAsync(workspace, identity.Id, manifest, disposable: false, token, old?.Lease);
            try
            {
                await host.WaitReadyAsync(live, spec, token);
                if (old is not null) await host.StopAsync(old.Lease, CancellationToken.None,
                    preserveDatabase: live.SharedDatabaseContainer is not null);
                string temporary = StatePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(new SavedAppRuntime(live, commit)));
                File.Move(temporary, StatePath, overwrite: true);
            }
            catch
            {
                await host.StopAsync(live, CancellationToken.None, preserveDatabase: live.SharedDatabaseContainer is not null);
                throw;
            }
            status = new("Ready", "The running app passed readiness, browser and responsive checks.", live.Origin.AbsoluteUri, commit);
            observedAt = DateTimeOffset.UtcNow;
            evidence.RecordCheck("runtime", "Passed", status.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Invalidate("Failed", "Running-app checks exceeded their time limit.");
            throw new TimeoutException("Running-app acceptance exceeded its execution limit.");
        }
        catch (OperationCanceledException)
        {
            Invalidate("Cancelled", "Running-app verification was stopped.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError("Running application verification failed for app {AppId}: {ExceptionType}.",
                identity.Id, exception.GetType().Name);
            Invalidate("Failed", exception is AppBrowserCheckException ? exception.Message : exception is TimeoutException
                ? "The app did not become ready. Ask the team to check its services."
                : "Running-app verification failed. Check the runtime prerequisites and the team's user journeys.");
            throw new InvalidOperationException(status.Message, exception);
        }
        finally { gate.Release(); }
    }

    public void Invalidate(string state, string message)
    {
        status = new(state, message);
        evidence.RecordCheck("runtime", state, message);
    }

    private void Update(string state, string message)
    {
        status = new(state, message);
        evidence.RecordCheck("runtime", state, message);
    }

    private SavedAppRuntime? ReadSaved()
    {
        if (!File.Exists(StatePath)) return null;
        if (new FileInfo(StatePath).Length > 64 * 1024)
            throw new InvalidOperationException("The saved managed runtime exceeds its size limit.");
        SavedAppRuntime saved;
        try
        {
            saved = JsonSerializer.Deserialize<SavedAppRuntime>(File.ReadAllText(StatePath))
                ?? throw new InvalidOperationException("The saved managed runtime is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The saved managed runtime is unreadable.", exception);
        }
        if (saved.Lease.AppId != identity.Id)
            throw new InvalidOperationException("The saved runtime belongs to another application.");
        return saved;
    }

    private async Task<bool> MatchesSourceAsync(string commit, CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(5));
        foreach (string[] args in new[] { new[] { "rev-parse", "HEAD" }, new[] { "status", "--porcelain" } })
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workspace.RootPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not check the app's current revision.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(bounded.Token);
            Task<string> stderr = process.StandardError.ReadToEndAsync(bounded.Token);
            try { await process.WaitForExitAsync(bounded.Token); await Task.WhenAll(stdout, stderr); }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw;
            }
            if (process.ExitCode != 0) throw new InvalidOperationException("The app's current revision could not be read.");
            string value = (await stdout).Trim();
            if (args[0] == "rev-parse" ? value != commit : value.Length > 0) return false;
        }
        return true;
    }
}
