using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class AppBuildPolicyTests
{
    public static Task RunAsync(string fixtureSource)
    {
        Run(fixtureSource);
        return Task.CompletedTask;
    }

    public static void Run(string fixtureSource)
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), ".app-build-policy-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var example = Scenario("instruction-example");
            InstructionExampleMatchesManifest(example.Workspace, example.Policy);
            PlanningAndLegacy();
            SdkAndCentralPackages();
            UnsafeScriptsAndOrigins();
            ScopedIdsAndEndpoints();
            CapabilitiesAndContract();
            AppShape();
            ValidAppAndBoundaries();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
        Console.WriteLine("PASS app build policy predicts first-build blockers and preserves safe generated apps");

        (DisposableFixtureWorkspace Workspace, AppBuildPolicy Policy) Scenario(string name, AppBuildCapabilities? capabilities = null, bool legacy = false)
        {
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            string source = legacy ? fixtureSource :
                Path.Combine(Path.GetDirectoryName(fixtureSource)!, DisposableFixtureWorkspace.StarterTemplateName);
            var workspace = new DisposableFixtureWorkspace(source, directory, NullLogger<DisposableFixtureWorkspace>.Instance);
            return (workspace, new AppBuildPolicy(workspace, new("policy-demo"), capabilities ?? new(10, ["dotnet", "static"], true, true)));
        }

        void PlanningAndLegacy()
        {
            var planning = Scenario("planning");
            Write(planning.Workspace, "README.md", "# App plan");
            Write(planning.Workspace, ".squad/team.md", "private team");
            Require(planning.Policy.Inspect().Count == 0, "Planning and team metadata alone must not require an app contract.");
            Write(planning.Workspace, "index.html", "<h1>Hello</h1>");
            Expect(planning.Policy, "manifest-missing", "validation-script-missing", "runtime-script-missing");

            var legacy = Scenario("legacy", legacy: true);
            Write(legacy.Workspace, "src/flight-plan.sh", "printf '%s\\n' OK\n");
            Write(legacy.Workspace, "index.html", "<h1>Legacy fixture</h1>");
            Require(legacy.Policy.Inspect().Count == 0, "Legacy fixture templates must remain compatible without a manifest.");
        }

        void SdkAndCentralPackages()
        {
            var app = Scenario("sdk");
            StaticContract(app.Workspace);
            Write(app.Workspace, "Directory.Build.props", "<Project><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
            Write(app.Workspace, "Directory.Packages.props", """
                <Project>
                  <PropertyGroup><EfVersion>10.0.1</EfVersion></PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="$(EfVersion)" />
                    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.1" />
                  </ItemGroup>
                </Project>
                """);
            Write(app.Workspace, "api/Api.csproj", """
                <Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup>
                  <PackageReference Include="Microsoft.EntityFrameworkCore" />
                  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
                </ItemGroup></Project>
                """);
            Expect(app.Policy, "sdk-target-mismatch", "ef-provider-major-mismatch");
            Require(app.Policy.Inspect().Any(issue => issue.Code == "ef-provider-major-mismatch" && issue.Path == "api/Api.csproj"),
                "Project checks must resolve centrally managed property versions.");
        }

        void UnsafeScriptsAndOrigins()
        {
            var app = Scenario("unsafe");
            ServerContract(app.Workspace);
            Write(app.Workspace, "tests/run.sh", """
                #!/bin/bash
                set -euo pipefail
                [[ -f state.db ]]
                rm -f /data/state.db
                pkill -f generated-api
                killall dotnet
                """);
            Write(app.Workspace, "api/Program.cs", """
                var origin = builder.Configuration["APP_ORIGIN"];
                var uri = new Uri(origin);
                builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(uri.ToString())));
                """);
            Write(app.Workspace, "web/src/client.ts", """const endpoint = "http://localhost:5050/api/items";""");
            Write(app.Workspace, "web/src/config.ts", """export const api = "http://localhost:5050/api";""");
            Write(app.Workspace, "web/.env", "VITE_API_URL=http://localhost:5050/api\n");
            Write(app.Workspace, "tests/serve.sh", "docker compose up\n");
            Expect(app.Policy, "non-posix-script", "destructive-validation", "name-based-process-kill",
                "unsafe-origin", "browser-localhost", "runtime-lifecycle-violation");
            Require(new[] { "web/src/client.ts", "web/src/config.ts", "web/.env" }.All(path =>
                app.Policy.Inspect().Any(issue => issue.Code == "browser-localhost" && issue.Path == path)),
                "Shipped browser configuration, source and public environment values must all use same-origin URLs.");
            Require(app.Policy.Inspect().All(issue => !issue.Message.Contains("generated-api", StringComparison.Ordinal) &&
                !issue.Message.Contains("5050", StringComparison.Ordinal) && !issue.Message.Contains("APP_ORIGIN", StringComparison.Ordinal)),
                "Public issue messages must never echo source values.");
        }

        void ScopedIdsAndEndpoints()
        {
            var app = Scenario("ids");
            AspireContract(app.Workspace);
            Write(app.Workspace, "AppHost/AppHost.csproj", """
                <Project><PropertyGroup><IsAspireHost>true</IsAspireHost><UserSecretsId>shared-apphost</UserSecretsId></PropertyGroup></Project>
                """);
            Write(app.Workspace, "AppHost/Program.cs", """
                var web = builder.AddNpmApp("web", "../web")
                    .WithEnvironment("PORT", web.GetEndpoint("http").Property(EndpointProperty.Port));
                """);
            Expect(app.Policy, "unscoped-user-secrets", "apphost-http-endpoint-missing");
            Write(app.Workspace, "AppHost/AppHost.csproj", """
                <Project><PropertyGroup><IsAspireHost>true</IsAspireHost><UserSecretsId>squad-policy-demo-apphost</UserSecretsId></PropertyGroup></Project>
                """);
            Write(app.Workspace, "Api/Api.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
            Write(app.Workspace, "frontend/package.json", """{"name":"frontend","private":true}""");
            Write(app.Workspace, "AppHost/Program.cs", """
                var builder = DistributedApplication.CreateBuilder(args).AddSquadKubernetes();
                var db = builder.AddPostgres("db").PublishAsSquadDatabase().AddDatabase("appdb");
                var api = builder.AddProject<Projects.Api>("api").WithReference(db).PublishAsSquadApi();
                var web = builder.AddNpmApp("web", "../web").WithHttpEndpoint(targetPort: 8080)
                    .WithEnvironment("PORT", web.GetEndpoint("http").Property(EndpointProperty.Port));
                """);
            SquadHosting(app.Workspace);
            Require(app.Policy.Inspect().Count == 0, "Scoped IDs and explicit HTTP endpoint declarations must pass.");
            StaticContract(app.Workspace);
            Expect(app.Policy, "architecture-mismatch");
        }

        void AppShape()
        {
            var staticApp = Scenario("shape-static");
            StaticContract(staticApp.Workspace);
            Expect(staticApp.Policy, "app-shape-unsupported");
            var sqlite = Scenario("shape-sqlite");
            ServerContract(sqlite.Workspace, architecture: "aspire", database: "sqlite");
            Write(sqlite.Workspace, "index.html", "<h1>App</h1>");
            Expect(sqlite.Policy, "app-shape-unsupported");
            var layout = Scenario("shape-layout");
            AspireContract(layout.Workspace);
            Write(layout.Workspace, "README.md", "# App");
            Write(layout.Workspace, "index.html", "<h1>App</h1>");
            Require(AppBuildPolicy.AspireLayout.All(path => layout.Policy.Inspect().Any(issue =>
                issue.Code == "app-layout-missing" && issue.Path == path)),
                "An Aspire app without the AppHost/Api/frontend layout is reported before the worker tries to build it.");
            Require(layout.Policy.Inspect().All(issue => issue.Code != "app-shape-unsupported"),
                "An Aspire app with PostgreSQL is the supported shape.");
            Expect(layout.Policy, "squad-hosting-unused");
            SquadHosting(layout.Workspace);
            Write(layout.Workspace, "AppHost/Program.cs", "builder.AddPostgres(\"db\").PublishAsKubernetesService(_ => { });");
            Expect(layout.Policy, "squad-hosting-unused");
        }

        void CapabilitiesAndContract()
        {
            var app = Scenario("capabilities", new(10, ["static"], false));
            ServerContract(app.Workspace, database: "postgres");
            File.Delete(Path.Combine(app.Workspace.RootPath, "tests/serve.sh"));
            Expect(app.Policy, "validation-profile-unsupported", "runtime-unavailable", "postgres-unavailable", "runtime-script-missing");
            Write(app.Workspace, AppBuildManifest.FileName, """{"schemaVersion":1,"architecture":"static","validationProfile":"static","extra":"private-source-value"}""");
            Expect(app.Policy, "manifest-invalid");
            Require(app.Policy.Inspect().All(issue => !issue.Message.Contains("private-source-value", StringComparison.Ordinal)),
                "Invalid contracts must produce fixed safe messages.");
            Write(app.Workspace, AppBuildManifest.FileName, "{ broken");
            Expect(app.Policy, "manifest-invalid");
            Write(app.Workspace, AppBuildManifest.FileName, """{"schemaVersion":1,"architecture":"server","validationProfile":"dotnet"}""");
            Expect(app.Policy, "manifest-invalid");

            var staticApp = Scenario("static-needs-browser");
            StaticContract(staticApp.Workspace);
            Write(staticApp.Workspace, AppBuildManifest.FileName, """{"schemaVersion":1,"architecture":"static","validationProfile":"static"}""");
            File.Delete(Path.Combine(staticApp.Workspace.RootPath, "tests/serve.sh"));
            Expect(staticApp.Policy, "missing-runtime", "runtime-script-missing");

            var disguised = Scenario("server-disguised-as-static");
            StaticContract(disguised.Workspace);
            Write(disguised.Workspace, "api/Api.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
            Expect(disguised.Policy, "architecture-mismatch");
            File.Delete(Path.Combine(disguised.Workspace.RootPath, "api/Api.csproj"));
            Write(disguised.Workspace, "AppHost/Program.cs", "var builder = DistributedApplication.CreateBuilder(args);");
            Expect(disguised.Policy, "architecture-mismatch");
        }

        void ValidAppAndBoundaries()
        {
            var app = Scenario("valid");
            AspireContract(app.Workspace);
            Write(app.Workspace, "AppHost/AppHost.csproj", """
                <Project><PropertyGroup><IsAspireHost>true</IsAspireHost><UserSecretsId>squad-policy-demo-apphost</UserSecretsId></PropertyGroup></Project>
                """);
            Write(app.Workspace, "frontend/package.json", """{"name":"frontend","private":true}""");
            Write(app.Workspace, "AppHost/Program.cs", """
                var builder = DistributedApplication.CreateBuilder(args).AddSquadKubernetes();
                var db = builder.AddPostgres("db").PublishAsSquadDatabase().AddDatabase("appdb");
                builder.AddProject<Projects.Api>("api").WithReference(db).PublishAsSquadApi();
                """);
            SquadHosting(app.Workspace);
            Write(app.Workspace, "Api/Api.csproj", """
                <Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>
                  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.1" />
                  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
                </ItemGroup></Project>
                """);
            Write(app.Workspace, "tests/run.sh", """
                #!/bin/sh
                set -eu
                TEST_ROOT=$(mktemp -d)
                rm -f "$TEST_ROOT/state.db"
                printf '%s\n' PASS
                """);
            Write(app.Workspace, "api/Program.cs", """
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri)) { ConfigureCors(uri); }
                """);
            Write(app.Workspace, "web/src/client.ts", """fetch("/api/items");""");
            Write(app.Workspace, "web/vite.config.ts", """export default { server: { proxy: "http://localhost:5050" } };""");
            Write(app.Workspace, "tests/browser.spec.ts", """const origin = "http://localhost:8080";""");
            Write(app.Workspace, "api/appsettings.json", """{"Urls":"http://localhost:8080","Password":"replace-me"}""");
            foreach (string excluded in new[] { ".git", ".squad", ".github", "node_modules", "bin", "obj", ".aspire" })
                Write(app.Workspace, excluded + "/bad.csproj", "<Project><PropertyGroup><TargetFramework>net99.0</TargetFramework></PropertyGroup></Project>", artifact: false);
            Require(app.Policy.Inspect().Count == 0, "Safe scripts, guarded origins, matching EF majors and excluded build/server artifacts must pass.");
            string instructions = app.Policy.Instructions();
            Require(instructions.Contains("policy-demo", StringComparison.Ordinal) &&
                instructions.Contains("restartJourneys", StringComparison.Ordinal) &&
                instructions.Contains("ConnectionStrings__database", StringComparison.Ordinal) &&
                instructions.Contains("SQUAD_APP_DATA=/data", StringComparison.Ordinal) &&
                instructions.Contains("RestoreSources", StringComparison.Ordinal),
                "First-build guidance must carry app scope, persistence, supplied connections and local restore prerequisites.");

            var prefix = Scenario("prefix");
            StaticContract(prefix.Workspace);
            Write(prefix.Workspace, "index.html", """<script type="module" src="./app.js"></script><a href="./settings">Settings</a>""");
            Require(prefix.Policy.Inspect().All(issue => issue.Code == "app-shape-unsupported"),
                "Relative modern static module previews raise no link issues; only the unsupported app shape is reported.");
            Write(prefix.Workspace, "index.html", """<script src="/assets/app.js"></script>""");
            Expect(prefix.Policy, "static-root-link");
            Write(prefix.Workspace, "oversize.ts", new string(' ', 128 * 1024 + 1));
            Expect(prefix.Policy, "inspection-limit");

            var links = Scenario("links");
            StaticContract(links.Workspace);
            File.CreateSymbolicLink(Path.Combine(links.Workspace.RootPath, "linked.ts"),
                Path.Combine(app.Workspace.RootPath, "web/src/client.ts"));
            Expect(links.Policy, "artifact-unreadable");
            Directory.Delete(links.Workspace.RootPath, recursive: true);
            Expect(links.Policy, "artifact-unreadable");
        }
    }

    private static void InstructionExampleMatchesManifest(DisposableFixtureWorkspace workspace, AppBuildPolicy policy)
    {
        string instructions = policy.Instructions();
        const string startFence = "```json";
        int start = instructions.IndexOf(startFence, StringComparison.Ordinal);
        Require(start >= 0, "Instructions must contain a complete fenced app.build.json example.");
        start += startFence.Length;
        int end = instructions.IndexOf("```", start, StringComparison.Ordinal);
        Require(end > start, "The instruction contract example must have a closing JSON fence.");
        Write(workspace, AppBuildManifest.FileName, instructions[start..end].Trim());
        AppBuildManifest manifest = AppBuildManifest.Read(workspace) ??
            throw new InvalidOperationException("The actual instruction example must be readable as an app build manifest.");
        manifest.Validate(workspace);
        Require(manifest.Architecture == "aspire" && manifest.Runtime is { Database: "postgres", HealthPaths.Count: > 0, Journeys.Count: > 0, RestartJourneys.Count: > 0 },
            "The instruction example must nest a complete database runtime and matching restart journeys inside runtime.");
    }

    private static void StaticContract(DisposableFixtureWorkspace workspace)
    {
        var manifest = new AppBuildManifest(1, "static", "static",
            new AppRuntimeSpec(["/"],
                [new AppBrowserJourney("Show detail", [new("click", "#show"), new("visible", "#detail")])]));
        Write(workspace, AppBuildManifest.FileName, JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Write(workspace, "tests/run.sh", "#!/bin/sh\nprintf '%s\\n' PASS\n");
        Write(workspace, "tests/serve.sh", "#!/bin/sh\nexec python3 -m http.server 8080 --bind 0.0.0.0 --directory .\n");
        Write(workspace, "index.html", "<h1>App</h1>");
    }

    private static void ServerContract(DisposableFixtureWorkspace workspace, string architecture = "server", string database = "sqlite")
    {
        var manifest = new AppBuildManifest(1, architecture, "dotnet",
            new AppRuntimeSpec(["/health/ready"],
                [new AppBrowserJourney("Create item", [new("fill", "#name", "Example"), new("click", "#save"), new("text", "#items", "Example")])],
                database, RestartJourneys:
                [new AppBrowserJourney("Saved item survives restart", [new("text", "#items", "Example")])]));
        Write(workspace, AppBuildManifest.FileName, JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Write(workspace, "tests/run.sh", "#!/bin/sh\nprintf '%s\\n' PASS\n");
        Write(workspace, "tests/serve.sh", "#!/bin/sh\nexec dotnet api/Api.dll --urls http://0.0.0.0:8080\n");
    }

    /// <summary>Copies the platform-provided hosting helpers from the starter template, as a reset workspace has them.</summary>
    private static void SquadHosting(DisposableFixtureWorkspace workspace) =>
        Write(workspace, "AppHost/SquadHosting.cs", File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "FixtureSource", DisposableFixtureWorkspace.StarterTemplateName, "AppHost", "SquadHosting.cs")));

    private static void AspireContract(DisposableFixtureWorkspace workspace) =>
        ServerContract(workspace, architecture: "aspire", database: "postgres");

    private static void Write(DisposableFixtureWorkspace workspace, string path, string content, bool artifact = true)
    {
        string destination = artifact ? workspace.PathPolicy.ResolveArtifactPath(path) : workspace.PathPolicy.ResolveWorkspacePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, content);
    }

    private static void Expect(AppBuildPolicy policy, params string[] codes)
    {
        IReadOnlyList<AppBuildIssue> issues = policy.Inspect();
        foreach (string code in codes)
            Require(issues.Any(issue => issue.Code == code), "Expected predictive preflight issue: " + code +
                "; got " + string.Join(", ", issues.Select(issue => issue.Code)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
