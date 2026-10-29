using System.Text.Json;
using Microsoft.Playwright;

namespace SquadOnAks.AcceptanceTests;

internal static class ArtifactPublicationScenario
{
    public static async Task RunAsync(IPage page, string artifactRoot)
    {
        int publications = 0;
        bool succeed = false;
        var releasePublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/publish/owners", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = """{"available":true,"login":"test-publisher","organizations":["octodemo"],"reason":null}"""
        }));
        await page.RouteAsync("**/publish", async route =>
        {
            publications++;
            using JsonDocument request = JsonDocument.Parse(route.Request.PostData!);
            if (!request.RootElement.GetProperty("private").GetBoolean() ||
                request.RootElement.GetProperty("owner").GetString() != "octodemo" ||
                request.RootElement.GetProperty("name").GetString() != "confirmed-app")
            {
                throw new InvalidOperationException("Publication must match the reviewed private destination.");
            }
            await releasePublication.Task;
            await route.FulfillAsync(new()
            {
                Status = succeed ? 200 : 422,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    success = succeed,
                    url = "https://github.com/octodemo/confirmed-app",
                    fullName = "octodemo/confirmed-app",
                    @private = true,
                    message = "Created octodemo/confirmed-app, but the push failed."
                })
            });
        });
        try
        {
            await page.GetByRole(AriaRole.Tab, new() { Name = "Artifacts", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator("#preview-scope")).ToContainTextAsync("not a deployed app");
            await Assertions.Expect(page.Locator("#zip-scope")).ToContainTextAsync("excludes Git history");
            await Assertions.Expect(page.Locator("#publish-scope")).ToContainTextAsync("new private repository");
            if (await page.Locator("#download-zip").GetAttributeAsync("aria-describedby") != "zip-scope")
            {
                throw new InvalidOperationException("ZIP scope must be programmatically associated with its action.");
            }
            await page.Locator("#toggle-publish").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-auth")).ToContainTextAsync("test-publisher");
            await page.GetByLabel("Owner", new() { Exact = true }).SelectOptionAsync("octodemo");
            await page.GetByLabel("Repository name", new() { Exact = true }).FillAsync("confirmed-app");
            await page.Locator("#publish-submit").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-destination")).ToContainTextAsync("octodemo/confirmed-app");
            await Assertions.Expect(page.Locator("#publish-confirm")).ToBeFocusedAsync();
            if (publications != 0) throw new InvalidOperationException("Review must not publish.");

            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactRoot, "publish-review-desktop.png"), FullPage = true });
            await page.SetViewportSizeAsync(390, 844);
            if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth"))
            {
                throw new InvalidOperationException("Artifact disclosure and confirmation must fit the mobile viewport.");
            }
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactRoot, "publish-review-mobile.png"), FullPage = true });
            await page.SetViewportSizeAsync(1280, 720);

            await page.Locator("#publish-back").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-name")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator("#publish-review")).ToBeHiddenAsync();
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-name").FillAsync("changed-app");
            await Assertions.Expect(page.Locator("#publish-review")).ToBeHiddenAsync();
            await page.Locator("#publish-name").FillAsync("confirmed-app");
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-cancel").ClickAsync();
            await Assertions.Expect(page.Locator("#toggle-publish")).ToBeFocusedAsync();
            if (publications != 0) throw new InvalidOperationException("Cancel must not publish.");

            await page.Locator("#toggle-publish").ClickAsync();
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-confirm").PressAsync("Enter");
            await Assertions.Expect(page.Locator("#publish-confirm")).ToBeDisabledAsync();
            await page.Locator("#publish-confirm").DispatchEventAsync("click");
            releasePublication.SetResult();
            await Assertions.Expect(page.Locator("#publish-result")).ToContainTextAsync("Publication is incomplete");
            await Assertions.Expect(page.Locator("#publish-result a"))
                .ToHaveAttributeAsync("href", "https://github.com/octodemo/confirmed-app");
            await Assertions.Expect(page.Locator("#publish-result")).ToContainTextAsync("retry is not yet supported");
            if (publications != 1) throw new InvalidOperationException("Repeated activation must send one request.");

            succeed = true;
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-confirm").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-result")).ToContainTextAsync("Published to private repository");
            if (publications != 2) throw new InvalidOperationException("Confirmed publication must submit once.");
            await page.Locator("#publish-cancel").ClickAsync();
            await page.GetByRole(AriaRole.Tab, new() { Name = "Work", Exact = true }).ClickAsync();
            Console.WriteLine("PASS artifact scopes, private destination review, cancellation, keyboard focus, duplicate suppression and result links (mocked GitHub)");
        }
        finally
        {
            releasePublication.TrySetResult();
            await page.UnrouteAsync("**/publish/owners");
            await page.UnrouteAsync("**/publish");
        }
    }
}
