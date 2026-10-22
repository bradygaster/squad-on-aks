using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

internal sealed class DeterministicScenarios(
    IBrowser browser,
    Uri baseUrl,
    string artifactRoot)
{
    private readonly List<string> failures = [];

    public async Task RunAsync()
    {
        await RunCaseAsync("casting-and-activation", CastingAndActivationAsync);
        await RunCaseAsync("malformed-harness", MalformedHarnessAsync);
        await RunCaseAsync("streaming-and-follow-up", StreamingAndFollowUpAsync);
        await RunCaseAsync("busy-and-cancellation", BusyAndCancellationAsync);
        await RunCaseAsync("failure-disconnect-replacement", FailureDisconnectReplacementAsync);

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Deterministic acceptance failed:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }

        Console.WriteLine("deterministic: 5 scenarios passed");
        Console.WriteLine($"artifacts: {artifactRoot}");
    }

    private async Task CastingAndActivationAsync(IPage page)
    {
        await ExpectStatusAsync(page, "Needs cast");
        await page.GetByRole(AriaRole.Button, new() { Name = "Describe mission" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("Proposed cast");

        await page.GetByRole(AriaRole.Button, new() { Name = "Request refinement" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("Focus the tester on integration tests.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm cast" }).ClickAsync();
        await ExpectStatusAsync(page, "Cast review");

        await page.GetByLabel("Harness path").FillAsync("../outside.md");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save harness file" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Rejected non-allow-listed path");

        await page.GetByLabel("Harness path").FillAsync(".squad/agents/gnc/charter.md");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save harness file" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("Saved .squad/agents/gnc/charter.md");

        await page.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
        await ExpectStatusAsync(page, "Ready");
    }

    private async Task MalformedHarnessAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm cast" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Make harness malformed" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
        await ExpectStatusAsync(page, "Blocked");
        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Activation blocked: routing references an inactive member.");
    }

    private async Task StreamingAndFollowUpAsync(IPage page)
    {
        await ActivateAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Send work" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("Streaming repair result");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Specialist activity" }))
            .ToContainTextAsync("[ACTIVE] GNC: GNC started. [squad.subagent]");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Specialist activity" }))
            .ToContainTextAsync("[SUCCESS] RETRO: Review complete. [squad.subagent]");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Harness changes" }))
            .ToContainTextAsync(".squad/team.md");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Application changes" }))
            .ToContainTextAsync("src/flight-plan.sh");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Application changes" }))
            .ToContainTextAsync("-printf '%s\\n' LAND ORBIT LAUNCH");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Application changes" }))
            .ToContainTextAsync("+printf '%s\\n' LAUNCH ORBIT LAND");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Validation evidence" }))
            .ToContainTextAsync("[SUCCESS] sh tests/run.sh → exit 0");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Validation evidence" }))
            .ToContainTextAsync("PASS route order");

        await page.GetByRole(AriaRole.Button, new() { Name = "Send contextual follow-up" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("earlier fixture repair passed review");
    }

    private async Task BusyAndCancellationAsync(IPage owner)
    {
        await ActivateAsync(owner);
        Task ownerRun = owner.GetByRole(AriaRole.Button, new() { Name = "Start cancellable work" }).ClickAsync();
        await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Specialist activity" }))
            .ToContainTextAsync("[ACTIVE] GNC: Long-running specialist started.");

        await using IBrowserContext competingContext = await browser.NewContextAsync();
        IPage competitor = await competingContext.NewPageAsync();
        await competitor.GotoAsync(baseUrl.ToString());
        await competitor.GetByRole(AriaRole.Button, new() { Name = "Send work" }).ClickAsync();
        await Assertions.Expect(competitor.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Busy: 409 Conflict");

        await owner.GetByRole(AriaRole.Button, new() { Name = "Cancel active work" }).ClickAsync();
        await ownerRun;
        await ExpectStatusAsync(owner, "Cancelling");
        await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("Run stopped after cancellation was requested.");
        await Assertions.Expect(owner.GetByRole(AriaRole.List, new() { Name = "Specialist activity" }))
            .ToContainTextAsync("[CANCELLED] GNC: GNC stopped after cancellation.");
    }

    private async Task FailureDisconnectReplacementAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Simulate failure" }).ClickAsync();
        await ExpectStatusAsync(page, "Failed");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("failure remains visible");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Specialist activity" }))
            .ToContainTextAsync("[FAILURE] GNC: GNC failed.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Simulate disconnect" }).ClickAsync();
        await ExpectStatusAsync(page, "Disconnected");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("no success or cancellation was inferred");

        await page.GetByRole(AriaRole.Button, new() { Name = "Simulate replacement" }).ClickAsync();
        await ExpectStatusAsync(page, "Needs cast");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Conversation stream" }))
            .ToContainTextAsync("pristine workspace");
    }

    private async Task RunCaseAsync(string name, Func<IPage, Task> test)
    {
        await ResetAsync();
        await using IBrowserContext context = await browser.NewContextAsync();
        string tracePath = Path.Combine(artifactRoot, $"{name}.zip");
        await context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        IPage page = await context.NewPageAsync();
        List<string> console = [];
        List<string> network = [];
        page.Console += (_, message) => console.Add($"{message.Type}: {message.Text}");
        page.Request += (_, request) => network.Add(
            $"REQUEST {request.Method} {request.Url}");
        page.RequestFailed += (_, request) => network.Add(
            $"FAILED {request.Method} {request.Url}: {request.Failure}");
        page.Response += (_, response) =>
        {
            network.Add($"RESPONSE {response.Status} {response.Request.Method} {response.Url}");
        };

        try
        {
            await page.GotoAsync(baseUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await test(page);
            await SaveDiagnosticsAsync(name, page, console, network);
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            await SaveDiagnosticsAsync(name, page, console, network);
            failures.Add($"FAIL {name}: {exception.Message}");
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = tracePath });
        }
    }

    private async Task SaveDiagnosticsAsync(
        string name,
        IPage page,
        IReadOnlyList<string> console,
        IReadOnlyList<string> network)
    {
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactRoot, $"{name}.png"),
            FullPage = true
        });
        await File.WriteAllLinesAsync(
            Path.Combine(artifactRoot, $"{name}-console.txt"),
            console);
        await File.WriteAllLinesAsync(
            Path.Combine(artifactRoot, $"{name}-network.txt"),
            network);
    }

    private async Task ResetAsync()
    {
        using HttpClient client = new();
        using HttpResponseMessage response = await client.PostAsync(new Uri(baseUrl, "/harness/reset"), null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task ActivateAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm cast" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Start taking requests" }).ClickAsync();
        await ExpectStatusAsync(page, "Ready");
    }

    private static Task ExpectStatusAsync(IPage page, string expected) =>
        Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync(expected);
}
