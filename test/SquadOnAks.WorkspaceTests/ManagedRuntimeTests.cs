using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class ManagedRuntimeTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        string root = Path.Combine(Path.GetTempPath(), "squad-runtime-tests", Guid.NewGuid().ToString("N"));
        var workspace = new DisposableFixtureWorkspace(fixtureSource, root,
            NullLogger<DisposableFixtureWorkspace>.Instance);
        workspace.Reset();
        var spec = new AppRuntimeSpec(["/health"], [new("Create", [new("click", "#add"), new("text", "#value", "1")])],
            "sqlite", RestartJourneys: [new("Saved data", [new("text", "#value", "1")])]);
        var manifest = new AppBuildManifest(1, "server", "dotnet", spec);
        string appId = Guid.NewGuid().ToString("N");
        try
        {
            var host = new FakeHost(appId);
            var browser = new FakeBrowser();
            var evidence = new WorkEvidenceStore();
            var runtime = new ManagedAppRuntime(workspace, new(appId), host, browser, evidence,
                NullLogger<ManagedAppRuntime>.Instance);
            await runtime.VerifyAndStartAsync(manifest, "commit-one", CancellationToken.None);
            Require(host.Events.SequenceEqual(new[] { "test:start", "ready", "restart", "ready", "test:stop", "live:start", "ready" }),
                "Acceptance must use a disposable app, restart it, remove it, then start the persistent app.");
            Require(browser.Calls.SequenceEqual(new[] { "desktop:1", "desktop:1", "mobile:0" }),
                "Browser journeys must run before and after restart, with a separate mobile check.");
            Require(runtime.GetStatus() is { State: "Ready", SourceCommit: "commit-one", Url: not null },
                "Only a fully accepted runtime gets a URL.");
            Require(evidence.GetSnapshot().Items.Last().State == "Passed", "Acceptance must record its actual result.");

            var clusterRuntime = new ManagedAppRuntime(workspace, new(appId), new UnavailableAppRuntimeHost(), browser,
                new WorkEvidenceStore(), NullLogger<ManagedAppRuntime>.Instance);
            await clusterRuntime.VerifyAndStartAsync(manifest, "commit-one", CancellationToken.None);
            Require(clusterRuntime.GetStatus() is { State: "NotChecked", Url: null } && browser.Calls.Count == 3,
                "Without a host runtime (cluster mode) the wave is not failed and not marked running; the deployment's health checks verify it.");

            host.Events.Clear();
            browser.Fail = true;
            InvalidOperationException? failure = null;
            try { await runtime.VerifyAndStartAsync(manifest, "commit-two", CancellationToken.None); }
            catch (InvalidOperationException exception) { failure = exception; }
            Require(failure is { InnerException: not null } &&
                !failure.Message.Contains("Deliberate browser failure", StringComparison.Ordinal),
                "Untrusted browser exceptions must retain private diagnostics without leaking into public wave errors.");
            Require(host.Events.SequenceEqual(new[] { "test:start", "ready", "test:stop" }),
                "A failed browser journey must clean up test resources and never promote or replace the working app.");
            Require(runtime.GetStatus() is { State: "Failed", Url: null }, "Browser failure must not expose a success URL.");
            Require(File.ReadAllText(workspace.StatePath("soaks-runtime.json")).Contains("commit-one"),
                "Failed updates must preserve the saved working revision.");
            Console.WriteLine("PASS runtime promotion gates, disposable acceptance, restart persistence and failed-update preservation");

            await File.WriteAllTextAsync(Path.Combine(root, AppBuildManifest.FileName),
                """{"schemaVersion":1,"architecture":"server","validationProfile":"dotnet","unexpected":true}""");
            RequireThrows(() => AppBuildManifest.Read(workspace), "Unknown contract fields must fail explicitly.");
            RequireThrows(() => (manifest with { Runtime = spec with { HealthPaths = ["//host/health"] } }).Validate(workspace),
                "Readiness paths must not redirect probes to another origin.");
            RequireThrows(() => (manifest with { Runtime = spec with { RestartJourneys = null } }).Validate(workspace),
                "Database applications need persistence acceptance after restart.");
            await File.WriteAllTextAsync(Path.Combine(root, AppBuildManifest.FileName),
                """{"schemaVersion":1,"architecture":"server","validationProfile":"dotnet","runtime":{"healthPaths":["/health"],"journeys":[{"name":"Reload after action","steps":[{"action":"click","selector":"#add"},{"action":"reload"},{"action":"text","selector":"#value","value":"1"}]}]}}""");
            Require(AppBuildManifest.Read(workspace)?.Runtime?.Journeys[0].Steps[1] is { Action: "reload", Selector: "" },
                "Reload must deserialize without a selector.");
            RequireThrows(() => (manifest with
            {
                Runtime = spec with { Journeys = [new("Missing action target", [new("click"), new("text", "#value", "1")])] }
            }).Validate(workspace), "Interactive actions must still declare their selectors.");
            Console.WriteLine("PASS bounded strict manifests and same-origin readiness contracts");
        }
        finally { Directory.Delete(root, recursive: true); }

        string? image = Environment.GetEnvironmentVariable("SQUAD_RUNTIME_DOCKER_TEST_IMAGE");
        if (string.IsNullOrWhiteSpace(image))
        {
            Console.WriteLine("SKIP real managed runtime: SQUAD_RUNTIME_DOCKER_TEST_IMAGE is unset");
            return;
        }
        await RunDockerAsync(fixtureSource, image);
    }

    private static async Task RunDockerAsync(string fixtureSource, string image)
    {
        string root = Path.Combine(Path.GetTempPath(), "squad-runtime-docker-tests", Guid.NewGuid().ToString("N"));
        var workspace = new DisposableFixtureWorkspace(fixtureSource, root,
            NullLogger<DisposableFixtureWorkspace>.Instance);
        workspace.Reset();
        string appId = Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(Path.Combine(root, "tests", "serve.sh"), """
            set -eu
            test "$(id -u)" = 1654
            test ! -e /var/run/docker.sock
            test ! -e .git
            test "${COPILOT_TOKEN+x}" != x
            test "${GITHUB_TOKEN+x}" != x
            exec python3 tests/runtime-fixture.py
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "tests", "runtime-fixture.py"), """
            import os, sqlite3
            from http.server import BaseHTTPRequestHandler, HTTPServer
            db = os.path.join(os.environ["SQUAD_APP_DATA"], "proof.db")
            def read(increment=False):
                with sqlite3.connect(db) as c:
                    c.execute("CREATE TABLE IF NOT EXISTS counter (value INTEGER NOT NULL)")
                    if c.execute("SELECT COUNT(*) FROM counter").fetchone()[0] == 0:
                        c.execute("INSERT INTO counter VALUES (0)")
                    if increment: c.execute("UPDATE counter SET value=value+1")
                    return str(c.execute("SELECT value FROM counter").fetchone()[0])
            class Handler(BaseHTTPRequestHandler):
                def do_GET(self):
                    if self.path == "/health":
                        read()
                        text, kind = "ready", "text/plain"
                    elif self.path == "/value": text, kind = read(), "text/plain"
                    elif self.path == "/":
                        text = '<!doctype html><meta name="viewport" content="width=device-width"><button id="add">Add</button><p id="value"></p><script>async function update(path,method="GET"){document.querySelector("#value").textContent=await(await fetch(path,{method})).text()}document.querySelector("#add").onclick=()=>update("/value","POST");update("/value")</script>'
                        kind = "text/html"
                    else:
                        self.send_error(404)
                        return
                    self.send_response(200)
                    self.send_header("Content-Type", kind)
                    self.end_headers()
                    self.wfile.write(text.encode())
                def do_POST(self):
                    self.send_response(200)
                    self.end_headers()
                    self.wfile.write(read(True).encode())
            HTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
            """);
        var spec = new AppRuntimeSpec(["/health"], [new("Increment", [new("text", "#value", "0"), new("click", "#add"), new("text", "#value", "1"), new("reload"), new("text", "#value", "1")])],
            "sqlite", RestartJourneys: [new("Reload saved value", [new("reload"), new("text", "#value", "1")])]);
        var manifest = new AppBuildManifest(1, "server", "static", spec);
        var factory = new RuntimeHttpClients();
        var host = new DockerAppRuntimeHost(new(new Dictionary<string, string> { ["static"] = image }, null,
            TimeSpan.FromSeconds(30)), factory, NullLogger<DockerAppRuntimeHost>.Instance,
            NullLoggerFactory.Instance);
        AppRuntimeLease? lease = null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            lease = await host.StartAsync(workspace, appId, manifest, disposable: true, timeout.Token);
            await host.WaitReadyAsync(lease, spec, timeout.Token);
            await RequireFailureAsync(() => host.StopAsync(lease with { ExportPath = Path.GetTempPath() }, timeout.Token));
            await host.WaitReadyAsync(lease, spec, timeout.Token);
            var browser = new AppBrowserAcceptance();
            await browser.CheckAsync(lease.Origin, spec.Journeys, mobile: false, timeout.Token);
            await host.RestartAsync(lease, timeout.Token);
            await host.WaitReadyAsync(lease, spec, timeout.Token);
            await browser.CheckAsync(lease.Origin, spec.RestartJourneys!, mobile: false, timeout.Token);
            await browser.CheckAsync(lease.Origin, [], mobile: true, timeout.Token);
            InvalidOperationException? browserFailure = null;
            try
            {
                await browser.CheckAsync(lease.Origin, [new("Private journey title",
                    [new("text", "#private-selector", "private expected value")])], mobile: false, timeout.Token);
            }
            catch (InvalidOperationException exception) { browserFailure = exception; }
            Require(browserFailure?.Message.Contains("journey 1, step 1 (text)", StringComparison.Ordinal) == true &&
                !browserFailure.Message.Contains("private", StringComparison.OrdinalIgnoreCase),
                "Real browser failures must identify the failing step without publishing selectors, titles or values.");
            string? postgresImage = Environment.GetEnvironmentVariable("SQUAD_RUNTIME_POSTGRES_TEST_IMAGE");
            if (!string.IsNullOrWhiteSpace(postgresImage))
                await RunPostgresUpdatesAsync(workspace, image, postgresImage, factory);
            Console.WriteLine("PASS real isolated runtime, SQLite user flow, fresh-browser restart persistence and mobile layout");
            var catalog = new WorkerDependencyCatalog(new Dictionary<string, string> { ["dotnet"] = image });
            string hints = await catalog.InstructionsAsync(timeout.Token);
            Require(hints.Contains("Verified offline NuGet cache for dotnet"), "Generation must receive actual cache evidence.");
            await File.WriteAllTextAsync(Path.Combine(root, "Missing.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="999.0.0" /></ItemGroup></Project>""");
            Require(catalog.Inspect(workspace, manifest with { ValidationProfile = "dotnet" })
                .Any(issue => issue.Code == "dependency-not-cached"), "An uncached pinned dependency must be rejected before running tests.");
            File.Delete(Path.Combine(root, "Missing.csproj"));
            await File.WriteAllTextAsync(Path.Combine(root, "tests", "run.sh"), "dotnet restore --locked-mode\n");
            await File.WriteAllTextAsync(Path.Combine(root, "packages.lock.json"),
                """{"version":2,"dependencies":{"net10.0":{"Microsoft.EntityFrameworkCore.Relational":{"type":"Transitive","resolved":"999.0.0"}}}}""");
            Require(catalog.Inspect(workspace, manifest with { ValidationProfile = "dotnet" })
                .Any(issue => issue.Code == "dependency-lock-not-cached"), "Locked transitive dependencies must be checked before validation.");
            await File.WriteAllTextAsync(Path.Combine(root, "tests", "run.sh"), "dotnet restore --force-evaluate\n");
            Require(!catalog.Inspect(workspace, manifest with { ValidationProfile = "dotnet" })
                .Any(issue => issue.Code == "dependency-lock-not-cached"), "Explicit offline lock re-evaluation must not be rejected for an unused host lock.");
            Console.WriteLine("PASS immutable cache discovery, predictive missing-package rejection and forged cleanup-path refusal");
        }
        finally
        {
            if (lease is not null) await host.StopAsync(lease, CancellationToken.None);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task RunPostgresUpdatesAsync(
        DisposableFixtureWorkspace workspace, string image, string postgresImage, IHttpClientFactory clients)
    {
        var host = new DockerAppRuntimeHost(new(new Dictionary<string, string> { ["static"] = image }, postgresImage,
            TimeSpan.FromSeconds(30)), clients, NullLogger<DockerAppRuntimeHost>.Instance, NullLoggerFactory.Instance);
        var spec = new AppRuntimeSpec(["/health"], [new("Save", [new("click", "#add"), new("text", "#value", "1")])],
            "postgres", RestartJourneys: [new("Read", [new("text", "#value", "1")])]);
        var manifest = new AppBuildManifest(1, "server", "static", spec);
        AppRuntimeLease? old = null, candidate = null, live = null;
        var volumes = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            string appId = Guid.NewGuid().ToString("N");
            old = await host.StartAsync(workspace, appId, manifest, false, timeout.Token);
            volumes.AddRange([old.DataVolume, old.DatabaseVolume!]);
            await host.WaitReadyAsync(old, spec, timeout.Token);
            (int ExitCode, string Output) sql;
            do
            {
                sql = await DockerAsync(
                    ["exec", old.DatabaseContainer!, "psql", "-U", "application", "-d", "application", "-c",
                        "CREATE TABLE IF NOT EXISTS update_proof(value integer); INSERT INTO update_proof VALUES(7);"],
                    timeout.Token);
                if (sql.ExitCode != 0) await Task.Delay(250, timeout.Token);
            } while (sql.ExitCode != 0);
            candidate = await host.StartAsync(workspace, appId, manifest, false, timeout.Token, old);
            Require(candidate.SharedDatabaseContainer == old.DatabaseContainer && candidate.DatabaseContainer is null,
                "Updates must reuse PostgreSQL, never start a second writer on its data volume.");
            await host.WaitReadyAsync(candidate, spec, timeout.Token);
            await host.StopAsync(candidate, timeout.Token, preserveDatabase: true);
            candidate = null;
            await host.WaitReadyAsync(old, spec, timeout.Token);
            live = await host.StartAsync(workspace, appId, manifest, false, timeout.Token, old);
            await host.WaitReadyAsync(live, spec, timeout.Token);
            await host.StopAsync(old, timeout.Token, preserveDatabase: true);
            old = null;
            sql = await DockerAsync(
                ["exec", live.SharedDatabaseContainer!, "psql", "-U", "application", "-d", "application",
                    "-Atc", "SELECT value FROM update_proof;"], timeout.Token);
            Require(sql.ExitCode == 0 && sql.Output.Trim() == "7",
                "Failed candidates and successful updates must preserve actual PostgreSQL data.");
            await host.WaitReadyAsync(live, spec, timeout.Token);
            Console.WriteLine("PASS real PostgreSQL single-writer updates, failed-candidate preservation and retained database rows");
        }
        finally
        {
            if (candidate is not null) await host.StopAsync(candidate, CancellationToken.None, preserveDatabase: true);
            if (live is not null) await host.StopAsync(live, CancellationToken.None);
            if (old is not null) await host.StopAsync(old, CancellationToken.None);
            foreach (string volume in volumes)
            {
                var removed = await DockerAsync(
                    ["volume", "rm", volume], CancellationToken.None);
                Require(removed.ExitCode == 0, "Test-owned PostgreSQL volumes must be cleaned up.");
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> DockerAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var start = new System.Diagnostics.ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Docker did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }

    private sealed class RuntimeHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false
        })
        { Timeout = TimeSpan.FromSeconds(3) };
    }

    private sealed class FakeHost(string appId) : IAppRuntimeHost
    {
        public List<string> Events { get; } = [];
        public Task<AppRuntimeLease> StartAsync(DisposableFixtureWorkspace workspace, string id, AppBuildManifest manifest,
            bool disposable, CancellationToken cancellationToken, AppRuntimeLease? previous = null)
        {
            Events.Add(disposable ? "test:start" : "live:start");
            return Task.FromResult(new AppRuntimeLease(appId, "fake", "fake", "fake", null, null,
                new Uri("http://127.0.0.1:8080/"), "fake", disposable));
        }
        public Task WaitReadyAsync(AppRuntimeLease lease, AppRuntimeSpec spec, CancellationToken cancellationToken)
        { Events.Add("ready"); return Task.CompletedTask; }
        public Task RestartAsync(AppRuntimeLease lease, CancellationToken cancellationToken)
        { Events.Add("restart"); return Task.CompletedTask; }
        public Task StopAsync(AppRuntimeLease lease, CancellationToken cancellationToken, bool preserveDatabase = false)
        { Events.Add(lease.Disposable ? "test:stop" : "live:stop"); return Task.CompletedTask; }
    }

    private sealed class FakeBrowser : IAppBrowserAcceptance
    {
        public List<string> Calls { get; } = [];
        public bool Fail { get; set; }
        public Task CheckAsync(Uri origin, IReadOnlyList<AppBrowserJourney> journeys, bool mobile, CancellationToken cancellationToken)
        {
            Calls.Add($"{(mobile ? "mobile" : "desktop")}:{journeys.Count}");
            return Fail ? Task.FromException(new InvalidOperationException("Deliberate browser failure.")) : Task.CompletedTask;
        }
    }

    private static async Task RequireFailureAsync(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected acceptance failure.");
    }
    private static void RequireThrows(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
