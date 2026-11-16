using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed record AppBuildIssue(string Code, string Message, string? Path = null);

/// <summary>Shared first-build guidance and conservative, bounded artifact preflight checks.</summary>
public sealed class AppBuildPolicy(
    DisposableFixtureWorkspace workspace,
    AppIdentity identity,
    AppBuildCapabilities capabilities)
{
    /// <summary>An imported repository that doesn't declare the deploy contract; waves work on it as it is.</summary>
    public bool IsPlainRepository => workspace.IsPlainRepository;

    private const int MaxEntries = 4096;
    private const int MaxFiles = 512;
    private const int MaxFileBytes = 128 * 1024;
    private const int MaxTotalBytes = 4 * 1024 * 1024;
    private static readonly HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".squad", ".github", "node_modules", "bin", "obj", ".aspire"
    };
    /// <summary>The files the worker's build script expects for every generated Aspire app.</summary>
    public static readonly string[] AspireLayout = ["AppHost/AppHost.csproj", "Api/Api.csproj", "frontend/package.json", "frontend/package-lock.json"];
    private static readonly HashSet<string> extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".fsproj", ".props", ".json", ".html", ".css",
        ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".sh", ".env", ".py", ".toml", ".fs"
    };

    /// <summary>Inject on every work turn, not on casting turns.</summary>
    public string Instructions() => $$"""
        FIRST-BUILD POLICY (apply on every work turn; this is not casting guidance)
        Generate the requested application natively in its own workspace. Do not ask the platform
        for templates/scaffolding, edit platform/generated-app boundaries, or add dynamic Aspire
        resources to the platform. Preserve the requested architecture, including a real Aspire
        AppHost when requested. A Microsoft.NET.Sdk.Web or AppHost workload is server/aspire, never
        label it static merely to make preview green. Never silently downgrade an architecture to bypass a missing cache,
        package, runtime or database prerequisite: report the explicit blocker.

        Declared capabilities: .NET SDK major {{capabilities.DotnetMajor}};
        validation profiles {{System.Text.Json.JsonSerializer.Serialize(capabilities.ValidationProfiles)}};
        app runtime available {{capabilities.RuntimeAvailable.ToString().ToLowerInvariant()}};
        PostgreSQL available {{capabilities.PostgresAvailable.ToString().ToLowerInvariant()}}.
        {{(capabilities.ClusterBuild ? ClusterBuildNote : "")}}
        App ID: {{System.Text.Json.JsonSerializer.Serialize(identity.Id)}}.
        Use this actual app ID in stable scoped UserSecretsId and volume names, for example
        squad-{{identity.Id}}-apphost and squad-{{identity.Id}}-data. Never use generic template IDs.
        Do not guess SDK or package versions beyond the declared SDK major. Inspect the available
        local RestoreSources package feed/cache, select available pinned compatible versions,
        keep EF Core and its EF database provider on consistent majors (including central props),
        and preserve packages.lock.json for reproducible restores. Missing packages are blockers,
        not permission to invent versions or fall back silently.
        Offline lockfiles must cover the actual project graph, target frameworks and worker RID,
        including every locked transitive dependency in the worker's local package cache. Do not
        blindly reuse host --locked-mode results as worker readiness proof. Check the platform's
        dependency catalog and report missing cached transitives as explicit prerequisite blockers.

        Once real app output exists, commit app.build.json with schemaVersion 1 and a supported
        validationProfile, and provide POSIX tests/run.sh, executed as sh tests/run.sh.
        The ONLY top-level contract fields are schemaVersion, architecture, validationProfile and
        runtime. Nest ALL runtime fields inside runtime: database, startScript, healthPaths, journeys
        and restartJourneys. Never put startScript, healthPaths, journeys or restartJourneys at the
        top level; strict contract validation rejects them there.
        Example contract (replace the example validationProfile with a supported profile):
        ```json
        {{AppBuildManifest.Example("aspire", "dotnet", "postgres")}}
        ```
        APP SHAPE (required for every new app; this is what the platform builds and deploys):
        architecture "aspire" and runtime database "postgres". There is no static-only, server-only,
        SQLite or database-free shape. Use exactly this layout at the workspace root:
        PACKAGES ARE PROVIDED: the workspace starts with Directory.Build.props, Directory.Packages.props,
        AppHost/AppHost.csproj, Api/Api.csproj, frontend/package.json and frontend/package-lock.json.
        They pin the exact packages the offline build worker has cached. Keep these package sets and
        versions unchanged (add only frontend scripts, project properties or ProjectReferences); do not
        add, remove or re-version a package, and do not write a new lockfile. Use what they provide:
        Aspire hosting packages in the AppHost; Aspire.Npgsql.EntityFrameworkCore.PostgreSQL in the API
        (builder.AddNpgsqlDbContext<T>("appdb"), which brings EF Core and Npgsql); React, React DOM,
        Vite and @vitejs/plugin-react in the front end.
        - AppHost/AppHost.csproj: the provided Aspire AppHost project. AppHost/SquadHosting.cs is
          provided by the platform: keep it unchanged.
          AppHost/Program.cs follows this shape:
          var builder = DistributedApplication.CreateBuilder(args).AddSquadKubernetes();
          var db = builder.AddPostgres("db").PublishAsSquadDatabase().AddDatabase("appdb");
          var api = builder.AddProject<Projects.Api>("api").WithHttpEndpoint(name: "http")
              .WithHttpHealthCheck("/health/ready").WithExternalHttpEndpoints()
              .WithReference(db).WaitFor(db).PublishAsSquadApi();
          var frontend = builder.AddViteApp("frontend", "../frontend").WithNpm(installCommand: "ci")
              .WithReference(api);
          api.PublishWithContainerFiles(frontend, "./wwwroot");
          builder.Build().Run();
          The helpers harden both workloads for the app's namespace and keep database data on a
          persistent volume; do not re-implement them with PublishAsKubernetesService.
        - Api/Api.csproj: the ASP.NET Core API (resource name "api"). EF Core with Npgsql using
          the "appdb" connection, creating its schema at startup. /health/ready must query the
          database. It serves the built front end from wwwroot, so the browser and API share one origin.
        - frontend/: a Vite + React front end that calls the API with relative paths (api/...),
          never absolute URLs or localhost ports, so it works under a prefix-mounted path.
        Keep the resource names api, db, appdb and frontend: the platform's deployment, readiness
        and topology views rely on them.
        MORE THAN ONE API, FRONT END OR DATABASE (only when the brief asks for it): "api" stays the
        app's single entry and the only service the browser talks to.
        - Another API: a top-level folder with <Folder>/<Folder>.csproj copied from Api/Api.csproj
          (same packages), its own /health/ready, and in the AppHost
          builder.AddProject<Projects.<Folder>>("<name>").WithHttpEndpoint(name: "http")
              .WithHttpHealthCheck("/health/ready").PublishAsSquadApi();
          plus a ProjectReference from AppHost/AppHost.csproj. The api project reaches it server-side
          through api.WithReference(<it>) (base address from configuration "services:<name>:http:0");
          the browser never calls it directly, so api forwards what the front ends need. When a call to
          another API fails, api answers with that service's name, status and short reason, for example
          {"error":"votes answered 500: duplicate key"}, and every API puts a short reason in its own 5xx
          answers, so a failed check names the service and why.
        - Another front end: a top-level folder with package.json and package-lock.json copied
          unchanged from frontend/, AddViteApp("<name>", "../<folder>").WithNpm(installCommand: "ci"),
          and api.PublishWithContainerFiles(<it>, "./wwwroot/<name>") so api serves it at /<name>/.
          In Api/Program.cs serve the front ends with exactly:
              app.UseDefaultFiles(); app.UseStaticFiles();
              app.MapFallbackToFile("/<name>/{*path:nonfile}", "<name>/index.html");
              app.MapFallbackToFile("index.html");
          Keep :nonfile on every fallback pattern (without it the fallback answers the front ends'
          own script and style requests with HTML) and add no other route for /<name> (a redirect
          there also matches /<name>/ and loops). Link to
          it from the main front end with a relative link (<name>/), and keep its API calls relative (../api/...).
        - Another database: .AddDatabase("<name>") on db, or another server
          builder.AddPostgres("<server>").PublishAsSquadDatabase() with its own .AddDatabase("<name>");
          reference it only from the project that owns that data. Nothing creates databases for you
          in the cluster: each project creates its own database if it doesn't exist before creating its
          schema (EnsureCreatedAsync does both; raw SQL schema scripts need the database created first).
        Declare every component in app.build.json under runtime.components, for example:
        "components": {"projects": [{"name": "api", "path": "Api"}, {"name": "catalog", "path": "Catalog"}],
          "frontends": [{"name": "frontend", "path": "frontend", "mount": "/"}, {"name": "admin", "path": "admin", "mount": "/admin/"}],
          "databases": [{"server": "db", "name": "appdb"}, {"server": "ordersserver", "name": "ordersdb"}]}
        Names are lowercase letters and digits only. At most 4 projects, 3 front ends and 3 database
        servers. The platform builds, deploys and draws exactly what is declared there. Database-backed apps must declare
        read-only restartJourneys that observe already saved data, never repeat create/write steps,
        proving persisted state after a platform-owned restart.
        Restart journey actions are restricted to text, count, visible and reload.
        Reload steps use {"action":"reload"} without a selector; other actions require selectors.
        Include at least one text/count assertion on a selector other than body, html or *, with exactly the same
        selector, action and value asserted in the original user journey, as shown in the serialized
        contract example above. Generic body visibility does not prove persistence; do not recreate saved data.
        Journey steps are objects {"action","selector","value"} and the ONLY actions are click, fill,
        press, text, visible, count and reload. There is no goto/navigate step and no other field:
        every journey starts at the app's root page.
        Journeys are selector-based Playwright user interactions with observable text/visible/count
        assertions, not page-open smoke checks. Health paths must perform real API/database readiness
        checks, not unconditional success. Persistence belongs in SQUAD_APP_DATA=/data or the supplied
        PostgreSQL connection; prove persistence after restart, not just in memory.
        Razor Pages forms must enable MVC taghelpers in a scoped _ViewImports.cshtml using
        @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers. Retain antiforgery protection and its
        generated form token, bind named inputs to the intended handler properties/parameters, and
        prove actual create/delete POST workflows in the real browser with observable saved/deleted
        state. Build/health success is not form acceptance. Never disable antiforgery to make checks pass.
        Launch workload DLLs with the correct Web project working/content/web root so static assets
        resolve; do not compensate for a wrong root with brittle absolute browser URLs.

        All starter apps need tests/serve.sh. Bind the frontend/gateway HTTP on all interfaces at
        port 8080; .NET render/services bind explicit separate INTERNAL ports. Only frontend/gateway
        8080 is exposed, and all browser API traffic goes through its same-origin reverse proxy.
        The local single-origin runtime supports bounded HTTP request/response workflows; do not
        depend on WebSocket or SSE. App networking is isolated: browser acceptance must not require
        external services, fonts or CDNs. Bundle all browser assets and dependencies locally.
        Serve frontend and API through one same-origin gateway/reverse proxy. Browser requests use
        relative API paths, never hardcoded localhost ports. Guard optional origins with Uri.TryCreate
        before URI/CORS configuration. Declare explicit HTTP endpoints for dynamic AppHost web ports.
        Keep the real Aspire AppHost source, but tests/serve.sh runs the app workloads directly using
        provided ConnectionStrings__database and SQUAD_APP_DATA=/data: never invoke docker, Aspire
        or AppHost there to create nested containers. The platform owns runtimes and their lifecycle;
        do not detach agent processes or start arbitrary host services.

        Validation must be isolated and non-destructive: use a disposable test data root/database,
        never rm a live database, pkill or killall. Use portable sh syntax, no [[ ]], arrays, source,
        process substitution, or pipefail. Clean up only resources owned by the test.
        Modern static ES-module previews are allowed with the shared sandbox unchanged.
        Never attempt live npm network access during validation/runtime. Dependencies must already
        exist in the chosen profile image, or preserve a prebuilt static dist in the exported app.
        The worker image provides Python3, Node and the declared .NET SDK major; do not guess versions.
        When runtime capability is unavailable, report that browser/runtime behavior cannot be
        verified and preserve the requested architecture.
        Static preview assets/navigation use relative links (prefix-mounted preview), not /assets links.
        The online app page is sandboxed without same-origin access: localStorage, sessionStorage,
        IndexedDB and cookies throw or are unavailable. Keep state in the API/database (or in memory
        for the current page) and never read browser storage on load. Browser requests through the
        online proxy carry only the method, path, query string, body and Content-Type: no custom
        headers (X-...), Authorization or cookies. Send identity such as a typed name in the JSON
        body or query string.
        Never inline credentials or surface raw logs/source snippets containing secrets in public output.
        """;

    public IReadOnlyList<AppBuildIssue> Inspect()
    {
        // An imported repository is checked against the deploy contract only once it declares one.
        if (workspace.IsPlainRepository) return [];
        var issues = new List<AppBuildIssue>();
        Dictionary<string, string> files = ReadArtifacts(issues);
        AppBuildManifest? manifest = null;
        try
        {
            manifest = AppBuildManifest.Read(workspace);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Add("artifact-unreadable", "The app build contract cannot be read. Restore workspace access and retry.", AppBuildManifest.FileName);
        }
        catch (InvalidOperationException exception)
        {
            // Name the broken rule when it is one of the contract's own fixed messages, so a fix wave knows what to change.
            string rule = exception is AppBuildContractException ? " " + exception.Message : "";
            Add("manifest-invalid", "Correct app.build.json using schemaVersion 1 and the supported architecture, runtime and journey contract. Journey steps have only action, selector and value; actions are click, fill, press, text, visible, count and reload (no goto: journeys start at the root page)." + rule, AppBuildManifest.FileName);
        }

        bool output = files.Keys.Any(IsAppOutput);
        bool starter = workspace.Template == DisposableFixtureWorkspace.StarterTemplateName;
        if (starter && output && manifest is null && !issues.Any(issue => issue.Path == AppBuildManifest.FileName))
            Add("manifest-missing", "Add app.build.json declaring the app architecture and supported validation profile.", AppBuildManifest.FileName);
        if (starter && output && !files.ContainsKey("tests/run.sh"))
            Add("validation-script-missing", "Add isolated POSIX validation at tests/run.sh, runnable with sh tests/run.sh.", "tests/run.sh");
        if (starter && output && !files.ContainsKey("tests/serve.sh"))
            Add("runtime-script-missing", "Add tests/serve.sh to serve the app on HTTP port 8080 for browser acceptance.", "tests/serve.sh");
        if (starter && output && manifest is not null &&
            (manifest.Architecture != "aspire" || manifest.Runtime?.Database != "postgres"))
            Add("app-shape-unsupported", "Every new app is an Aspire app with a PostgreSQL database: declare architecture \"aspire\" and runtime database \"postgres\".", AppBuildManifest.FileName);
        if (starter && output && manifest?.Architecture == "aspire")
        {
            foreach (string required in AspireLayout.Where(required => !files.ContainsKey(required)))
                Add("app-layout-missing", $"Add {required}; the platform builds and deploys AppHost/, Api/ and frontend/ at the workspace root.", required);
            if (files.TryGetValue("frontend/package.json", out string? packageJson) &&
                files.TryGetValue("frontend/package-lock.json", out string? packageLock) &&
                !FrontendLockMatches(packageJson, packageLock))
                Add("frontend-packages-changed", "Keep the provided frontend dependencies and package-lock.json unchanged; the offline build worker only has those exact packages.", "frontend/package.json");
            string appHost = string.Join('\n', files.Where(pair => pair.Key.StartsWith("AppHost/", StringComparison.Ordinal) &&
                pair.Key.EndsWith(".cs", StringComparison.Ordinal) && pair.Key != "AppHost/SquadHosting.cs").Select(pair => pair.Value));
            if (!files.ContainsKey("AppHost/SquadHosting.cs") ||
                !new[] { "AddSquadKubernetes(", "PublishAsSquadDatabase(", "PublishAsSquadApi(" }.All(call => appHost.Contains(call, StringComparison.Ordinal)))
                Add("squad-hosting-unused", "Keep AppHost/SquadHosting.cs and call AddSquadKubernetes(), PublishAsSquadDatabase() and PublishAsSquadApi() from the AppHost so the app can deploy into its namespace.", "AppHost/Program.cs");
            if (manifest.Runtime?.Components is { } components)
                InspectComponents(components, files, appHost, Add);
        }
        if (manifest is not null)
        {
            if (!capabilities.ValidationProfiles.Contains(manifest.ValidationProfile, StringComparer.Ordinal))
                Add("validation-profile-unsupported", "Choose a validation profile from the declared platform capabilities.", AppBuildManifest.FileName);
            if (starter && output && manifest.Runtime is null)
                Add("missing-runtime", "Declare runtime, healthPaths and observable browser journeys; provide tests/serve.sh for browser acceptance.", AppBuildManifest.FileName);
            // In cluster mode the build worker and the deployment's health checks verify the running
            // Aspire app and its PostgreSQL database; the host itself never runs them.
            bool clusterVerified = capabilities.ClusterBuild && manifest.Architecture == "aspire";
            if ((manifest.Runtime is not null || starter && output) && !capabilities.RuntimeAvailable && !clusterVerified)
                Add("runtime-unavailable", "App runtime/browser behavior cannot be verified because runtime execution is unavailable. Report the prerequisite blocker and preserve the requested architecture.", AppBuildManifest.FileName);
            if (manifest.Runtime is not null)
            {
                if (manifest.Runtime.Database == "postgres" && !capabilities.PostgresAvailable && !clusterVerified)
                    Add("postgres-unavailable", "PostgreSQL is unavailable. Report this prerequisite blocker; do not silently replace the database.", AppBuildManifest.FileName);
                if (!(starter && output) && !files.ContainsKey("tests/serve.sh"))
                    Add("runtime-script-missing", "Add tests/serve.sh to run workloads on HTTP port 8080 using the platform-provided data and connection settings.", "tests/serve.sh");
            }
        }

        var xml = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        foreach ((string path, string text) in files)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".csproj" or ".fsproj" or ".props")
            {
                try
                {
                    using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = MaxFileBytes
                    });
                    xml[path] = XDocument.Load(reader);
                }
                catch (XmlException)
                {
                    Add("project-invalid", "Correct the project or central property XML before validation.", path);
                }
            }
            if (extension == ".sh")
                InspectScript(path, text, issues);
            if (extension == ".cs")
            {
                if (manifest?.Architecture == "static" &&
                    Matches(text, @"\b(?:DistributedApplication|WebApplication)\s*\.\s*CreateBuilder\s*\(") &&
                    !IsTestPath(path))
                    Add("architecture-mismatch", "Declare server or aspire architecture for server/AppHost workloads; static preview must not replace their runtime acceptance.", path);
                if (Matches(text, @"\bnew\s+Uri\s*\(\s*origin\s*\)") &&
                    !Matches(text, @"(?:TryCreate\s*\(\s*origin|IsNullOr(?:WhiteSpace|Empty)\s*\(\s*origin|origin\s*\?\?)"))
                    Add("unsafe-origin", "Validate optional app origins with Uri.TryCreate before configuring URI/CORS; do not construct a URI from an unchecked origin.", path);
                InspectEndpoints(path, text, issues);
            }
            if (manifest?.Runtime is not null && IsBrowserSource(path) &&
                (Matches(text, """["'`]https?://(?:localhost|127\.0\.0\.1|\[::1\])(?=[:/"'`])""") ||
                 Path.GetFileName(path).StartsWith(".env", StringComparison.Ordinal) &&
                 Matches(text, """(?m)^\s*(?:VITE_|NEXT_PUBLIC_|PUBLIC_|REACT_APP_)[A-Z0-9_]+\s*=\s*["']?https?://(?:localhost|127\.0\.0\.1|\[::1\])(?=[:/\s]|$)""")))
                Add("browser-localhost", "Replace browser localhost URLs with relative same-origin API paths through the app gateway.", path);
            if (manifest?.Runtime is not null && IsBrowserSource(path) &&
                Matches(text, @"\b(?:localStorage|sessionStorage|indexedDB)\b|\bdocument\s*\.\s*cookie\b"))
                Add("browser-storage-unavailable", "Online apps run in a sandboxed page without localStorage, sessionStorage, IndexedDB or cookies. Keep state in the API/database or in memory.", path);
            if (manifest?.Architecture == "static" &&
                (extension is ".html" or ".css") &&
                Matches(text, """(?:\b(?:src|href|action)\s*=\s*["']/(?!/)|url\(\s*["']?/(?!/))"""))
                Add("static-root-link", "Use relative static asset and navigation links so the app works under the preview path prefix.", path);
        }
        InspectProjects(xml, manifest?.Architecture, issues);
        return issues.Distinct().ToArray();

        void Add(string code, string message, string? path = null) =>
            issues.Add(new(code, message, SafePath(path)));
    }

    private Dictionary<string, string> ReadArtifacts(List<AppBuildIssue> issues)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((workspace.RootPath, 0));
        int entries = 0, bytes = 0;
        try
        {
            if (File.GetAttributes(workspace.RootPath).HasFlag(FileAttributes.ReparsePoint))
            {
                issues.Add(new("artifact-unreadable", "The app workspace root must not be a symbolic link. Restore a separate app workspace and retry."));
                return files;
            }
            while (pending.Count > 0)
            {
                (string directory, int depth) = pending.Pop();
                foreach (string item in Directory.EnumerateFileSystemEntries(directory)
                    .Take(MaxEntries - entries + 1).Order(StringComparer.Ordinal))
                {
                    if (++entries > MaxEntries)
                        throw new InspectionLimitException();
                    string name = Path.GetFileName(item);
                    if (excluded.Contains(name)) continue;
                    string path = Path.GetRelativePath(workspace.RootPath, item).Replace('\\', '/');
                    string resolved;
                    try
                    {
                        resolved = workspace.PathPolicy.ResolveArtifactPath(path);
                        if (Directory.Exists(resolved))
                        {
                            if (depth >= 32) throw new InspectionLimitException();
                            pending.Push((resolved, depth + 1));
                            continue;
                        }
                        if (!extensions.Contains(Path.GetExtension(path)) && !name.StartsWith(".env", StringComparison.Ordinal))
                            continue;
                        if (files.Count >= MaxFiles) throw new InspectionLimitException();
                        using FileStream stream = File.OpenRead(resolved);
                        if (stream.Length > MaxFileBytes || stream.Length + bytes > MaxTotalBytes)
                            throw new InspectionLimitException();
                        byte[] buffer = new byte[(int)stream.Length];
                        stream.ReadExactly(buffer);
                        if (stream.ReadByte() != -1) throw new InspectionLimitException();
                        bytes += buffer.Length;
                        files.Add(path, Encoding.UTF8.GetString(buffer));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        issues.Add(new("artifact-unreadable", "An app artifact cannot be safely read. Remove symlinks or restore workspace access, then retry.", SafePath(path)));
                    }
                }
            }
        }
        catch (InspectionLimitException)
        {
            issues.Add(new("inspection-limit", "App preflight reached its artifact size/count limit. Reduce generated output or split large source files, then retry."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new("artifact-unreadable", "The app workspace cannot be fully read. Restore workspace access and retry."));
        }
        return files;
    }

    private void InspectProjects(Dictionary<string, XDocument> documents, string? architecture, List<AppBuildIssue> issues)
    {
        foreach ((string path, XDocument document) in documents)
        {
            IEnumerable<XElement> elements = document.Descendants();
            if (elements.Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .SelectMany(element => element.Value.Split(';'))
                .Select(value => Regex.Match(value.Trim(), @"^net(?:coreapp)?(?<major>\d+)\.", RegexOptions.CultureInvariant))
                .Any(match => match.Success && int.TryParse(match.Groups["major"].Value, out int major) && major > capabilities.DotnetMajor))
                issues.Add(new("sdk-target-mismatch", "Target a framework supported by the declared .NET SDK major; do not guess or upgrade the platform SDK.", SafePath(path)));

            bool appHost = Path.GetFileNameWithoutExtension(path).EndsWith("AppHost", StringComparison.OrdinalIgnoreCase) ||
                elements.Any(element => element.Name.LocalName == "IsAspireHost" && element.Value.Equals("true", StringComparison.OrdinalIgnoreCase)) ||
                document.Root?.Attribute("Sdk")?.Value.Contains("Aspire.AppHost", StringComparison.OrdinalIgnoreCase) == true;
            bool webSdk = (document.Root?.Attribute("Sdk")?.Value.Split(';') ?? [])
                .Concat(elements.Where(element => element.Name.LocalName == "Sdk")
                    .Select(element => element.Attribute("Name")?.Value ?? ""))
                .Any(sdk => sdk.Trim() == "Microsoft.NET.Sdk.Web" || sdk.Trim().StartsWith("Microsoft.NET.Sdk.Web/", StringComparison.Ordinal));
            if (architecture == "static" && (appHost || webSdk) && !IsTestPath(path))
                issues.Add(new("architecture-mismatch", "Declare server or aspire architecture for server/AppHost workloads; static preview must not replace their runtime acceptance.", SafePath(path)));
            if (appHost && elements.Any(element => element.Name.LocalName == "UserSecretsId" &&
                !Matches(element.Value, @"(?:^|[^a-zA-Z0-9])" + Regex.Escape(identity.Id) + @"(?:$|[^a-zA-Z0-9])")))
                issues.Add(new("unscoped-user-secrets", "Use a stable AppHost UserSecretsId scoped to this actual app ID, not a generic or shared template value.", SafePath(path)));

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var central = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            string[] ancestors = Ancestors(directory).Reverse().ToArray();
            foreach (string ancestor in ancestors)
            {
                foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props" })
                {
                    string candidate = ancestor.Length == 0 ? name : ancestor + "/" + name;
                    if (documents.TryGetValue(candidate, out XDocument? props))
                        ReadVersions(props, properties, central);
                }
            }
            ReadVersions(document, properties, central);
            var majors = new HashSet<int>();
            foreach (XElement reference in elements.Where(element =>
                element.Name.LocalName is "PackageReference" or "PackageVersion" &&
                !element.AncestorsAndSelf().Any(ancestor => ancestor.Attribute("Condition") is not null)))
            {
                string? package = reference.Attribute("Include")?.Value ?? reference.Attribute("Update")?.Value;
                if (package is null || !IsEfPackage(package)) continue;
                string? version = reference.Attribute("VersionOverride")?.Value ?? reference.Attribute("Version")?.Value ??
                    reference.Elements().FirstOrDefault(element => element.Name.LocalName == "Version")?.Value ??
                    central.GetValueOrDefault(package);
                for (int attempt = 0; version is not null && attempt < 8; attempt++)
                {
                    Match property = Regex.Match(version, @"^\$\(([^)]+)\)$", RegexOptions.CultureInvariant);
                    if (!property.Success) break;
                    version = properties.GetValueOrDefault(property.Groups[1].Value);
                }
                Match pinned = Regex.Match(version ?? "", @"^(?<major>\d+)\.\d+\.\d+(?:[-+][a-zA-Z0-9.-]+)?$", RegexOptions.CultureInvariant);
                if (pinned.Success && int.TryParse(pinned.Groups["major"].Value, out int major)) majors.Add(major);
            }
            if (majors.Count > 1)
                issues.Add(new("ef-provider-major-mismatch", "Pin EF Core and its EF database provider to compatible matching majors, including centrally managed versions.", SafePath(path)));
        }
    }

    private static void ReadVersions(XDocument document, Dictionary<string, string> properties, Dictionary<string, string> central)
    {
        foreach (XElement group in document.Descendants().Where(element => element.Name.LocalName == "PropertyGroup" && element.Attribute("Condition") is null))
            foreach (XElement property in group.Elements().Where(element => element.Attribute("Condition") is null))
                properties[property.Name.LocalName] = property.Value.Trim();
        foreach (XElement package in document.Descendants().Where(element => element.Name.LocalName == "PackageVersion" && element.Attribute("Condition") is null))
        {
            string? name = package.Attribute("Include")?.Value ?? package.Attribute("Update")?.Value;
            string? version = package.Attribute("Version")?.Value ?? package.Elements().FirstOrDefault(element => element.Name.LocalName == "Version")?.Value;
            if (name is not null && version is not null) central[name] = version.Trim();
        }
    }

    private static void InspectScript(string path, string text, List<AppBuildIssue> issues)
    {
        string executable = string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));
        if (Matches(executable, @"(?:^|[;&|\s])(?:pkill|killall)(?:\s|$)"))
            issues.Add(new("name-based-process-kill", "Remove pkill/killall. Stop only specific processes owned by the isolated test or let the platform own runtime shutdown.", SafePath(path)));
        if (Matches(WithoutHeredocBodies(executable), @"(?:(?:^|[\s;&|(!])\[\[\s|\s\]\](?=\s|;|&|\||\)|$)|<\(|>\(|&>|\$'|\bset\s+-[^\r\n]*\bpipefail\b|(?:^|\n)\s*(?:source\s|function\s)|\$\{[A-Za-z_]\w*(?:\[[^\]\r\n]*\])?//|(?:^|\n)\s*(?:declare\s+-a|[a-zA-Z_]\w*=\())"))
            issues.Add(new("non-posix-script", "Use portable POSIX sh syntax in scripts; replace bash-only conditionals, arrays, source, substitutions and pipefail.", SafePath(path)));
        var disposable = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match assignment in Regex.Matches(executable, @"(?m)^\s*(?:export\s+)?(?<name>[A-Z_]*TEST[A-Z_]*)=[""']?\$\(\s*mktemp\s+-d(?:\s|\))", RegexOptions.CultureInvariant))
            disposable.Add(assignment.Groups["name"].Value);
        foreach (string line in executable.Split('\n'))
        {
            if (!Matches(line, @"(?:^|[;&|\s])rm\s") ||
                !Matches(line, @"(?:\.(?:db|sqlite|sqlite3)(?:[\s""'*;]|$)|SQUAD_APP_DATA|/data(?:/|[\s""']|$))"))
                continue;
            bool isolated = disposable.Any(name =>
                Matches(line, @"[""']?\$(?:\{" + Regex.Escape(name) + @"\}|" + Regex.Escape(name) + @")/") &&
                !Matches(line, @"(?:SQUAD_APP_DATA|/data/|(?:^|/)\.\.(?:/|$)|(?:\s|[""'])[\w./-]+\.(?:db|sqlite|sqlite3)(?:[\s""';]|$))"));
            if (!isolated)
                issues.Add(new("destructive-validation", "Do not delete live database files. Run database validation and cleanup only inside a provably disposable test root.", SafePath(path)));
        }
        if (path == "tests/serve.sh" &&
            Matches(executable, @"(?:^|[;&|\s])(?:docker|aspire)\s|\bdotnet\s+run\b[^\r\n]*AppHost|\b(?:nohup|setsid)\s"))
            issues.Add(new("runtime-lifecycle-violation", "Run app workloads directly in tests/serve.sh using supplied connections/data; no nested Docker/Aspire/AppHost or detached host services.", path));
    }

    /// <summary>
    /// Heredoc bodies and single-quoted strings are data for another program (for example embedded
    /// Python), not shell syntax, so shell-dialect checks skip them. Safety checks still read the full script.
    /// </summary>
    private static string WithoutHeredocBodies(string script)
    {
        var kept = new StringBuilder();
        string? terminator = null;
        bool stripTabs = false;
        foreach (string line in script.Split('\n'))
        {
            if (terminator is not null)
            {
                if ((stripTabs ? line.TrimStart('\t') : line).TrimEnd('\r') == terminator) terminator = null;
                continue;
            }
            kept.Append(line).Append('\n');
            Match heredoc = Regex.Match(line, @"<<(?<strip>-?)\s*(?<quote>['""]?)(?<word>[A-Za-z_][A-Za-z0-9_]*)\k<quote>",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (heredoc.Success)
            {
                terminator = heredoc.Groups["word"].Value;
                stripTabs = heredoc.Groups["strip"].Value == "-";
            }
        }
        return WithoutSingleQuotedText(kept.ToString());
    }

    // Single-quoted text is literal in sh (often embedded Python, JSON or a grep pattern), so it is not
    // shell syntax. Track quoting so an apostrophe inside double quotes, a comment or after a backslash
    // does not start a quoted region and hide (or expose) the rest of the script.
    private static string WithoutSingleQuotedText(string script)
    {
        var kept = new StringBuilder(script.Length);
        bool single = false, @double = false, comment = false;
        for (int index = 0; index < script.Length; index++)
        {
            char current = script[index];
            if (comment)
            {
                if (current == '\n') comment = false;
                kept.Append(current);
            }
            else if (single)
            {
                if (current == '\'') { single = false; kept.Append(current); }
                else if (current == '\n') kept.Append(current);
            }
            else if (current == '\\' && index + 1 < script.Length)
            {
                kept.Append(current).Append(script[++index]);
            }
            else
            {
                if (current == '"') @double = !@double;
                else if (current == '\'' && !@double) single = true;
                else if (current == '#' && !@double && (index == 0 || char.IsWhiteSpace(script[index - 1]))) comment = true;
                kept.Append(current);
            }
        }
        return kept.ToString();
    }

    private static void InspectEndpoints(string path, string text, List<AppBuildIssue> issues)
    {
        foreach (Match chain in Regex.Matches(text, @"\.Add(?:NpmApp|JavaScriptApp|Project)\b[^;]+;", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            string resource = chain.Value;
            // AddProject can inherit HTTP endpoints from a launch profile without a fluent endpoint.
            if (resource.StartsWith(".AddProject", StringComparison.Ordinal) &&
                !Matches(resource, @"launchProfileName\s*:\s*null"))
                continue;
            if (Matches(resource, """\.WithEnvironment\s*\(\s*"(?:PORT|ASPNETCORE_HTTP_PORTS)"\s*,[\s\S]*(?:GetEndpoint|EndpointProperty\.Port)""") &&
                !Matches(resource, @"\.WithHttpEndpoint\s*\(|\.WithEndpoint\s*\(\s*""http""\s*,|\.WithEndpoint\s*\([^;]*scheme\s*:\s*""http"""))
                issues.Add(new("apphost-http-endpoint-missing", "Declare an explicit HTTP endpoint for the AppHost web resource before wiring its dynamic port.", SafePath(path)));
        }
    }

    private const string ClusterBuildNote =
        "Cluster mode: the platform's offline build worker and the app's deployment health checks verify " +
        "the running Aspire app and PostgreSQL after each wave. Do not report runtime or PostgreSQL " +
        "availability as blockers; keep tests/run.sh to offline checks (dotnet build, npm run build, unit tests).";

    /// <summary>The frontend's declared dependencies must be exactly the locked root dependencies.</summary>
    /// <summary>Every declared component must exist on disk and in the AppHost, since the build worker builds exactly these.</summary>
    private static void InspectComponents(
        AppComponents components, IReadOnlyDictionary<string, string> files, string appHost, Action<string, string, string> add)
    {
        string compact = System.Text.RegularExpressions.Regex.Replace(appHost, @"\s+", "");
        int Count(string call) => System.Text.RegularExpressions.Regex.Matches(appHost, System.Text.RegularExpressions.Regex.Escape(call)).Count;
        foreach (AppProjectComponent project in components.Projects)
        {
            string csproj = $"{project.Path}/{project.Path}.csproj";
            if (!files.ContainsKey(csproj))
                add("component-missing", $"Add {csproj} for the \"{project.Name}\" project declared in runtime.components (copy Api/Api.csproj; keep its packages).", csproj);
            if (!compact.Contains($"AddProject<Projects.{project.Path}>(\"{project.Name}\")", StringComparison.Ordinal))
                add("component-not-hosted", $"Add builder.AddProject<Projects.{project.Path}>(\"{project.Name}\") to the AppHost, as declared in runtime.components.", "AppHost/Program.cs");
        }
        if (Count("PublishAsSquadApi(") < components.Projects.Count)
            add("squad-hosting-unused", "Call PublishAsSquadApi() on every project in the AppHost so each one deploys into the app's namespace.", "AppHost/Program.cs");
        files.TryGetValue("frontend/package.json", out string? mainPackage);
        foreach (AppFrontendComponent frontend in components.Frontends.Where(item => item.Path != "frontend"))
        {
            if (!files.TryGetValue($"{frontend.Path}/package.json", out string? package) ||
                !files.TryGetValue($"{frontend.Path}/package-lock.json", out string? packageLock))
                add("component-missing", $"Add {frontend.Path}/package.json and package-lock.json for the \"{frontend.Name}\" front end (copy them from frontend/).", $"{frontend.Path}/package.json");
            else if (!FrontendLockMatches(package, packageLock) || mainPackage is null || !SameDependencies(package, mainPackage))
                add("frontend-packages-changed", $"Give {frontend.Path}/ exactly the packages and package-lock.json of frontend/; the offline build worker only has those.", $"{frontend.Path}/package.json");
        }
        foreach (AppFrontendComponent frontend in components.Frontends)
            if (!compact.Contains($"AddViteApp(\"{frontend.Name}\",\"../{frontend.Path}\")", StringComparison.Ordinal))
                add("component-not-hosted", $"Add builder.AddViteApp(\"{frontend.Name}\", \"../{frontend.Path}\") to the AppHost and publish it into the api project at {frontend.Mount}.", "AppHost/Program.cs");
        foreach (string server in components.Servers)
            if (!compact.Contains($"AddPostgres(\"{server}\")", StringComparison.Ordinal))
                add("component-not-hosted", $"Add builder.AddPostgres(\"{server}\").PublishAsSquadDatabase() to the AppHost, as declared in runtime.components.", "AppHost/Program.cs");
        if (Count("PublishAsSquadDatabase(") < components.Servers.Count)
            add("squad-hosting-unused", "Call PublishAsSquadDatabase() on every PostgreSQL server in the AppHost so its data lives on a volume.", "AppHost/Program.cs");
        foreach (AppDatabaseComponent database in components.Databases)
            if (!compact.Contains($"AddDatabase(\"{database.Name}\")", StringComparison.Ordinal))
                add("component-not-hosted", $"Add .AddDatabase(\"{database.Name}\") on the \"{database.Server}\" server in the AppHost, as declared in runtime.components.", "AppHost/Program.cs");
    }

    private static bool SameDependencies(string packageJson, string otherPackageJson)
    {
        try
        {
            using var one = System.Text.Json.JsonDocument.Parse(packageJson);
            using var two = System.Text.Json.JsonDocument.Parse(otherPackageJson);
            foreach (string section in new[] { "dependencies", "devDependencies" })
            {
                string Read(System.Text.Json.JsonElement element) =>
                    element.TryGetProperty(section, out var values) && values.ValueKind == System.Text.Json.JsonValueKind.Object
                        ? string.Join(';', values.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal).Select(item => $"{item.Name}={item.Value}"))
                        : "";
                if (Read(one.RootElement) != Read(two.RootElement)) return false;
            }
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool FrontendLockMatches(string packageJson, string packageLock)
    {
        try
        {
            using var package = System.Text.Json.JsonDocument.Parse(packageJson);
            using var lockFile = System.Text.Json.JsonDocument.Parse(packageLock);
            if (!lockFile.RootElement.TryGetProperty("packages", out var packages) ||
                !packages.TryGetProperty("", out var root))
                return false;
            foreach (string section in new[] { "dependencies", "devDependencies" })
            {
                Dictionary<string, string?> Read(System.Text.Json.JsonElement element) =>
                    element.TryGetProperty(section, out var values) && values.ValueKind == System.Text.Json.JsonValueKind.Object
                        ? values.EnumerateObject().ToDictionary(item => item.Name, item => (string?)item.Value.ToString(), StringComparer.Ordinal)
                        : new(StringComparer.Ordinal);
                Dictionary<string, string?> declared = Read(package.RootElement), locked = Read(root);
                if (declared.Count != locked.Count || declared.Any(pair => !locked.TryGetValue(pair.Key, out string? version) || version != pair.Value))
                    return false;
            }
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool IsEfPackage(string name) =>
        name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Pomelo.EntityFrameworkCore.MySql", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MySql.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Oracle.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase);

    private static bool IsAppOutput(string path) =>
        Path.GetFileName(path).Equals("index.html", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(path) is "package.json" or "server.js" or "server.mjs" or "server.py" or "app.py" or "pyproject.toml" ||
        path.StartsWith("src/", StringComparison.Ordinal) &&
            Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".fs" or ".py" or ".js" or ".ts" or ".tsx" or ".jsx";

    private static bool IsBrowserSource(string path)
    {
        string[] segments = path.Split('/');
        if (segments.Any(segment => segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("test", StringComparison.OrdinalIgnoreCase) || segment is "__tests__" or "scripts" or "server" or "api" or "AppHost"))
            return false;
        string name = Path.GetFileName(path);
        if (Matches(name, @"(?:^|\.)(?:test|spec)\.") ||
            Matches(name, @"^(?:vite|vitest|webpack|rollup|next|eslint|postcss|tailwind|playwright)\.config\.") ||
            name.StartsWith("server.", StringComparison.OrdinalIgnoreCase))
            return false;
        return Path.GetExtension(path).ToLowerInvariant() is ".html" or ".jsx" or ".tsx" ||
            Path.GetExtension(path).ToLowerInvariant() is ".js" or ".mjs" or ".ts" &&
                (segments.Any(segment => segment is "src" or "public" or "web" or "client" or "frontend") || !path.Contains('/')) ||
            name is "config.json" or "runtime-config.json" ||
            name.StartsWith(".env", StringComparison.Ordinal) && segments.Any(segment => segment is "web" or "client" or "frontend");
    }

    private static bool IsTestPath(string path) =>
        path.Split('/').Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("tests", StringComparison.OrdinalIgnoreCase) || segment == "__tests__") ||
        Matches(Path.GetFileName(path), @"(?:^|\.)(?:test|spec)\.");

    private static IEnumerable<string> Ancestors(string directory)
    {
        while (directory.Length > 0)
        {
            yield return directory;
            int slash = directory.LastIndexOf('/');
            directory = slash < 0 ? "" : directory[..slash];
        }
        yield return "";
    }

    private static string? SafePath(string? path) =>
        path is { Length: <= 200 } && path.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '.' or '-' or '_')
            ? path : null;

    private static bool Matches(string text, string pattern) =>
        Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private sealed class InspectionLimitException : Exception;
}
