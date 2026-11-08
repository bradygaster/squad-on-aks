using System.Text.Json;
using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

internal sealed class NtierLiveAcceptanceLayer(string? baseUrl) : IAcceptanceLayer
{
    public async Task RunAsync()
    {
        if (Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_NTIER_LIVE") != "1")
            throw new InvalidOperationException("Set SQUAD_ACCEPTANCE_NTIER_LIVE=1 to test the real generated app and write task data.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? target) || !target.IsLoopback ||
            target.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("ntier-live requires the generated Razor application's loopback URL.");
        string phase = Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_NTIER_PHASE") ?? "before-restart";
        if (phase is not ("before-restart" or "after-restart"))
            throw new InvalidOperationException("The N-tier phase must be before-restart or after-restart.");

        string artifacts = Path.Combine(Paths.FindRepositoryRoot(), "artifacts", "playwright", "ntier-live");
        Directory.CreateDirectory(artifacts);
        string statePath = Path.Combine(artifacts, "restart-state.json");
        string title = phase == "before-restart"
            ? $"Playwright persistence proof {DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}"
            : JsonSerializer.Deserialize<PersistenceState>(await File.ReadAllTextAsync(statePath))?.Title
                ?? throw new InvalidOperationException("The before-restart test did not save its task title.");
        Console.WriteLine($"PLAYWRIGHT Task Ledger: {phase} against the actual Razor UI at {target}");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1440, Height = 1000 },
            IgnoreHTTPSErrors = true
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        IPage page = await context.NewPageAsync();
        List<string> errors = [];
        page.PageError += (_, error) => errors.Add(error);
        try
        {
            await page.GotoAsync(target.ToString());
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your tasks", Exact = true })).ToBeVisibleAsync();
            if (phase == "before-restart")
            {
                await page.GetByLabel("Task name", new() { Exact = true }).FillAsync(" ");
                await page.GetByRole(AriaRole.Button, new() { Name = "Add task", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Task name is required");
                Console.WriteLine("PASS real Razor form rejects a blank task title");
                await page.GetByLabel("Task name", new() { Exact = true }).FillAsync(title);
                await page.GetByRole(AriaRole.Button, new() { Name = "Add task", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Mark complete: {title}", Exact = true })).ToBeVisibleAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = $"Mark complete: {title}", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Mark incomplete: {title}", Exact = true })).ToBeVisibleAsync();
                await page.ReloadAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Mark incomplete: {title}", Exact = true })).ToBeVisibleAsync();
                await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new PersistenceState(title)));
                Console.WriteLine("PASS real UI creates, completes and reloads the PostgreSQL-backed task");
            }
            else
            {
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Mark incomplete: {title}", Exact = true })).ToBeVisibleAsync();
                Console.WriteLine("PASS saved completed task survives API restart and a fresh browser context");
                await page.GetByRole(AriaRole.Button, new() { Name = $"Mark incomplete: {title}", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Mark complete: {title}", Exact = true })).ToBeVisibleAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = $"Delete task: {title}", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Delete task: {title}", Exact = true })).ToHaveCountAsync(0);
                await page.ReloadAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Delete task: {title}", Exact = true })).ToHaveCountAsync(0);
                Console.WriteLine("PASS real UI reopens and deletes the task; deletion survives reload");
            }
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{phase}-desktop.png"), FullPage = true });
            await page.SetViewportSizeAsync(390, 844);
            if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"))
                throw new InvalidOperationException("The Razor UI overflows a phone viewport.");
            await Assertions.Expect(page.GetByLabel("Task name", new() { Exact = true })).ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{phase}-mobile.png"), FullPage = true });
            if (errors.Count != 0)
                throw new InvalidOperationException("Browser errors: " + string.Join("; ", errors));
            await File.WriteAllTextAsync(Path.Combine(artifacts, $"{phase}-result.json"),
                JsonSerializer.Serialize(new { phase, url = target.ToString(), title, completedAt = DateTimeOffset.UtcNow, errors }));
            Console.WriteLine($"PASS ntier-live {phase}: real browser, real HTTP API and real PostgreSQL; no route interception");
        }
        catch
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{phase}-failure.png"), FullPage = true });
            throw;
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(artifacts, $"{phase}-trace.zip") });
        }
    }

    private sealed record PersistenceState(string Title);
}
