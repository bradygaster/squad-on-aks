using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SquadOnAks.Web.Workspace;

public interface IAppBrowserAcceptance
{
    Task CheckAsync(Uri origin, IReadOnlyList<AppBrowserJourney> journeys, bool mobile, CancellationToken cancellationToken);
}

internal sealed class AppBrowserCheckException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

public sealed class AppBrowserAcceptance : IAppBrowserAcceptance
{
    public async Task CheckAsync(
        Uri origin, IReadOnlyList<AppBrowserJourney> journeys, bool mobile, CancellationToken cancellationToken)
    {
        if (!origin.IsLoopback || origin.Scheme != "http" || origin.AbsolutePath != "/")
            throw new InvalidOperationException("Browser acceptance requires an owned loopback runtime origin.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ChromiumSandbox = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ServiceWorkers = ServiceWorkerPolicy.Block,
            AcceptDownloads = false,
            ViewportSize = new() { Width = mobile ? 390 : 1280, Height = mobile ? 844 : 900 }
        });
        context.SetDefaultTimeout(10_000);
        int failures = 0;
        await context.RouteAsync("**/*", async route =>
        {
            if (SameOrigin(origin, route.Request.Url)) await route.ContinueAsync();
            else { Interlocked.Increment(ref failures); await route.AbortAsync(); }
        });
        await context.RouteWebSocketAsync("**/*", route =>
        {
            if (SameOrigin(origin, route.Url, websocket: true)) route.ConnectToServer();
            else { Interlocked.Increment(ref failures); _ = route.CloseAsync(); }
        });
        IPage page = await context.NewPageAsync();
        page.PageError += (_, _) => Interlocked.Increment(ref failures);
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal))
                Interlocked.Increment(ref failures);
        };
        page.RequestFailed += (_, request) =>
        {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? url) && url.AbsolutePath != "/favicon.ico")
                Interlocked.Increment(ref failures);
        };
        page.Response += (_, response) =>
        {
            // Missing favicons are not a broken application, but missing modules/APIs are.
            if (response.Status >= 400 && new Uri(response.Url).AbsolutePath != "/favicon.ico")
                Interlocked.Increment(ref failures);
        };
        using CancellationTokenRegistration abort = cancellationToken.Register(() => _ = browser.CloseAsync());
        int journeyNumber = 0;
        int stepNumber = 0;
        string action = "load";
        try
        {
            foreach (AppBrowserJourney journey in journeys)
            {
                journeyNumber++;
                stepNumber = 0;
                action = "load";
                cancellationToken.ThrowIfCancellationRequested();
                IResponse? response = await page.GotoAsync(origin.AbsoluteUri);
                if (response?.Ok != true) throw new AppBrowserCheckException("The running application did not load.");
                foreach (AppBrowserStep step in journey.Steps)
                {
                    stepNumber++;
                    action = step.Action;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (step.Action == "reload") { await page.ReloadAsync(); continue; }
                    ILocator target = page.Locator(step.Selector);
                    switch (step.Action)
                    {
                        case "click": await target.ClickAsync(); break;
                        case "fill": await target.FillAsync(step.Value!); break;
                        case "press": await target.PressAsync(step.Value!); break;
                        case "visible": await Expect(target).ToBeVisibleAsync(); break;
                        case "text": await Expect(target).ToContainTextAsync(step.Value!); break;
                        case "count": await Expect(target).ToHaveCountAsync(int.Parse(step.Value!)); break;
                        default: throw new InvalidOperationException("An unsupported browser action was requested.");
                    }
                }
            }
            if (mobile)
            {
                await page.GotoAsync(origin.AbsoluteUri);
                if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"))
                    throw new AppBrowserCheckException("The application overflows a phone viewport.");
                await Expect(page.Locator("body")).ToBeVisibleAsync();
            }
            if (Volatile.Read(ref failures) != 0)
                throw new AppBrowserCheckException("The running app had browser, asset, API or origin errors.");
        }
        catch (PlaywrightException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new AppBrowserCheckException(
                $"Browser journey {journeyNumber}, step {stepNumber} ({action}) failed. Ask the team to check that step in app.build.json against the real application.", exception);
        }
    }

    private static bool SameOrigin(Uri origin, string url, bool websocket = false) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? value) &&
        value.Scheme == (websocket ? "ws" : "http") && value.Host == origin.Host && value.Port == origin.Port &&
        value.UserInfo.Length == 0;
}
