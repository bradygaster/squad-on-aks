using System.Text.Json;
using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

// An app attached to a GitHub repository: its open issues start waves, and a draft pull request is opened only on click.
// GitHub is never called: /workspace/source, /workspace/github and /workspace/pull are faked.
internal static class RepositoryWorkScenario
{
    private const string BodyMarker = "issue-body-must-not-render";

    public static async Task RunAsync(IPage page, string artifactRoot)
    {
        int pulls = 0;
        int pullStatus = 200;
        string pullBody = """{"number":31,"url":"https://github.com/octo/moonwake/pull/31","closes":[10],"draft":true}""";
        string work = JsonSerializer.Serialize(new
        {
            issues = new object[]
            {
                new { number = 10, title = "Make lunar controls accessible", url = "https://github.com/octo/moonwake/issues/10",
                    labels = new[] { "accessibility" }, fromTeam = true, body = BodyMarker },
                new { number = 11, title = "<img src=x onerror='window.issueInjected=true'>Speed up turns", url = "javascript:alert(1)",
                    labels = Array.Empty<string>(), fromTeam = false, body = BodyMarker }
            },
            pullRequests = new object[]
            {
                new { number = 28, title = "Faster turns", url = "https://github.com/octo/moonwake/pull/28", draft = true, closes = new[] { 11 } }
            }
        });
        await page.RouteAsync("**/apps/first/workspace/source", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = """
                {"imported":true,"fullName":"octo/moonwake","url":"https://github.com/octo/moonwake","defaultBranch":"main",
                 "branch":"squad/76907502","hadSquad":true,"deployable":false,"hasAppHost":false,"hasContract":false,
                 "hasJourneys":false,"hasSquad":true,"hasTests":false}
                """
        }));
        await page.RouteAsync("**/apps/first/workspace/github", route => route.FulfillAsync(new() { ContentType = "application/json", Body = work }));
        await page.RouteAsync("**/apps/first/workspace/pull", route =>
        {
            pulls++;
            return route.FulfillAsync(new() { Status = pullStatus, ContentType = "application/json", Body = pullBody });
        });
        try
        {
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#repo-work")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("[data-repo-work='issues']")).ToHaveTextAsync("Issues 2");
            await Assertions.Expect(page.Locator("[data-repo-work='pulls']")).ToHaveTextAsync("Pull requests 1");
            await Assertions.Expect(page.Locator("#repo-work-list")).ToContainTextAsync("#10 Make lunar controls accessible");
            await Assertions.Expect(page.Locator("#repo-work-list")).ToContainTextAsync("accessibility");
            await Assertions.Expect(page.Locator("#repo-work-list")).ToContainTextAsync("Pull request #28 is open for this");
            await Assertions.Expect(page.Locator("#repo-work-list button")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator("#repo-work-list img")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#repo-work-list a[href^='javascript']")).ToHaveCountAsync(0);
            Require(!await page.EvaluateAsync<bool>("Boolean(window.issueInjected)"), "Issue titles must not inject markup.");
            Require(!(await page.ContentAsync()).Contains(BodyMarker), "Issue bodies must never reach the page.");
            await Assertions.Expect(page.Locator("#app-status-text")).ToContainTextAsync("work goes to squad/76907502");
            await Assertions.Expect(page.Locator("#repo-onboarding summary")).ToHaveTextAsync("Put it online (optional)");
            await Assertions.Expect(page.Locator("#repo-onboarding")).Not.ToHaveAttributeAsync("open", "");

            await page.GetByRole(AriaRole.Button, new() { Name = "Work on this" }).ClickAsync();
            await Assertions.Expect(page.Locator("#work-request")).ToHaveValueAsync("Work on issue #10: Make lunar controls accessible");
            await page.Locator("#work-request").FillAsync("");

            await page.Locator("[data-repo-work='pulls']").ClickAsync();
            await Assertions.Expect(page.Locator("[data-repo-work='pulls']")).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(page.Locator("#repo-work-list")).ToContainTextAsync("#28 Faster turns");
            await Assertions.Expect(page.Locator("#repo-work-list")).ToContainTextAsync("Draft · closes #11");
            await page.Locator("[data-repo-work='issues']").ClickAsync();

            await Assertions.Expect(page.Locator("#wave-pull")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#wave-pull-target")).ToHaveTextAsync(
                "Sends to branch squad/76907502 on octo/moonwake. Your main branch isn’t changed.");
            Require(pulls == 0, "A pull request is opened only on the person's click.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Open draft pull request" }).ClickAsync();
            await Assertions.Expect(page.Locator("#pull-result")).ToContainTextAsync("Draft pull request #31 is open · closes #10");
            await Assertions.Expect(page.Locator("#pull-result a")).ToHaveAttributeAsync("href", "https://github.com/octo/moonwake/pull/31");
            Require(pulls == 1, "One click opens one pull request.");
            await Assertions.Expect(page.Locator("#send-branch")).ToHaveTextAsync("Send to GitHub");

            // GitHub's refusal reaches the person as the server's own sentence; anything else is the plain fallback.
            pullStatus = 422;
            pullBody = """{"message":"GitHub didn't allow a pull request on this repository."}""";
            await page.GetByRole(AriaRole.Button, new() { Name = "Open draft pull request" }).ClickAsync();
            await Assertions.Expect(page.Locator("#pull-result")).ToHaveTextAsync("GitHub didn't allow a pull request on this repository.");
            pullBody = """{"message":"Validation Failed: head sha {abc} at https://api.github.com"}""";
            await page.GetByRole(AriaRole.Button, new() { Name = "Open draft pull request" }).ClickAsync();
            await Assertions.Expect(page.Locator("#pull-result")).ToHaveTextAsync("The draft pull request couldn’t be opened. Try again in a moment.");
            pullStatus = 200;
            pullBody = """{"number":29,"url":"https://github.com/octo/moonwake/pull/29","closes":[],"draft":false}""";
            await page.GetByRole(AriaRole.Button, new() { Name = "Open draft pull request" }).ClickAsync();
            await Assertions.Expect(page.Locator("#pull-result")).ToContainTextAsync("Pull request #29 is open");
            await Assertions.Expect(page.Locator("#pull-result")).Not.ToContainTextAsync("Draft");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactRoot, "repository-work-desktop.png"), FullPage = true });

            await page.UnrouteAsync("**/apps/first/workspace/github");
            await page.RouteAsync("**/apps/first/workspace/github", route => route.FulfillAsync(new() { Status = 503, Body = "{}" }));
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#wave-pull")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#repo-work")).ToBeHiddenAsync();
            await page.UnrouteAsync("**/apps/first/workspace/github");
            await page.RouteAsync("**/apps/first/workspace/github", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = """{"issues":[],"pullRequests":[]}"""
            }));
            await page.ReloadAsync();
            await Assertions.Expect(page.Locator("#wave-pull")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#repo-work")).ToBeHiddenAsync();
        }
        finally
        {
            await page.UnrouteAsync("**/apps/first/workspace/source");
            await page.UnrouteAsync("**/apps/first/workspace/github");
            await page.UnrouteAsync("**/apps/first/workspace/pull");
            await page.ReloadAsync();
        }
        await Assertions.Expect(page.Locator("#repo-work")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#wave-pull")).ToBeHiddenAsync();
        // Let the reloaded page finish its first render so later checks start from a settled page.
        await Assertions.Expect(page.Locator("#wave-result")).ToContainTextAsync("This wave is complete");
        await Assertions.Expect(page.Locator("#activity-list")).Not.ToBeEmptyAsync();
        await Assertions.Expect(page.Locator("#team-status")).Not.ToBeEmptyAsync();
        Console.WriteLine("PASS repository issues start waves, titles stay text, and a draft pull request opens only on click");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
