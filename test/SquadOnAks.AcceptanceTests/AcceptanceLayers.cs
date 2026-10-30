using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

internal interface IAcceptanceLayer
{
    Task RunAsync();
}

internal sealed class DeterministicAcceptanceLayer : IAcceptanceLayer
{
    public async Task RunAsync()
    {
        string repositoryRoot = Paths.FindRepositoryRoot();
        string artifactRoot = Path.Combine(repositoryRoot, "artifacts", "playwright", "deterministic");
        Directory.CreateDirectory(artifactRoot);

        int port = Ports.Reserve();
        await using AcceptanceHostProcess host = await AcceptanceHostProcess.StartAsync(
            repositoryRoot,
            $"http://127.0.0.1:{port}");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });

        DeterministicScenarios scenarios = new(browser, host.BaseUrl, artifactRoot);
        await scenarios.RunAsync();
    }
}

internal sealed class LifecycleBrowserAcceptanceLayer : IAcceptanceLayer
{
    public async Task RunAsync()
    {
        string root = Paths.FindRepositoryRoot();
        string artifacts = Path.Combine(root, ".playwright-mcp", "daydream");
        Directory.CreateDirectory(artifacts);
        int port = Ports.Reserve();
        await using AcceptanceHostProcess host = await AcceptanceHostProcess.StartAsync(
            root, $"http://127.0.0.1:{port}", realLifecycle: true);
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER_CHANNEL")
        });
        await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 } });
        IPage page = await context.NewPageAsync();
        List<string> errors = [];
        page.PageError += (_, error) => errors.Add(error);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        try
        {
            await page.AddInitScriptAsync("""
                if (window.top === window && !sessionStorage.getItem('seeded-once')) {
                sessionStorage.setItem('seeded-once', 'true');
                sessionStorage.setItem('daydream:conversation', JSON.stringify({
                    generation:'old-generation', threadId:'old-thread',
                    messages:[{role:'user',content:'Old idea'}], idea:'Old idea'
                }));
                }
                """);
            await page.GotoAsync(host.BaseUrl.ToString());
            await Assertions.Expect(page.Locator("#team-status")).ToHaveTextAsync("A little possibility");
            await Assertions.Expect(page).ToHaveTitleAsync("Daydream — Make room for your idea");
            await Assertions.Expect(page.Locator("#library")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#app-library > li")).ToHaveCountAsync(2);
            await Assertions.Expect(page.Locator("#new-app")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#idea-heading")).ToContainTextAsync("What do you want to make?");
            await VerifySurfaceAsync(page);
            await CaptureAsync(page, artifacts, "first-run");
            await Assertions.Expect(page.Locator("#theme-toggle")).ToHaveAttributeAsync("aria-label", "Switch to night mode");
            await page.Locator("#theme-toggle").ClickAsync();
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
            await Assertions.Expect(page.Locator("#theme-toggle")).ToHaveAttributeAsync("aria-label", "Switch to day mode");
            Require(await page.EvaluateAsync<string>("getComputedStyle(document.documentElement).backgroundColor") == "rgb(21, 20, 31)",
                "Night mode must repaint the page.");
            await CaptureAsync(page, artifacts, "first-run-night");
            await page.Locator("#theme-toggle").ClickAsync();
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
            await page.Locator("#send-casting-turn").ClickAsync();
            await Assertions.Expect(page.Locator("#casting-prompt")).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(page.Locator("#casting-prompt")).ToBeFocusedAsync();
            await page.GetByLabel("Tell us what you’re imagining").FillAsync("Maintain this route-planning service.");
            await page.Locator("#casting-prompt").PressAsync("Control+Enter");
            await Assertions.Expect(page.Locator("#squad-members li")).ToHaveCountAsync(3);
            await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("GNC");
            await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("Squad lead");
            await Assertions.Expect(page.Locator("#cast-squad")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Route Buddy");
            await Assertions.Expect(page.Locator("#library-app-name")).ToHaveTextAsync("Route Buddy");
            using var client = new HttpClient { BaseAddress = host.BaseUrl };
            using (JsonDocument files = JsonDocument.Parse(await client.GetStringAsync("/workspace/files")))
                Require(!files.RootElement.EnumerateArray().Any(file => file.GetProperty("path").GetString() == ".squad/team.md"),
                    "A proposal must not cast the squad before explicit confirmation.");
            await page.GetByLabel("Tell us what you’re imagining").FillAsync("Refine the proposal so GNC owns implementation.");
            await page.Locator("#send-casting-turn").ClickAsync();
            await Assertions.Expect(page.Locator("#send-casting-turn")).ToBeEnabledAsync();
            await page.Locator("#cast-squad").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#team-status")).ToHaveTextAsync("Your squad is ready");
            await Assertions.Expect(page.Locator("#casting-controls")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#work-request")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("GNC");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#work-request")).ToHaveAttributeAsync("aria-invalid", "true");
            await page.GetByLabel("What would you like your squad to do next?").FillAsync("Repair the known fixture defect with a test.");
            await page.Locator("#work-request").PressAsync("Control+Enter");
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Delivered", new() { Timeout = 30000 });
            await Assertions.Expect(page.Locator("#wave-proof")).ToContainTextAsync("everything held up");
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator("#wave-date")).Not.ToBeEmptyAsync();

            await Assertions.Expect(page.Locator("#open-app")).ToHaveAttributeAsync("aria-disabled", "false");
            await Assertions.Expect(page.Locator("#app-preview")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#wave-try")).ToBeVisibleAsync();
            IPage preview = await context.RunAndWaitForPageAsync(() => page.Locator("#wave-try").ClickAsync());
            await preview.WaitForLoadStateAsync();
            await Assertions.Expect(preview).ToHaveTitleAsync("Your route planner");
            await preview.CloseAsync();
            await VerifySurfaceAsync(page);
            await CaptureAsync(page, artifacts, "active-app");
            await ArtifactPublicationScenario.RunAsync(page, artifacts);

            await page.EvaluateAsync("""
                window.statusMutations = 0;
                window.statusObserver = new MutationObserver(records => window.statusMutations += records.length);
                for (const id of ['team-status', 'wave-result'])
                    window.statusObserver.observe(document.getElementById(id), { childList: true, characterData: true, subtree: true });
                """);
            await page.WaitForTimeoutAsync(2300);
            Require(await page.EvaluateAsync<int>("window.statusMutations") == 0,
                "Unchanged polling must not repeatedly announce the same status to screen readers.");
            await page.EvaluateAsync("window.statusObserver.disconnect()");
            await page.GetByLabel("What would you like your squad to do next?").FillAsync("A contextual follow-up.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(2);
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Delivered");
            await page.Locator("[data-wave='1']").ClickAsync();
            await Assertions.Expect(page.Locator("#evidence-heading")).ToHaveTextAsync("Wave 1");
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(2);
            await page.Locator("[data-wave='1']").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#evidence-heading")).ToHaveTextAsync("Wave 1");
            await Assertions.Expect(page.Locator("[data-wave='1']")).ToBeFocusedAsync();
            await page.GetByLabel("What would you like your squad to do next?").FillAsync("Start long-running work.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#cancel-work")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("In the making");
            await Assertions.Expect(page.Locator("#viewing-window")).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(page.Locator("#preview-status")).ToHaveTextAsync("Your squad is building…");
            await VerifySurfaceAsync(page);
            await CaptureAsync(page, artifacts, "building");
            await page.Locator("#cancel-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Stopped", new() { Timeout = 30000 });
            await Assertions.Expect(page.Locator("#viewing-window")).ToHaveAttributeAsync("aria-busy", "false");
            await page.GetByLabel("What would you like your squad to do next?").FillAsync("Exercise visible failure.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Needs another try", new() { Timeout = 30000 });
            await Assertions.Expect(page.Locator("#wave-result")).ToContainTextAsync("Try a smaller request");
            await Assertions.Expect(page.Locator("#work-request")).ToBeEnabledAsync();
            await VerifySurfaceAsync(page);
            await VerifyManyAppsAsync(page, context, client, artifacts);
            Require(errors.Count == 0, "Browser errors: " + string.Join("; ", errors));
            Console.WriteLine("lifecycle-browser: Daydream many-app library, isolated squads and waves, independent live previews, casting, human evidence, publication keyboard paths, cancellation, failure, persistence and responsive layout passed");
        }
        catch
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "failure.png"), FullPage = true });
            throw;
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(artifacts, "lifecycle.zip") });
        }
    }

    private static async Task VerifyManyAppsAsync(IPage page, IBrowserContext context, HttpClient client, string artifacts)
    {
        string firstPreview = await page.Locator("#open-app").GetAttributeAsync("href") ?? throw new InvalidOperationException("First preview missing.");
        await page.Locator("#new-app").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("What do you want to make?");
        string secondId = new Uri(page.Url).Query.Split("app=")[1].Split('&')[0];
        await Assertions.Expect(page.Locator("#app-library > li")).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#squad-board")).ToBeHiddenAsync();
        await page.Locator("#casting-prompt").FillAsync("Plan my garden.");
        await page.Locator("#send-casting-turn").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
        await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("FERN");
        await page.Locator("#cast-squad").ClickAsync();
        await Assertions.Expect(page.Locator("#team-status")).ToHaveTextAsync("Your squad is ready");
        await page.Locator("#work-request").FillAsync("Repair the known fixture defect and welcome gardeners.");
        await page.Locator("#send-work").ClickAsync();
        await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Delivered");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
        string secondPreview = await page.Locator("#open-app").GetAttributeAsync("href") ?? throw new InvalidOperationException("Second preview missing.");
        Require(firstPreview != secondPreview, "Every app needs its own live address.");
        Require(await page.Locator("#download-zip").GetAttributeAsync("href") == $"/apps/{secondId}/workspace/export.zip",
            "Downloads must follow the selected app.");
        IPage second = await context.RunAndWaitForPageAsync(() => page.Locator("#open-app").ClickAsync());
        await second.WaitForLoadStateAsync();
        await Assertions.Expect(second).ToHaveTitleAsync("Your garden");
        IPage first = await context.NewPageAsync();
        await first.GotoAsync(firstPreview);
        await Assertions.Expect(first).ToHaveTitleAsync("Your route planner");
        using (HttpResponseMessage asset = await client.GetAsync(new Uri(new Uri(secondPreview), "style.css")))
            Require(asset.IsSuccessStatusCode, "Per-app preview assets must be served.");
        using (HttpResponseMessage boundary = await client.GetAsync(new Uri(new Uri(secondPreview), "casting/status")))
            Require(boundary.StatusCode == HttpStatusCode.NotFound, "The preview listener must not expose Daydream's APIs.");
        await page.Locator("[data-app='first']").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Route Buddy");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(4);
        await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("GNC");
        await Assertions.Expect(page.Locator("#squad-members")).Not.ToContainTextAsync("FERN");
        await Assertions.Expect(page.Locator("#open-app")).ToHaveAttributeAsync("href", firstPreview);
        await page.Locator("[data-wave='1']").ClickAsync();
        await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Delivered");
        await page.Locator($"[data-app='{secondId}']").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
        await Assertions.Expect(page.Locator("#squad-members")).ToContainTextAsync("FERN");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
        await VerifySurfaceAsync(page);
        await CaptureAsync(page, artifacts, "many-apps");
        await first.ReloadAsync();
        await Assertions.Expect(first).ToHaveTitleAsync("Your route planner");
        await second.ReloadAsync();
        await Assertions.Expect(second).ToHaveTitleAsync("Your garden");
        await first.CloseAsync();
        await second.CloseAsync();
        Console.WriteLine("PASS two apps: independent squads, reopenable waves, switching, reload, downloads, simultaneous live previews and preview API isolation");
    }

    private static async Task CaptureAsync(IPage page, string artifacts, string state)
    {
        await page.SetViewportSizeAsync(1440, 900);
        await page.EvaluateAsync("window.scrollTo(0, 0)");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, state + "-desktop.png"), FullPage = true });
        await page.SetViewportSizeAsync(390, 844);
        await page.EvaluateAsync("window.scrollTo(0, 0)");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, state + "-mobile.png"), FullPage = true });
        await page.SetViewportSizeAsync(1440, 900);
    }

    private static async Task VerifySurfaceAsync(IPage page)
    {
        await Assertions.Expect(page.Locator("pre, code, .file-tree, .file-viewer, .application-diff, #review-content, #raw-file")).ToHaveCountAsync(0);
        string visible = await page.Locator("body").InnerTextAsync();
        Require(!System.Text.RegularExpressions.Regex.IsMatch(visible,
            @"\b(AKS|Kubernetes|cluster|pod|container|instance|Squad on AKS|git|commit|branch|repository|diff|PR|deploy|workspace)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase), "Technical vocabulary must never reach the interface.");
        foreach (int width in new[] { 1440, 390, 360 })
        {
            await page.SetViewportSizeAsync(width, 900);
            Require(!await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth"), $"No horizontal page overflow at {width}px.");
            Require(!await page.EvaluateAsync<bool>("""
                [...document.querySelectorAll('main *')].some(el => {
                    const style = getComputedStyle(el);
                    return ['auto','scroll'].includes(style.overflowY) && el.scrollHeight > el.clientHeight + 1;
                })
                """), "App panels must scroll naturally with the page.");
        }
        await page.SetViewportSizeAsync(1440, 900);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class LiveAcceptanceLayer(string name, string gateVariable, string? baseUrl)
    : IAcceptanceLayer
{
    public async Task RunAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(gateVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name} is opt-in. Set {gateVariable}=1 explicitly; default validation never runs live models or Azure.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? target))
        {
            throw new InvalidOperationException(
                $"{name} requires --base-url or SQUAD_ACCEPTANCE_BASE_URL from Aspire/deployment discovery.");
        }

        using HttpClient client = new();
        using HttpResponseMessage rootResponse = await client.GetAsync(target);
        rootResponse.EnsureSuccessStatusCode();
        string rootContent = await rootResponse.Content.ReadAsStringAsync();
        if (!rootContent.Contains("Squad on AKS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name} root response did not contain the expected Squad on AKS marker.");
        }

        foreach (string healthPath in new[] { "/health/live", "/health/ready" })
        {
            using HttpResponseMessage healthResponse = await client.GetAsync(new Uri(target, healthPath));
            healthResponse.EnsureSuccessStatusCode();
        }

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(target.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
            .ToContainTextAsync("Squad on AKS");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Work", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Artifacts", Exact = true }))
            .ToBeVisibleAsync();
        Console.WriteLine($"{name}: root, health, and browser contracts passed at {target}");
    }
}

internal sealed class NativeCastingAcceptanceLayer(string? baseUrl) : IAcceptanceLayer
{
    public async Task RunAsync()
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? target))
        {
            throw new InvalidOperationException(
                "native-casting requires --base-url from local Aspire endpoint discovery.");
        }

        string repositoryRoot = Paths.FindRepositoryRoot();
        string artifactRoot = Path.Combine(repositoryRoot, "artifacts", "playwright", "native-casting");
        Directory.CreateDirectory(artifactRoot);

        using HttpClient client = new() { BaseAddress = target };
        await RequireNoHarnessFilesAsync(client, "startup");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync();
        await context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(180_000);
        try
        {
            await page.GotoAsync(target.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync(
                new System.Text.RegularExpressions.Regex("^(Needs cast|Casting)$"));
            await Assertions.Expect(page.Locator("#send-work"))
                .ToBeDisabledAsync();

            await page.GetByLabel("What will this Squad do?").FillAsync(
                "Maintain this route-planning shell service. Implement small changes, test them, and review every result.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Propose cast" }).ClickAsync();
            await Assertions.Expect(page.Locator("#send-casting-turn"))
                .ToHaveTextAsync("Refine proposal", new() { Timeout = 180_000 });
            await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Cast conversation" })
                .Locator(":scope > li")).ToHaveCountAsync(2);
            await RequireNoHarnessFilesAsync(client, "proposal");

            await SendRefinementAsync(
                page,
                "Refine the proposal so one specialist owns shell integration tests. Do not create files.");
            await RequireNoHarnessFilesAsync(client, "first refinement");
            await SendRefinementAsync(
                page,
                "Refine it again so the reviewer explicitly checks accessibility and error messages. Do not create files.");
            await RequireNoHarnessFilesAsync(client, "second refinement");

            await page.GetByRole(AriaRole.Button, new() { Name = "Confirm this cast" }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync(
                "Cast review",
                new() { Timeout = 180_000 });

            WorkspaceDiagnostics diagnostics = await GetDiagnosticsAsync(client);
            Require(
                diagnostics.HarnessFiles.Contains(".squad/team.md"),
                "Native confirmation did not generate team.md.");
            Require(
                diagnostics.HarnessFiles.Contains(".squad/routing.md"),
                "Native confirmation did not generate routing.md.");
            Require(
                diagnostics.HarnessFiles.Any(path => path.EndsWith("/charter.md", StringComparison.Ordinal)),
                "Native confirmation did not generate charters.");
            Require(
                diagnostics.HarnessFiles.Any(path => path.EndsWith("/history.md", StringComparison.Ordinal)),
                "Native confirmation did not generate histories.");
            Require(
                diagnostics.HarnessFiles.Any(path => path.StartsWith(".squad/casting/", StringComparison.Ordinal)),
                "Native confirmation did not generate casting state.");

            IReadOnlyList<string> options = await page.GetByLabel("Native file")
                .Locator("option")
                .AllTextContentsAsync();
            string[] reviewOptions = options.ToArray();
            Require(
                reviewOptions.Contains(".squad/team.md") &&
                reviewOptions.Contains(".squad/routing.md") &&
                reviewOptions.Any(path => path.EndsWith("/charter.md", StringComparison.Ordinal)),
                "Cast review did not expose the required native Markdown.");
            Require(
                reviewOptions.All(path =>
                    path is ".squad/team.md" or ".squad/routing.md" ||
                    path.EndsWith("/charter.md", StringComparison.Ordinal)),
                "Cast review exposed a non-allow-listed file.");

            using HttpResponseMessage rejected = await client.PutAsJsonAsync(
                "/casting/review",
                new { path = "../outside.md", content = "escape" });
            Require(rejected.StatusCode == HttpStatusCode.BadRequest, "Traversal edit was not rejected.");
            using HttpResponseMessage nonAllowlisted = await client.PutAsJsonAsync(
                "/casting/review",
                new { path = ".squad/agents/example/history.md", content = "not editable" });
            Require(
                nonAllowlisted.StatusCode == HttpStatusCode.BadRequest,
                "Non-allow-listed native edit was not rejected.");

            await page.GetByLabel("Native file").SelectOptionAsync(".squad/routing.md");
            string routing = await page.GetByLabel("Markdown").InputValueAsync();
            await page.GetByLabel("Markdown").FillAsync("");
            await page.GetByRole(AriaRole.Button, new() { Name = "Save review edit" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Validation errors" }))
                .ToContainTextAsync("routing.md is missing or empty.");
            await Assertions.Expect(page.Locator("#send-work"))
                .ToBeDisabledAsync();

            await page.GetByLabel("Markdown").FillAsync(routing);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save review edit" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Ready");
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send request" }))
                .ToBeEnabledAsync();

            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactRoot, "ready.png"),
                FullPage = true
            });
            Console.WriteLine(
                $"native-casting: browser proposal, two refinements, confirmation, review, validation, and activation passed at {target}");
        }
        finally
        {
            await context.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactRoot, "native-casting.zip")
            });
        }
    }

    private static async Task SendRefinementAsync(IPage page, string refinement)
    {
        int before = await page.GetByRole(AriaRole.List, new() { Name = "Cast conversation" })
            .Locator(":scope > li")
            .CountAsync();
        await page.GetByLabel("What will this Squad do?").FillAsync(refinement);
        await page.GetByRole(AriaRole.Button, new() { Name = "Refine proposal" }).ClickAsync();
        await Assertions.Expect(page.Locator("#send-casting-turn"))
            .ToHaveTextAsync("Refine proposal", new() { Timeout = 180_000 });
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Cast conversation" })
                .Locator(":scope > li"))
            .ToHaveCountAsync(before + 2);
    }

    private static async Task RequireNoHarnessFilesAsync(HttpClient client, string phase)
    {
        WorkspaceDiagnostics diagnostics = await GetDiagnosticsAsync(client);
        Require(
            diagnostics.HarnessFiles.Length == 0,
            $"{phase} wrote native roster files before confirmation: {string.Join(", ", diagnostics.HarnessFiles)}");
    }

    private static async Task<WorkspaceDiagnostics> GetDiagnosticsAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/diagnostics/workspace");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDiagnostics>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Workspace diagnostics response was empty.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record WorkspaceDiagnostics(string[] HarnessFiles);
}

internal sealed class AcceptanceHostProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task stdout;
    private readonly Task stderr;

    private AcceptanceHostProcess(Process process, Uri baseUrl, Task stdout, Task stderr)
    {
        this.process = process;
        BaseUrl = baseUrl;
        this.stdout = stdout;
        this.stderr = stderr;
    }

    public Uri BaseUrl { get; }

    public static async Task<AcceptanceHostProcess> StartAsync(
        string repositoryRoot,
        string url,
        bool realLifecycle = false)
    {
        string project = Path.Combine(
            repositoryRoot,
            "test",
            realLifecycle ? "SquadOnAks.AguiSmoke" : "SquadOnAks.AcceptanceHost",
            realLifecycle ? "SquadOnAks.AguiSmoke.csproj" : "SquadOnAks.AcceptanceHost.csproj");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new DirectoryNotFoundException("Could not determine the acceptance build configuration."));
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        if (realLifecycle)
        {
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--serve-browser");
        }
        startInfo.ArgumentList.Add("--urls");
        if (realLifecycle)
        {
            int previewPort = Ports.Reserve();
            startInfo.ArgumentList.Add(url + $";http://127.0.0.1:{previewPort}");
            startInfo.ArgumentList.Add("--SQUAD_PREVIEW_PORT");
            startInfo.ArgumentList.Add(previewPort.ToString());
            startInfo.ArgumentList.Add("--SQUAD_PREVIEW_URL");
            startInfo.ArgumentList.Add($"http://127.0.0.1:{previewPort}");
        }
        else startInfo.ArgumentList.Add(url);
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";

        Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start the deterministic acceptance host.");

        Task stdout = DrainAsync(process.StandardOutput, Console.Out);
        Task stderr = DrainAsync(process.StandardError, Console.Error);
        Uri baseUrl = new(url);

        using HttpClient client = new();
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Acceptance host exited before becoming ready (exit code {process.ExitCode}).");
            }

            try
            {
                string readinessPath = realLifecycle ? "/work/status" : "/harness/state";
                using HttpResponseMessage response = await client.GetAsync(new Uri(baseUrl, readinessPath));
                if (response.StatusCode is HttpStatusCode.OK)
                {
                    return new(process, baseUrl, stdout, stderr);
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100);
        }

        process.Kill(entireProcessTree: true);
        throw new TimeoutException("Acceptance host did not become ready.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        await Task.WhenAll(stdout, stderr);
        process.Dispose();
    }

    private static async Task DrainAsync(StreamReader reader, TextWriter writer)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            await writer.WriteLineAsync(line);
        }
    }
}

internal static class Paths
{
    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SquadOnAks.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SquadOnAks.slnx.");
    }
}

internal static class Ports
{
    public static int Reserve()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
