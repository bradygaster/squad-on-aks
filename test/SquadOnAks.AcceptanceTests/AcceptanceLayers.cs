using System.Diagnostics;
using System.IO.Compression;
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
        string workspace = Path.Combine(Path.GetTempPath(), "daydream-library-browser", Guid.NewGuid().ToString("N"));
        await using AcceptanceHostProcess host = await AcceptanceHostProcess.StartAsync(
            root, $"http://127.0.0.1:{port}", realLifecycle: true, workspacePath: workspace);
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER_CHANNEL")
        });
        await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 } });
        IPage page = await context.NewPageAsync();
        List<string> errors = [];
        List<string> rootWrites = [];
        page.PageError += (_, error) => errors.Add(error);
        page.Request += (_, request) =>
        {
            if (request.Method != "GET" && new Uri(page.Url).Query.Length == 0)
                rootWrites.Add(request.Url);
        };
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
            await Assertions.Expect(page.Locator("#team-status")).ToHaveTextAsync("All apps");
            await Assertions.Expect(page).ToHaveTitleAsync("Squad — Apps");
            await Assertions.Expect(page.Locator("#library")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#app-library > li")).ToHaveCountAsync(2);
            await Assertions.Expect(page.Locator("#new-app")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#project-view")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#library-heading")).ToHaveTextAsync("Apps");
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Your apps", Exact = true })).ToHaveCountAsync(0);
            await VerifySurfaceAsync(page);
            await CaptureAsync(page, artifacts, "first-run");
            await Assertions.Expect(page.Locator("#theme-toggle")).ToHaveAttributeAsync("aria-label", "Switch to dark theme");
            await page.Locator("#theme-toggle").ClickAsync();
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
            await Assertions.Expect(page.Locator("#theme-toggle")).ToHaveAttributeAsync("aria-label", "Switch to light theme");
            Require(await page.EvaluateAsync<string>("getComputedStyle(document.documentElement).backgroundColor") == "rgb(13, 17, 23)",
                "Dark theme must repaint the page.");
            await CaptureAsync(page, artifacts, "first-run-night");
            await page.Locator("#theme-toggle").ClickAsync();
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
            Require(rootWrites.Count == 0, "Opening the neighborhood must not select an app or start work.");
            await page.Locator("[data-app='first']").ClickAsync();
            await Assertions.Expect(page.Locator("#library")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#idea-heading")).ToContainTextAsync("New app");
            await page.Locator("#send-casting-turn").ClickAsync();
            await Assertions.Expect(page.Locator("#casting-prompt")).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(page.Locator("#casting-prompt")).ToBeFocusedAsync();
            await page.GetByLabel("Describe your app").FillAsync("Maintain this route-planning service. Repair the known fixture defect with a test.");
            await page.Locator("#casting-prompt").PressAsync("Control+Enter");
            await Assertions.Expect(page.Locator("#squad-board, #squad-members, #cast-squad")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#team-members li")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#startup-question")).ToHaveTextAsync("Who will use this app?");
            await Assertions.Expect(page.Locator("#build-app")).ToBeDisabledAsync();
            await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Route Buddy");
            using var client = new HttpClient { BaseAddress = host.BaseUrl };
            using (JsonDocument files = JsonDocument.Parse(await client.GetStringAsync("/workspace/files")))
                Require(!files.RootElement.EnumerateArray().Any(file => file.GetProperty("path").GetString() == ".squad/team.md"),
                    "A proposal must not cast the squad before explicit confirmation.");
            await page.GetByLabel("Describe your app").FillAsync("For our neighborhood.");
            await page.Locator("#send-casting-turn").ClickAsync();
            await Assertions.Expect(page.Locator("#send-casting-turn")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#build-app")).ToBeEnabledAsync();
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#startup-summary")).ToContainTextAsync("For our neighborhood.");
            bool failFirstBuild = true;
            int setupTurns = 0;
            await page.RouteAsync("**/apps/first/ag-ui", async route =>
            {
                string? phase = await route.Request.HeaderValueAsync("X-Squad-Phase");
                if (phase == "cast-review") setupTurns++;
                if (phase == "work" && failFirstBuild)
                {
                    failFirstBuild = false;
                    await route.FulfillAsync(new() { Status = 503, Body = "Temporarily unavailable" });
                }
                else await route.ContinueAsync();
            });
            await page.Locator("#build-app").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#notice")).ToContainTextAsync("Choose Build my app to try again.");
            await Assertions.Expect(page.Locator("#build-app")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(0);
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#startup-summary")).ToContainTextAsync("For our neighborhood.");
            await page.Locator("#build-app").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed", new() { Timeout = 30000 });
            Require(setupTurns == 1, "Retry after activation must not cast or confirm the team again.");
            await page.UnrouteAsync("**/apps/first/ag-ui");
            await Assertions.Expect(page.Locator("#casting-controls")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#work-request")).ToBeEnabledAsync();
            await Assertions.Expect(page.Locator("#wave-objective")).ToContainTextAsync("Maintain this route-planning service.");
            await Assertions.Expect(page.Locator("#wave-objective")).ToContainTextAsync("For our neighborhood.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#work-request")).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(page.Locator("#wave-proof")).ToContainTextAsync("Validation passed");
            await Assertions.Expect(page.Locator("#team-members li")).ToHaveCountAsync(2);
            await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("GNC");
            await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("Implementation specialist.");
            await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("RETRO");
            await Assertions.Expect(page.Locator("#member-count")).ToHaveTextAsync("2");
            await Assertions.Expect(page.Locator("#wave-contributors")).ToContainTextAsync("GNC");
            await VerifyTeamDetailsAsync(page);
            await page.GetByRole(AriaRole.Link, new() { Name = "Team", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Team", Exact = true })).ToHaveAttributeAsync("aria-current", "location");
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = true })).Not.ToHaveAttributeAsync("aria-current", "location");
            Require(new Uri(page.Url).Query == "?app=first", "Section navigation must retain the selected app.");
            await page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator("#wave-date")).Not.ToBeEmptyAsync();
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator("#build-app")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");

            await Assertions.Expect(page.Locator("#open-app")).ToHaveAttributeAsync("aria-disabled", "false");
            await Assertions.Expect(page.Locator("#app-preview")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#put-online")).ToBeDisabledAsync();
            await Assertions.Expect(page.Locator("#online-note")).ToContainTextAsync("Online publishing is unavailable in this local installation.");
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
            await page.GetByLabel("What would you like to change?").FillAsync("A contextual follow-up.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(2);
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");
            await page.Locator("[data-wave='1']").ClickAsync();
            await Assertions.Expect(page.Locator("#evidence-heading")).ToHaveTextAsync("Wave 1");
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(2);
            await page.Locator("[data-wave='1']").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#evidence-heading")).ToHaveTextAsync("Wave 1");
            await Assertions.Expect(page.Locator("[data-wave='1']")).ToBeFocusedAsync();
            await page.GetByLabel("What would you like to change?").FillAsync("Start long-running work.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#cancel-work")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("In progress");
            await Assertions.Expect(page.Locator("#viewing-window")).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(page.Locator("#preview-status")).ToHaveTextAsync("Build in progress…");
            await VerifySurfaceAsync(page);
            await CaptureAsync(page, artifacts, "building");
            await page.Locator("#cancel-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Stopped", new() { Timeout = 30000 });
            await Assertions.Expect(page.Locator("#viewing-window")).ToHaveAttributeAsync("aria-busy", "false");
            await page.GetByLabel("What would you like to change?").FillAsync("Exercise visible failure.");
            await page.Locator("#send-work").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Failed", new() { Timeout = 30000 });
            await Assertions.Expect(page.Locator("#wave-result")).ToContainTextAsync("Try a smaller request");
            await Assertions.Expect(page.Locator("#work-request")).ToBeEnabledAsync();
            await VerifySurfaceAsync(page);
            string secondId = await VerifyManyAppsAsync(page, context, client, artifacts);
            await host.DisposeAsync();
            await using AcceptanceHostProcess restarted = await AcceptanceHostProcess.StartAsync(
                root, host.BaseUrl.ToString().TrimEnd('/'), realLifecycle: true, workspacePath: workspace);
            await page.GotoAsync(host.BaseUrl + $"?app={secondId}");
            await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
            await Assertions.Expect(page.Locator("#squad-board")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("FERN");
            await Assertions.Expect(page.Locator("#team-members")).Not.ToContainTextAsync("GNC");
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
            await page.Locator(".brand").ClickAsync();
            await page.Locator("[data-app='first']").ClickAsync();
            await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Route Buddy");
            await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(4);
            await page.Locator("[data-wave='1']").ClickAsync();
            await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");
            Console.WriteLine("PASS server restart: both saved apps retain their titles, activated squads and reopenable waves");
            Require(errors.Count == 0, "Browser errors: " + string.Join("; ", errors));
            Console.WriteLine("lifecycle-browser: GitHub-native app library, real team roles/contributors, roster errors, isolated waves/previews, publication keyboard paths, cancellation, failure, persistence and responsive layout passed");
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

    private static async Task<string> VerifyManyAppsAsync(IPage page, IBrowserContext context, HttpClient client, string artifacts)
    {
        string firstPreview = await page.Locator("#open-app").GetAttributeAsync("href") ?? throw new InvalidOperationException("First preview missing.");
        await page.Locator(".brand").ClickAsync();
        await Assertions.Expect(page.Locator("[data-app='first'] .plot-update")).ToHaveTextAsync("Exercise visible failure.");
        await Assertions.Expect(page.Locator("[data-app='first'] time")).Not.ToBeEmptyAsync();
        await page.Locator("#new-app").ClickAsync();
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"[?&]app=[a-f0-9]{32}"));
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("New app");
        string secondId = new Uri(page.Url).Query.Split("app=")[1].Split('&')[0];
        await Assertions.Expect(page.Locator("#library")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#squad-board")).ToHaveCountAsync(0);
        await page.Locator("#casting-prompt").FillAsync("Plan my garden. Welcome gardeners. Repair the known fixture defect. Write index.html with inline CSS and JS, no dependencies or network requests.");
        await page.Locator("#send-casting-turn").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
        await page.Locator("#build-app").ClickAsync();
        await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");
        await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("FERN");
        await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("BLOOM");
        await Assertions.Expect(page.Locator("#team-members")).Not.ToContainTextAsync("GNC");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#wave-objective")).ToHaveTextAsync("Plan my garden. Welcome gardeners.");
        await Assertions.Expect(page.Locator("#wave-highlights li")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#wave-highlights")).ToHaveTextAsync("Your garden has a welcoming page.");
        string secondPreview = await page.Locator("#open-app").GetAttributeAsync("href") ?? throw new InvalidOperationException("Second preview missing.");
        Require(firstPreview != secondPreview, "Every app needs its own live address.");
        Require(await page.Locator("#download-zip").GetAttributeAsync("href") == $"/apps/{secondId}/workspace/export.zip",
            "Downloads must follow the selected app.");
        foreach ((string id, string ownTitle, string otherTitle) in new[]
        {
            ("first", "Your route planner", "Your garden"),
            (secondId, "Your garden", "Your route planner")
        })
        {
            using var archive = new ZipArchive(new MemoryStream(await client.GetByteArrayAsync($"/apps/{id}/workspace/export.zip")));
            using var reader = new StreamReader(archive.GetEntry("index.html")!.Open());
            string content = await reader.ReadToEndAsync();
            Require(content.Contains(ownTitle) && !content.Contains(otherTitle) &&
                !archive.Entries.Any(entry => entry.FullName.StartsWith(".git/")),
                "Each download must contain only that app, never another app or private library state.");
        }
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
        await page.Locator(".brand").ClickAsync();
        await Assertions.Expect(page.Locator("#app-library > li")).ToHaveCountAsync(3);
        await page.Locator("[data-app='first']").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Route Buddy");
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(4);
        await Assertions.Expect(page.Locator("#open-app")).ToHaveAttributeAsync("href", firstPreview);
        await page.Locator("[data-wave='1']").ClickAsync();
        await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");
        await page.Locator(".brand").ClickAsync();
        await page.Locator($"[data-app='{secondId}']").ClickAsync();
        await Assertions.Expect(page.Locator("#idea-heading")).ToHaveTextAsync("Garden Guide");
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
        return secondId;
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

    private static async Task VerifyTeamDetailsAsync(IPage page)
    {
        const string endpoint = "**/apps/first/casting/review";
        await page.RouteAsync(endpoint, route => route.FulfillAsync(new() { Status = 503 }));
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("#team-error")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#work-request")).ToBeEnabledAsync();
        await Assertions.Expect(page.Locator("#wave-list button")).ToHaveCountAsync(1);
        await page.UnrouteAsync(endpoint);
        await page.RouteAsync(endpoint, route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(new[]
            {
                new { path = ".squad/team.md", content = "## Members\n| Name | Role | Charter |\n| --- | --- | --- |\n| GNC | Implementation specialist | [.squad/agents/gnc/charter.md](.squad/agents/gnc/charter.md) |\n| RETRO | Reviewer | .squad/agents/retro/charter.md |\n## Alumni\n- OLD: .squad/agents/old/charter.md" },
                new { path = ".squad/agents/gnc/charter.md", content = "# GNC\nImplementation specialist." },
                new { path = ".squad/agents/retro/charter.md", content = "# RETRO\nReviewer." }
            })
        }));
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("#team-error")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#team-members li")).ToHaveCountAsync(2);
        await Assertions.Expect(page.Locator("#team-members")).Not.ToContainTextAsync("OLD");
        await Assertions.Expect(page.Locator("#team-members .member-role").First).ToHaveTextAsync("Implementation specialist");
        await page.UnrouteAsync(endpoint);
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("#team-members")).ToContainTextAsync("GNC");
    }

    private static async Task VerifySurfaceAsync(IPage page)
    {
        await Assertions.Expect(page.Locator("pre, code, .file-tree, .file-viewer, .application-diff, #review-content, #raw-file")).ToHaveCountAsync(0);
        string visible = await page.Locator("body").InnerTextAsync();
        Require(!System.Text.RegularExpressions.Regex.IsMatch(visible,
            @"\b(AKS|Kubernetes|clusters?|pods?|containers?|instances?|git|commits?|branches|diffs?|PR|workspaces?|code|coding|hire|HTML|CSS|JS|JavaScript|TypeScript|dependencies|databases?)\b|\b[\w.-]+\.(html?|css|jsx?|tsx?|json|cs|py|sh)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase), "Source and infrastructure details must not leak into the interface.");
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
        if (!rootContent.Contains("id=\"app-library\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name} root response did not contain the app library.");
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
            .ToHaveTextAsync("Apps");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "New app", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#project-view")).ToBeHiddenAsync();
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
    private bool disposed;

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
        bool realLifecycle = false,
        string? workspacePath = null)
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
        if (workspacePath is not null)
        {
            startInfo.ArgumentList.Add("--SQUAD_WORKSPACE_PATH");
            startInfo.ArgumentList.Add(workspacePath);
        }
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
        if (disposed) return;
        disposed = true;
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
