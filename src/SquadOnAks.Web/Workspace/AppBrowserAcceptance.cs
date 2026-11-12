using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SquadOnAks.Web.Workspace;

public interface IAppBrowserAcceptance
{
    Task CheckAsync(Uri origin, IReadOnlyList<AppBrowserJourney> journeys, bool mobile, CancellationToken cancellationToken);
}

/// <summary>
/// A journey failed. <see cref="Exception.Message"/> is a fixed sentence safe to show people;
/// <see cref="Details"/> are bounded notes (step, selector, failing requests) for the team only.
/// </summary>
public sealed class AppBrowserCheckException(string message, IReadOnlyList<string>? details = null, Exception? inner = null)
    : InvalidOperationException(message, inner)
{
    public IReadOnlyList<string> Details { get; } = details ?? [];
}

/// <param name="clusterOrigin">
/// Allow an in-cluster service origin (the cluster journey runner). Otherwise only an owned loopback runtime is accepted.
/// </param>
/// <param name="chromiumSandbox">
/// Chromium's own sandbox. The cluster runner turns it off because its credential-free, egress-locked pod is the sandbox
/// and restricted pods cannot create the namespaces Chromium's sandbox needs.
/// </param>
public sealed class AppBrowserAcceptance(bool clusterOrigin = false, bool chromiumSandbox = true) : IAppBrowserAcceptance
{
    private const int MaxNotes = 12;

    public async Task CheckAsync(
        Uri origin, IReadOnlyList<AppBrowserJourney> journeys, bool mobile, CancellationToken cancellationToken)
    {
        bool allowedHost = origin.IsLoopback ||
            clusterOrigin && origin.Host.EndsWith(".svc.cluster.local", StringComparison.Ordinal);
        if (!allowedHost || origin.Scheme != "http" || origin.AbsolutePath != "/" || origin.UserInfo.Length != 0)
            throw new InvalidOperationException("Browser acceptance requires an owned loopback runtime origin.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ChromiumSandbox = chromiumSandbox,
            Args = chromiumSandbox ? [] : ["--disable-dev-shm-usage"]
        });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ServiceWorkers = ServiceWorkerPolicy.Block,
            AcceptDownloads = false,
            ViewportSize = new() { Width = mobile ? 390 : 1280, Height = mobile ? 844 : 900 }
        });
        // A cluster check runs against a copy that started moments ago (cold API and database), so allow it longer.
        context.SetDefaultTimeout(clusterOrigin ? 30_000 : 10_000);
        int failures = 0;
        var notes = new List<string>();
        void Note(string text)
        {
            lock (notes)
                if (notes.Count < MaxNotes && !notes.Contains(text)) notes.Add(text.Length > 300 ? text[..300] : text);
        }
        string Where(string url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? value)
            ? (value.Host == origin.Host && value.Port == origin.Port ? value.PathAndQuery : $"{value.Scheme}://{value.Host}{value.AbsolutePath}")
            : "(invalid URL)";
        await context.RouteAsync("**/*", async route =>
        {
            if (SameOrigin(origin, route.Request.Url)) await route.ContinueAsync();
            else
            {
                Interlocked.Increment(ref failures);
                Note($"Blocked a request that leaves the app's own origin: {route.Request.Method} {Where(route.Request.Url)}");
                await route.AbortAsync();
            }
        });
        await context.RouteWebSocketAsync("**/*", route =>
        {
            if (SameOrigin(origin, route.Url, websocket: true)) route.ConnectToServer();
            else
            {
                Interlocked.Increment(ref failures);
                Note($"Blocked a WebSocket that leaves the app's own origin: {Where(route.Url)}");
                _ = route.CloseAsync();
            }
        });
        var pending = new Dictionary<IRequest, string>();
        IPage page = await context.NewPageAsync();
        page.Request += (_, request) => { lock (pending) pending[request] = $"{request.Method} {Where(request.Url)}"; };
        page.RequestFinished += (_, request) => { lock (pending) pending.Remove(request); };
        page.RequestFailed += (_, request) => { lock (pending) pending.Remove(request); };
        page.PageError += (_, error) =>
        {
            Interlocked.Increment(ref failures);
            Note($"Uncaught page error: {FirstLine(error)}");
        };
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref failures);
                Note($"Console error: {FirstLine(message.Text)}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            // A navigation that a fresh container's network interrupts is retried below, not the app's fault.
            if (Transient(request.Failure)) return;
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? url) && url.AbsolutePath != "/favicon.ico")
            {
                Interlocked.Increment(ref failures);
                Note($"Request failed: {request.Method} {Where(request.Url)}");
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status < 400 || new Uri(response.Url).AbsolutePath == "/favicon.ico") return;
            // An API's own 4xx answer (validation, not found, conflict) is app behaviour a journey may test on purpose;
            // a 4xx page asset (script, style, document) or any 5xx is a broken application.
            bool api = response.Request.ResourceType is "fetch" or "xhr";
            if (!api || response.Status >= 500) Interlocked.Increment(ref failures);
            Note($"{response.Request.Method} {Where(response.Url)} answered {response.Status}");
        };
        using CancellationTokenRegistration abort = cancellationToken.Register(() => _ = browser.CloseAsync());
        int journeyNumber = 0;
        int stepNumber = 0;
        string action = "load";
        string selector = "";
        string journeyName = "";
        try
        {
            foreach (AppBrowserJourney journey in journeys)
            {
                journeyNumber++;
                journeyName = journey.Name;
                stepNumber = 0;
                action = "load";
                selector = "";
                cancellationToken.ThrowIfCancellationRequested();
                IResponse? response = await RetryAsync(() => page.GotoAsync(origin.AbsoluteUri), cancellationToken);
                if (response?.Ok != true)
                    throw new AppBrowserCheckException("The running application did not load.",
                        Notes($"The root page answered {response?.Status.ToString() ?? "nothing"}."));
                foreach (AppBrowserStep step in journey.Steps)
                {
                    stepNumber++;
                    action = step.Action;
                    selector = step.Selector;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (step.Action == "reload") { await RetryAsync(() => page.ReloadAsync(), cancellationToken); continue; }
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
                await RetryAsync(() => page.GotoAsync(origin.AbsoluteUri), cancellationToken);
                if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"))
                    throw new AppBrowserCheckException("The application overflows a phone viewport.",
                        Notes("At a 390px-wide phone viewport the page is wider than the screen."));
                await Expect(page.Locator("body")).ToBeVisibleAsync();
            }
            if (Volatile.Read(ref failures) != 0)
                throw new AppBrowserCheckException("The running app had browser, asset, API or origin errors.",
                    Notes("Every journey step passed, but the app reported the errors below."));
        }
        // Playwright's step timeout must count as a journey failure (with the step named), whatever its base type.
        catch (Exception exception) when (exception is PlaywrightException or System.TimeoutException ||
            exception.GetType().FullName == "Microsoft.Playwright.TimeoutException")
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (pending)
                foreach (string waiting in pending.Values.Distinct().Take(5))
                    Note($"Still waiting for an answer when the step gave up: {waiting}");
            throw new AppBrowserCheckException(
                $"Browser journey {journeyNumber}, step {stepNumber} ({action}) failed. Ask the team to check that step in app.build.json against the real application.",
                Notes($"Journey \"{journeyName}\", step {stepNumber}: {action} {selector} — {FirstLine(exception.Message)}"),
                exception);
        }

        IReadOnlyList<string> Notes(string first)
        {
            lock (notes) return [first, .. notes];
        }
    }

    // Chromium reports these when the container's network settles or a just-started server isn't accepting yet.
    private static bool Transient(string? failure) => failure is not null &&
        (failure.Contains("ERR_NETWORK_CHANGED", StringComparison.Ordinal) ||
         failure.Contains("ERR_CONNECTION_REFUSED", StringComparison.Ordinal) ||
         failure.Contains("ERR_CONNECTION_RESET", StringComparison.Ordinal) ||
         failure.Contains("ERR_EMPTY_RESPONSE", StringComparison.Ordinal));

    private static async Task<IResponse?> RetryAsync(Func<Task<IResponse?>> navigate, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return await navigate(); }
            catch (PlaywrightException exception) when (attempt < 5 && Transient(exception.Message))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static string FirstLine(string text)
    {
        string line = text.Split('\n', 2)[0].Trim();
        return line.Length > 240 ? line[..240] : line;
    }

    private static bool SameOrigin(Uri origin, string url, bool websocket = false) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? value) &&
        value.Scheme == (websocket ? "ws" : "http") && value.Host == origin.Host && value.Port == origin.Port &&
        value.UserInfo.Length == 0;
}
