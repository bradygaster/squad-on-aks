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
            Body = """{"available":true,"login":"test-publisher","organizations":["octodemo"]}"""
        }));
        await page.RouteAsync("**/publish", async route =>
        {
            publications++;
            using JsonDocument request = JsonDocument.Parse(route.Request.PostData!);
            if (!request.RootElement.GetProperty("private").GetBoolean() ||
                request.RootElement.GetProperty("owner").GetString() != "octodemo" ||
                request.RootElement.GetProperty("name").GetString() != "confirmed-app")
                throw new InvalidOperationException("Publication must match the reviewed private destination.");
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
                    message = "Technical diagnostics must never be shown."
                })
            });
        });
        try
        {
            await page.Locator("#toggle-publish").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#publish-auth")).ToContainTextAsync("test-publisher");
            await page.GetByLabel("Whose account?").SelectOptionAsync("octodemo");
            await page.GetByLabel("Repository name").FillAsync("not a valid name");
            await page.Locator("#publish-submit").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-name")).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(page.Locator("#publish-name")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#toggle-publish")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.GetByLabel("Repository name").FillAsync("confirmed-app");
            await page.GetByLabel("Whose account?").SelectOptionAsync("octodemo");
            await page.Locator("#publish-submit").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-destination")).ToContainTextAsync("octodemo / confirmed-app");
            await Assertions.Expect(page.Locator("#publish-confirm")).ToBeFocusedAsync();
            if (publications != 0) throw new InvalidOperationException("Review must not publish.");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactRoot, "publish-desktop.png"), FullPage = true });
            await page.SetViewportSizeAsync(390, 844);
            if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth"))
                throw new InvalidOperationException("Publication must fit on a phone.");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactRoot, "publish-mobile.png"), FullPage = true });
            await page.SetViewportSizeAsync(1440, 900);
            await page.Locator("#publish-back").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-name")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator("#publish-review")).ToBeHiddenAsync();
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-name").FillAsync("changed-app");
            await Assertions.Expect(page.Locator("#publish-review")).ToBeHiddenAsync();
            await page.Locator("#publish-name").FillAsync("confirmed-app");
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-cancel").ClickAsync();
            if (publications != 0) throw new InvalidOperationException("Cancel must not publish.");
            await page.Locator("#toggle-publish").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-owner")).ToBeEnabledAsync();
            await page.GetByLabel("Whose account?").SelectOptionAsync("octodemo");
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-confirm").PressAsync("Enter");
            await Assertions.Expect(page.Locator("#publish-confirm")).ToBeDisabledAsync();
            await page.Locator("#publish-confirm").DispatchEventAsync("click");
            releasePublication.SetResult();
            await Assertions.Expect(page.Locator("#publish-result")).ToContainTextAsync("Publication isn’t complete");
            await Assertions.Expect(page.Locator("#publish-result")).Not.ToContainTextAsync("Technical diagnostics");
            if (publications != 1) throw new InvalidOperationException("Repeated activation must send one request.");
            succeed = true;
            await page.Locator("#publish-submit").ClickAsync();
            await page.Locator("#publish-confirm").ClickAsync();
            await Assertions.Expect(page.Locator("#publish-result")).ToContainTextAsync("Published to a private repository.");
            await Assertions.Expect(page.Locator("#publish-result a")).ToHaveAttributeAsync("href", "https://github.com/octodemo/confirmed-app");
            if (publications != 2) throw new InvalidOperationException("Confirmed publication must submit once.");
            await page.Locator("#publish-cancel").ClickAsync();
            await Assertions.Expect(page.Locator("#toggle-publish")).ToBeFocusedAsync();
            Console.WriteLine("PASS Daydream publication: private destination, validation, keyboard, Escape, changed-details review, duplicate suppression and plain-language outcomes (mocked service)");
        }
        finally
        {
            releasePublication.TrySetResult();
            await page.UnrouteAsync("**/publish/owners");
            await page.UnrouteAsync("**/publish");
        }
    }
}
