using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

internal sealed class ArcadeLiveAcceptanceLayer(string? baseUrl) : IAcceptanceLayer
{
    private const string Brief = """
        Build an original, single-player alien-invasion arcade shooter inspired by old-school Space Invaders,
        without copying its name, artwork or sounds. Use a purple (#7c3aed), orange (#ff7a18) and black (#0a0a0a)
        neo-brutalist theme: thick borders, square controls, offset shadows, and bold readable type.
        Ship a complete static browser game using HTML canvas, document-relative CSS and JavaScript,
        no packages, external assets, fonts or services. Include alien formations, shooting, scoring,
        three lives, increasing rounds, game over, restart, and a beginner-friendly first round.
        Support arrow keys/A/D to move and holding Space to shoot, plus visible touch controls.
        Add meaningful offline validation in tests/run.sh using Node or Python.
        Keep the team small. All product decisions are supplied; plan it and then build from this brief.

        Use clear accessible button names: Start game, Pause, Resume, Restart, Move left, Move right, Fire.
        Display real game state in an element with data-testid="game-state" (Ready, Playing, Paused, Game over),
        real score with data-testid="score", and lives with data-testid="lives".
        Restart returns to Ready with zero score and three lives. Pausing freezes the game.
        These are visible live game indicators, not test-only or simulated state.
        """;

    private const string FollowUp = """
        Turn this into a two-game arcade. Preserve the existing alien shooter and its controls.
        Add an original old-school brick-breaker game with a moving paddle, bouncing ball, destructible bricks,
        score, three lives, pause/resume, game over and restart. Keep the purple/orange/black neo-brutalist
        theme and no external dependencies. Add visible game-selection buttons named Alien shooter and
        Brick breaker. Changing games resets to Ready. Both games use the existing Start game, Pause,
        Resume, Restart, Move left and Move right buttons and real visible game-state, score and lives
        data-testid indicators. Restart resets to Ready with zero score and three lives.
        Keep keyboard and touch play working, preserve document-relative preview assets,
        and extend the meaningful offline validation to cover both games. Deliver a working app.
        """;

    public async Task RunAsync()
    {
        if (Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_ARCADE_LIVE") != "1")
            throw new InvalidOperationException("arcade-live contacts real models and writes app waves. Set SQUAD_ACCEPTANCE_ARCADE_LIVE=1 explicitly.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? target) || !target.IsLoopback ||
            target.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("arcade-live requires a loopback --base-url discovered through Aspire.");

        string artifacts = Path.Combine(Paths.FindRepositoryRoot(), "artifacts", "playwright", "arcade-live",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(artifacts);
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1440, Height = 1000 },
            HasTouch = true
        });
        List<string> errors = [];
        context.Page += (_, opened) => opened.PageError += (_, error) => errors.Add(error);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        IPage app = await context.NewPageAsync();
        IPage? game = null;
        string? appId = Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_ARCADE_APP_ID");
        try
        {
            if (appId is null)
            {
                await app.GotoAsync(target.ToString());
                await app.GetByRole(AriaRole.Button, new() { Name = "New app", Exact = true }).ClickAsync();
                await app.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"[?&]app=[a-f0-9]{32}"));
                appId = new Uri(app.Url).Query.Split("app=")[1].Split('&')[0];
            }
            else
            {
                if (!Guid.TryParseExact(appId, "N", out _))
                    throw new InvalidOperationException("The resumed arcade app ID must be an existing generated app ID.");
                await app.GotoAsync(new Uri(target, $"?app={appId}").ToString());
            }
            using var http = new HttpClient { BaseAddress = target };
            JsonElement before = await http.GetFromJsonAsync<JsonElement>($"/apps/{appId}/waves");
            Require(before.GetArrayLength() == 0, "This build test needs an app with no prior waves; it will not duplicate existing work.");
            Console.WriteLine($"arcade-live app={appId} artifacts={artifacts}");
            await File.WriteAllTextAsync(Path.Combine(artifacts, "app-id.txt"), appId);
            JsonElement cast = await http.GetFromJsonAsync<JsonElement>($"/apps/{appId}/casting/status");
            if (!cast.GetProperty("canTakeRequests").GetBoolean())
            {
                await app.GetByLabel("Describe your app").FillAsync(Brief);
                await app.Locator("#send-casting-turn").ClickAsync();
                await Assertions.Expect(app.Locator("#send-casting-turn")).ToBeEnabledAsync(new() { Timeout = 300_000 });
                Require(await app.Locator("#notice").IsHiddenAsync(), "Planning failed: " + await app.Locator("#notice").InnerTextAsync());
                Require(await app.Locator("#startup-question").IsHiddenAsync(), "Planning requested clarification: " + await app.Locator("#startup-question").InnerTextAsync());
            }
            await app.Locator("#build-app").ClickAsync();
            await WaitForWaveAsync(app, 1);
            await VerifyWaveAsync(http, appId, 1, artifacts);
            await Assertions.Expect(app.Locator("#team-members li")).Not.ToHaveCountAsync(0);
            string preview = await app.Locator("#open-app").GetAttributeAsync("href")
                ?? throw new InvalidOperationException("The first build has no preview address.");
            game = await context.NewPageAsync();
            await game.GotoAsync(preview);
            await VerifyGameAsync(game, artifacts, "alien-shooter", scoreRequired: true);

            await app.GetByLabel("What would you like to change?").FillAsync(FollowUp);
            await app.Locator("#send-work").ClickAsync();
            await WaitForWaveAsync(app, 2);
            await VerifyWaveAsync(http, appId, 2, artifacts);
            await game.ReloadAsync();
            await game.GetByRole(AriaRole.Button, new() { Name = "Brick breaker", Exact = true }).ClickAsync();
            await VerifyGameAsync(game, artifacts, "brick-breaker", scoreRequired: true);
            await game.GetByRole(AriaRole.Button, new() { Name = "Alien shooter", Exact = true }).ClickAsync();
            await Assertions.Expect(game.GetByTestId("game-state")).ToHaveTextAsync("Ready");
            await game.GetByRole(AriaRole.Button, new() { Name = "Start game", Exact = true }).ClickAsync();
            await Assertions.Expect(game.GetByTestId("game-state")).ToHaveTextAsync("Playing");
            await app.ReloadAsync();
            await Assertions.Expect(app.Locator("#wave-list button")).ToHaveCountAsync(2);
            await app.Locator("[data-wave='1']").ClickAsync();
            await Assertions.Expect(app.Locator("#wave-state")).ToHaveTextAsync("Completed");
            await Assertions.Expect(app.Locator("#wave-proof")).ToContainTextAsync("Validation passed");
            Require(errors.Count == 0, "Browser errors: " + string.Join("; ", errors));
            await File.WriteAllTextAsync(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new
            {
                appId, preview, completedAt = DateTimeOffset.UtcNow,
                firstWave = "Alien shooter: real build, validation and gameplay passed",
                secondWave = "Brick breaker: real update, validation and gameplay passed",
                reload = "Both waves reopen; original game remains playable",
                errors
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS arcade-live: two real validated game waves, gameplay, pause/restart, mobile, theme and reload at {preview}");
        }
        catch
        {
            await app.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "platform-failure.png"), FullPage = true });
            if (game is not null)
                await game.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "game-failure.png"), FullPage = true });
            throw;
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(artifacts, "trace.zip") });
        }
    }

    private static async Task WaitForWaveAsync(IPage page, int number)
    {
        await page.WaitForFunctionAsync("""
            number => !document.querySelector('#notice').hidden ||
                (document.querySelector('#evidence-heading').textContent === `Wave ${number}` &&
                 ['Completed', 'Failed', 'Stopped'].includes(document.querySelector('#wave-state').textContent))
            """, number, new() { Timeout = 900_000 });
        Require(await page.Locator("#notice").IsHiddenAsync(), "Build failed: " + await page.Locator("#notice").InnerTextAsync());
        await Assertions.Expect(page.Locator("#wave-state")).ToHaveTextAsync("Completed");
        await Assertions.Expect(page.Locator("#open-app")).ToHaveAttributeAsync("aria-disabled", "false", new() { Timeout = 30_000 });
    }

    private static async Task VerifyWaveAsync(HttpClient http, string appId, int number, string artifacts)
    {
        JsonElement detail = await http.GetFromJsonAsync<JsonElement>($"/apps/{appId}/waves/{number}");
        JsonElement summary = detail.GetProperty("summary");
        Require(summary.GetProperty("state").GetString() == "Completed", $"Wave {number} did not complete.");
        Require(summary.GetProperty("validationState").GetString() == "Passed", $"Wave {number} did not pass real validation.");
        Require(summary.GetProperty("changedFiles").GetArrayLength() > 0, $"Wave {number} recorded no changes.");
        await File.WriteAllTextAsync(Path.Combine(artifacts, $"wave-{number}.json"), detail.GetRawText());
        Console.WriteLine($"PASS wave {number}: completed, changed files and validation passed");
    }

    private static async Task VerifyGameAsync(IPage page, string artifacts, string name, bool scoreRequired)
    {
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Ready");
        await Assertions.Expect(page.Locator("canvas:visible")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Start game", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Playing");
        await page.Keyboard.DownAsync("Space");
        try
        {
            foreach (string key in new[] { "ArrowLeft", "ArrowRight", "ArrowRight", "ArrowLeft" })
            {
                await page.Keyboard.DownAsync(key);
                await page.WaitForTimeoutAsync(300);
                await page.Keyboard.UpAsync(key);
            }
            if (scoreRequired)
                await page.WaitForFunctionAsync("() => Number(document.querySelector('[data-testid=score]').textContent.match(/\\d+/)?.[0]) > 0",
                    options: new() { Timeout = 20_000 });
        }
        finally { await page.Keyboard.UpAsync("Space"); }
        await page.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Paused");
        string paused = await page.Locator("canvas:visible").EvaluateAsync<string>("canvas => canvas.toDataURL()");
        await page.WaitForTimeoutAsync(300);
        Require(paused == await page.Locator("canvas:visible").EvaluateAsync<string>("canvas => canvas.toDataURL()"),
            $"{name} simulation did not freeze on pause.");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{name}-desktop.png"), FullPage = true });
        await page.GetByRole(AriaRole.Button, new() { Name = "Resume", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Playing");
        await page.GetByRole(AriaRole.Button, new() { Name = "Restart", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Ready");
        Require(await page.GetByTestId("score").EvaluateAsync<int>("el => Number(el.textContent.match(/\\d+/)[0])") == 0,
            $"{name} restart must reset the score.");
        Require(await page.GetByTestId("lives").EvaluateAsync<int>("el => Number(el.textContent.match(/\\d+/)[0])") == 3,
            $"{name} restart must reset lives.");
        string[] colors = await page.EvaluateAsync<string[]>("""
            () => [...new Set([...document.querySelectorAll('body, main, h1, h2, button')].flatMap(el => {
                const s = getComputedStyle(el);
                return [s.backgroundColor, s.color, s.borderColor];
            }))]
            """);
        foreach (string color in new[] { "rgb(124, 58, 237)", "rgb(255, 122, 24)", "rgb(10, 10, 10)" })
            Require(colors.Contains(color), $"{name} is missing the requested theme color {color}.");
        await page.SetViewportSizeAsync(390, 844);
        Require(!await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"),
            $"{name} overflows the phone viewport.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Move left", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Move right", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Start game", Exact = true }).TapAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Playing");
        await page.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true }).TapAsync();
        await Assertions.Expect(page.GetByTestId("game-state")).ToHaveTextAsync("Paused");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{name}-mobile.png"), FullPage = true });
        await page.GetByRole(AriaRole.Button, new() { Name = "Restart", Exact = true }).TapAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        Console.WriteLine($"PASS {name}: points scored, keyboard, pause/resume, restart, requested palette and phone controls");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
