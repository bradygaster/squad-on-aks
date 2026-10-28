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
        string repositoryRoot = Paths.FindRepositoryRoot();
        string artifactRoot = Path.Combine(repositoryRoot, "artifacts", "playwright", "lifecycle");
        Directory.CreateDirectory(artifactRoot);
        foreach (string artifact in new[]
        {
            "complete-journey.png",
            "failure.png",
            "console.txt",
            "network.txt",
            "lifecycle.zip"
        })
        {
            File.Delete(Path.Combine(artifactRoot, artifact));
        }

        int port = Ports.Reserve();
        await using AcceptanceHostProcess host = await AcceptanceHostProcess.StartAsync(
            repositoryRoot,
            $"http://127.0.0.1:{port}",
            realLifecycle: true);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER_CHANNEL")
        });
        await using IBrowserContext ownerContext = await browser.NewContextAsync();
        await ownerContext.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
        IPage owner = await ownerContext.NewPageAsync();
        List<string> console = [];
        List<string> network = [];
        owner.Console += (_, message) => console.Add($"{message.Type}: {message.Text}");
        owner.RequestFailed += (_, request) => network.Add(
            $"{request.Method} {request.Url}: {request.Failure}");
        owner.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                network.Add($"{response.Status} {response.Request.Method} {response.Url}");
            }
        };

        try
        {
            await owner.GotoAsync(host.BaseUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Needs cast");
            await Assertions.Expect(owner.Locator(".cast-history")).ToBeHiddenAsync();
            using (var intentClient = new HttpClient { BaseAddress = host.BaseUrl })
            {
                using HttpResponseMessage missingIntent = await intentClient.GetAsync("/casting/intent");
                Require(missingIntent.StatusCode == HttpStatusCode.NotFound,
                    "Uncast workspace must explicitly report missing intent.");
            }

            await owner.GetByLabel("What will this Squad do?").FillAsync(
                "Maintain this route-planning service. Implement small changes, test them, and review every result.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Propose cast" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Cast conversation" }))
                .ToContainTextAsync("Proposed cast");
            await Assertions.Expect(owner.Locator(".cast-history")).ToBeVisibleAsync();
            await owner.GetByLabel("What will this Squad do?").FillAsync(
                "Refine the proposal so GNC owns implementation.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Refine proposal" }).ClickAsync();
            await owner.GetByRole(AriaRole.Button, new() { Name = "Confirm this cast" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Cast review");

            await owner.GetByLabel("Native file").SelectOptionAsync(".squad/routing.md");
            string routing = await owner.GetByLabel("Markdown").InputValueAsync();
            await owner.GetByLabel("Markdown").FillAsync("");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Save review edit" }).ClickAsync();
            await owner.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
            await Assertions.Expect(owner.GetByLabel("Run outcome")).ToHaveTextAsync("Blocked");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Validation errors" }))
                .ToContainTextAsync("routing.md is missing or empty.");

            await owner.GetByLabel("Markdown").FillAsync(routing);
            await owner.GetByRole(AriaRole.Button, new() { Name = "Save review edit" }).ClickAsync();
            await owner.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Ready");
            await owner.Locator("#setup > summary").ClickAsync();
            await Assertions.Expect(owner.Locator(".original-intent"))
                .ToContainTextAsync("Maintain this route-planning service.");
            await Assertions.Expect(owner.Locator("#casting-controls")).ToBeHiddenAsync();
            await Assertions.Expect(owner.Locator("#casting-prompt")).ToBeDisabledAsync();
            await Assertions.Expect(owner.Locator("#review-content")).ToHaveAttributeAsync("readonly", "");
            await Assertions.Expect(owner.Locator("#save-review")).ToBeHiddenAsync();
            await Assertions.Expect(owner.Locator(".review-panel")).ToBeHiddenAsync();
            await owner.Locator("#view-team-artifacts").FocusAsync();
            await owner.Keyboard.PressAsync("Enter");
            await Assertions.Expect(owner.Locator("#file-heading")).ToContainTextAsync(".squad/team.md");
            await Assertions.Expect(owner.Locator("#file-body .markdown")).ToBeVisibleAsync();
            await Assertions.Expect(owner.Locator("#file-heading")).ToBeFocusedAsync();
            await owner.GetByRole(AriaRole.Tab, new() { Name = "Work", Exact = true }).ClickAsync();
            Require(
                await owner.Locator(".cast-history > summary").EvaluateAsync<bool>(
                    "el => getComputedStyle(el, '::marker').content.includes(String.fromCodePoint(0x2002))"),
                "Disclosure marker must be separated from its label.");
            await owner.Locator("#setup > summary").ClickAsync();

            using (var guardClient = new HttpClient { BaseAddress = host.BaseUrl })
            {
                using HttpResponseMessage intentResponse = await guardClient.GetAsync("/casting/intent");
                intentResponse.EnsureSuccessStatusCode();
                Require(intentResponse.Headers.CacheControl?.NoStore == true, "Original intent must not be cached.");
                using JsonDocument intent = JsonDocument.Parse(await intentResponse.Content.ReadAsStringAsync());
                Require(intent.RootElement.GetProperty("text").GetString() ==
                    "Maintain this route-planning service. Implement small changes, test them, and review every result.",
                    "Original intent endpoint must return exact text, without injected guard or refinements.");
                foreach (string endpoint in new[] { "/workspace/file", "/workspace/raw" })
                {
                    using HttpResponseMessage privateRecord = await guardClient.GetAsync(
                        endpoint + "?path=.git%2Fsquad-original-intent.json");
                    Require(privateRecord.StatusCode == HttpStatusCode.BadRequest,
                        "Intent metadata must not be accessible through artifact APIs.");
                }
                foreach (string phase in new[] { "casting", "cast-review" })
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/ag-ui");
                    request.Headers.Add("X-Squad-Phase", phase);
                    request.Content = JsonContent.Create(new
                    {
                        threadId = Guid.NewGuid().ToString(),
                        runId = Guid.NewGuid().ToString(),
                        messages = new[] { new { id = "attempt", role = "user", content = "Recast." } },
                        state = new { },
                        tools = Array.Empty<object>(),
                        context = Array.Empty<object>(),
                        forwardedProps = new { }
                    });
                    using HttpResponseMessage response = await guardClient.SendAsync(request);
                    Require(response.StatusCode == HttpStatusCode.Conflict,
                        $"Activated Squad accepted {phase}.");
                }
            }

            await owner.GetByLabel("Describe one bounded request for the active Squad.").FillAsync(
                "Repair the known fixture defect with a test.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Send request" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Active");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Specialist activity", IncludeHidden = true }))
                .ToContainTextAsync("GNC · Delegated");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Specialist activity", IncludeHidden = true }))
                .ToContainTextAsync("Squad.Agents.AI native trace · squad.subagent");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Application evidence", IncludeHidden = true }))
                .ToContainTextAsync("src/flight-plan.sh");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Application evidence", IncludeHidden = true }))
                .ToContainTextAsync("-printf '%s\\n' LAND ORBIT LAUNCH");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Harness evidence", IncludeHidden = true }))
                .ToContainTextAsync(".squad/team.md");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Harness evidence", IncludeHidden = true }))
                .ToContainTextAsync("+++ b/.squad/team.md");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Validation evidence", IncludeHidden = true }))
                .ToContainTextAsync("sh tests/run.sh · exit 0");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Validation evidence", IncludeHidden = true }))
                .ToContainTextAsync("PASS route order");
            await Assertions.Expect(owner.GetByLabel("Run outcome")).ToHaveTextAsync("Success");
            await Assertions.Expect(owner.Locator(".evidence-group[open]")).ToHaveCountAsync(0);
            ILocator validationSummary = owner.Locator(".evidence-group > summary")
                .Filter(new() { HasText = "Validation" });
            await validationSummary.FocusAsync();
            await owner.Keyboard.PressAsync("Enter");
            await Assertions.Expect(owner.Locator("[aria-label='Validation evidence']")).ToBeVisibleAsync();
            await owner.Locator("[aria-label='Validation evidence'] summary").First.ClickAsync();
            await Assertions.Expect(owner.Locator("[aria-label='Validation evidence'] pre").First).ToBeVisibleAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Wave timeline" }))
                .ToContainTextAsync("Wave 1");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Wave timeline" }))
                .ToContainTextAsync("Completed");

            await owner.GetByLabel("Describe one bounded request for the active Squad.").FillAsync(
                "Give a contextual follow-up for the earlier fixture repair.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Send request" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Work conversation" }))
                .ToContainTextAsync("retained-context:true");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Wave timeline" }))
                .ToContainTextAsync("Wave 2");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Wave 1", Exact = false }).First.ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Harness evidence", IncludeHidden = true }))
                .ToContainTextAsync("+++ b/.squad/team.md");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Work conversation" }))
                .ToContainTextAsync("Repair the known fixture defect with a test.");

            await using IBrowserContext competitorContext = await browser.NewContextAsync();
            IPage competitor = await competitorContext.NewPageAsync();
            competitor.Console += (_, message) => console.Add($"competitor {message.Type}: {message.Text}");
            competitor.RequestFailed += (_, request) => network.Add(
                $"competitor {request.Method} {request.Url}: {request.Failure}");
            competitor.Response += (_, response) =>
            {
                if (response.Status >= 400)
                {
                    network.Add(
                        $"competitor {response.Status} {response.Request.Method} {response.Url}");
                }
            };
            await competitor.GotoAsync(host.BaseUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Assertions.Expect(competitor.GetByRole(AriaRole.Status)).ToHaveTextAsync("Ready");
            await competitor.Locator("#setup > summary").ClickAsync();
            await Assertions.Expect(competitor.Locator(".cast-history")).ToBeHiddenAsync();
            await Assertions.Expect(competitor.Locator(".review-panel")).ToBeHiddenAsync();
            await Assertions.Expect(competitor.Locator(".original-intent > p")).ToHaveTextAsync(
                "Maintain this route-planning service. Implement small changes, test them, and review every result.");
            await competitor.SetViewportSizeAsync(390, 844);
            await competitor.Locator("#view-team-artifacts").ClickAsync();
            await Assertions.Expect(competitor.Locator("#file-heading")).ToContainTextAsync(".squad/team.md");
            await Assertions.Expect(competitor.Locator("#file-body .markdown")).ToBeVisibleAsync();
            await competitor.GetByRole(AriaRole.Tab, new() { Name = "Work", Exact = true }).ClickAsync();
            await competitor.Locator("#setup > summary").ClickAsync();
            await competitor.GetByLabel("Describe one bounded request for the active Squad.").FillAsync(
                "Competing request");

            await owner.GetByLabel("Describe one bounded request for the active Squad.").FillAsync(
                "Start long-running specialist work.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Send request" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Active");
            await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Specialist activity", IncludeHidden = true }))
                .ToContainTextAsync("Long-running specialist started");
            await Assertions.Expect(owner.Locator("#live-progress")).ToContainTextAsync("GNC");
            await Assertions.Expect(owner.Locator(".activity-repeat")).ToHaveTextAsync(
                new System.Text.RegularExpressions.Regex("Repeated 3 times"));
            await Assertions.Expect(owner.Locator(".activity-running")).ToHaveCountAsync(1);
            await Assertions.Expect(owner.Locator("[aria-label='Specialist activity'] > li")).ToHaveCountAsync(4);
            ILocator activitySummary = owner.Locator(".evidence-group > summary")
                .Filter(new() { HasText = "Specialist activity" });
            await activitySummary.ClickAsync();
            string? activeWave = await owner.Locator(".wave-card[aria-current]").GetAttributeAsync("data-wave");
            await owner.GetByRole(AriaRole.Tab, new() { Name = "Artifacts", Exact = true }).ClickAsync();
            await Assertions.Expect(owner.Locator("#work-view")).ToBeHiddenAsync();
            await owner.GetByRole(AriaRole.Tab, new() { Name = "Artifacts", Exact = true }).FocusAsync();
            await owner.Keyboard.PressAsync("ArrowLeft");
            await Assertions.Expect(owner.Locator(".wave-card[aria-current]")).ToHaveAttributeAsync("data-wave", activeWave!);
            await Assertions.Expect(activitySummary.Locator("..")).ToHaveAttributeAsync("open", "");
            await Assertions.Expect(owner.Locator("#live-progress")).ToBeVisibleAsync();
            await owner.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
            Require(
                await owner.Locator(".activity-running > strong").EvaluateAsync<string>(
                    "el => getComputedStyle(el, '::before').animationName") == "none",
                "Activity spinner must respect reduced motion.");

            await competitor.GetByRole(AriaRole.Button, new() { Name = "Send request" }).ClickAsync();
            await Assertions.Expect(competitor.GetByRole(AriaRole.Alert))
                .ToContainTextAsync("409 Conflict");
            await Assertions.Expect(competitor.GetByLabel("Run outcome")).ToHaveTextAsync("Busy");

            await owner.GetByRole(AriaRole.Button, new() { Name = "Cancel active work" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Alert)).ToContainTextAsync("Cancelled");
            await Assertions.Expect(owner.GetByLabel("Run outcome")).ToHaveTextAsync("Cancelled");
            await Assertions.Expect(owner.Locator(".activity-running")).ToHaveCountAsync(0);
            await Assertions.Expect(owner.Locator("#live-progress")).ToBeHiddenAsync();

            await owner.GetByLabel("Describe one bounded request for the active Squad.").FillAsync(
                "Show a visible failure.");
            await owner.GetByRole(AriaRole.Button, new() { Name = "Send request" }).ClickAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Status)).ToHaveTextAsync("Failed");
            await Assertions.Expect(owner.GetByLabel("Run outcome")).ToHaveTextAsync("Failed");
            await Assertions.Expect(owner.GetByRole(AriaRole.Alert))
                .ToContainTextAsync("scripted visible failure");
            await Assertions.Expect(owner.GetByLabel("Describe one bounded request for the active Squad."))
                .ToBeEnabledAsync();
            await Assertions.Expect(owner.GetByRole(AriaRole.Button, new() { Name = "Send request" }))
                .ToBeEnabledAsync();
            await owner.Locator("#setup > summary").ClickAsync();
            await Assertions.Expect(owner.Locator(".original-intent > p")).ToHaveTextAsync(
                "Maintain this route-planning service. Implement small changes, test them, and review every result.");
            await owner.Locator("#setup > summary").ClickAsync();
            await owner.SetViewportSizeAsync(390, 844);
            Require(
                !await owner.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"),
                "Workflow must not overflow a mobile viewport.");
            await Assertions.Expect(owner.Locator("#send-work")).ToBeVisibleAsync();
            await owner.SetViewportSizeAsync(1280, 900);

            await owner.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactRoot, "complete-journey.png"),
                FullPage = true
            });
            await File.WriteAllLinesAsync(Path.Combine(artifactRoot, "console.txt"), console);
            await File.WriteAllLinesAsync(Path.Combine(artifactRoot, "network.txt"), network);
            Console.WriteLine(
                "lifecycle-browser: casting, remediation, delegation, diffs, validation, follow-up, concurrency, cancellation, and failure passed");
        }
        catch
        {
            await owner.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactRoot, "failure.png"),
                FullPage = true
            });
            await File.WriteAllLinesAsync(Path.Combine(artifactRoot, "console.txt"), console);
            await File.WriteAllLinesAsync(Path.Combine(artifactRoot, "network.txt"), network);
            throw;
        }
        finally
        {
            await ownerContext.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactRoot, "lifecycle.zip")
            });
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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
        await Assertions.Expect(page.GetByText("AG-UI", new() { Exact = false })).ToBeVisibleAsync();
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
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Needs cast");
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send request" }))
                .ToBeDisabledAsync();

            await page.GetByLabel("What will this Squad do?").FillAsync(
                "Maintain this route-planning shell service. Implement small changes, test them, and review every result.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Propose cast" }).ClickAsync();
            await Assertions.Expect(page.Locator("#send-casting-turn"))
                .ToHaveTextAsync("Refine proposal", new() { Timeout = 180_000 });
            await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Cast conversation" })
                .Locator("li")).ToHaveCountAsync(2);
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
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send request" }))
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
            .Locator("li")
            .CountAsync();
        await page.GetByLabel("What will this Squad do?").FillAsync(refinement);
        await page.GetByRole(AriaRole.Button, new() { Name = "Refine proposal" }).ClickAsync();
        await Assertions.Expect(page.Locator("#send-casting-turn"))
            .ToHaveTextAsync("Refine proposal", new() { Timeout = 180_000 });
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Cast conversation" })
                .Locator("li"))
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
        startInfo.ArgumentList.Add(url);
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
