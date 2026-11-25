using System.Text.Json;
using SquadOnAks.Web.Workspace;

// Input comes only from the host, which validated it as part of app.build.json.
// Output is one result line the host reads: never page content, cookies or response bodies.
const string Passed = "SOAKS-CHECK-PASSED: ";
const string Failed = "SOAKS-CHECK-FAILED: ";
const string Error = "SOAKS-CHECK-ERROR";
// The declared journeys (names only) that passed, in the order they ran: the app's kept promises.
var done = new List<string>();

try
{
    var origin = new Uri(Environment.GetEnvironmentVariable("SOAKS_CHECK_ORIGIN")
        ?? throw new InvalidOperationException("SOAKS_CHECK_ORIGIN is required."));
    JourneyInput input = JsonSerializer.Deserialize<JourneyInput>(
        Environment.GetEnvironmentVariable("SOAKS_CHECK_JOURNEYS") ?? "",
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("SOAKS_CHECK_JOURNEYS is required.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
    // The check copy was just deployed: wait (bounded) until it answers before the browser starts.
    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
    {
        DateTimeOffset until = DateTimeOffset.UtcNow.AddSeconds(90);
        while (true)
        {
            try
            {
                using HttpResponseMessage response = await http.GetAsync(origin, deadline.Token);
                if (response.IsSuccessStatusCode || DateTimeOffset.UtcNow > until) break;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !deadline.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow > until) break;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token);
        }
    }
    var browser = new AppBrowserAcceptance(clusterOrigin: true, chromiumSandbox: false);
    await browser.CheckAsync(origin, input.Journeys, mobile: false, deadline.Token);
    done.AddRange(input.Journeys.Select(journey => journey.Name));
    // A fresh browser reads back what the journeys saved (the database is the only state between them).
    if (input.RestartJourneys is { Count: > 0 } restart)
    {
        await browser.CheckAsync(origin, restart, mobile: false, deadline.Token);
        done.AddRange(restart.Select(journey => journey.Name));
    }
    await browser.CheckAsync(origin, [], mobile: true, deadline.Token);
    Console.WriteLine(Passed + JsonSerializer.Serialize(new { journeys = done }));
    return 0;
}
catch (AppBrowserCheckException exception)
{
    Console.WriteLine(Failed + JsonSerializer.Serialize(new
    {
        message = exception.Message,
        details = exception.Details,
        journey = exception.Journey,
        passed = done.Concat(exception.Passed)
    }));
    return 1;
}
catch (Exception exception)
{
    // Not the app's fault (for example the browser could not start): report a type, not a message.
    Console.WriteLine($"{Error}: {exception.GetType().Name}");
    return 2;
}

internal sealed record JourneyInput(
    IReadOnlyList<AppBrowserJourney> Journeys,
    IReadOnlyList<AppBrowserJourney>? RestartJourneys);
