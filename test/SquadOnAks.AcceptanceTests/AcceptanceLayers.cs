using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        using HttpResponseMessage response = await client.GetAsync(target);
        response.EnsureSuccessStatusCode();

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(target.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
        Console.WriteLine($"{name}: reachable browser contract at {target}");
    }
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

    public static async Task<AcceptanceHostProcess> StartAsync(string repositoryRoot, string url)
    {
        string project = Path.Combine(
            repositoryRoot,
            "test",
            "SquadOnAks.AcceptanceHost",
            "SquadOnAks.AcceptanceHost.csproj");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
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
                using HttpResponseMessage response = await client.GetAsync(new Uri(baseUrl, "/harness/state"));
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
