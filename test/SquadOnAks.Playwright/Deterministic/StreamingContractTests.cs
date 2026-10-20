using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SquadOnAks.Playwright.Deterministic;

public sealed class StreamingContractTests : IAsyncLifetime
{
    private readonly List<string> consoleMessages = [];
    private readonly List<string> failedRequests = [];
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;
    private IPage? page;
    private Process? server;
    private string? baseUrl;
    private string? artifactDirectory;
    private string? publishedHostDirectory;

    [Fact]
    [Trait("Layer", "Deterministic")]
    public async Task BlazorPageStreamsThroughAgUiBoundary()
    {
        Assert.NotNull(page);
        Assert.NotNull(baseUrl);

        await page.GotoAsync(baseUrl);
        ILocator lifecycle = page.GetByRole(AriaRole.Status, new() { Name = "Lifecycle status" });
        await Expect(lifecycle).ToHaveTextAsync("Needs cast");

        await page.GetByRole(AriaRole.Button, new() { Name = "Start scripted stream" }).ClickAsync();

        await Expect(lifecycle).ToHaveTextAsync("Ready");
        await Expect(page.GetByLabel("Agent response")).ToHaveTextAsync("Squad ready");
        Assert.Empty(failedRequests);
        Assert.DoesNotContain(consoleMessages, message => message.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    public async Task InitializeAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        artifactDirectory = Path.Combine(repositoryRoot, "artifacts", "playwright", "deterministic");
        Directory.CreateDirectory(artifactDirectory);

        int port = GetAvailablePort();
        baseUrl = $"http://127.0.0.1:{port}";
        publishedHostDirectory = Path.Combine(
            Path.GetTempPath(),
            $"squad-on-aks-testhost-{Guid.NewGuid():N}");
        await PublishHostAsync(repositoryRoot, publishedHostDirectory);
        server = StartServer(publishedHostDirectory, baseUrl);
        await WaitForServerAsync(baseUrl, server);

        playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        context = await browser.NewContextAsync();
        await context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        page = await context.NewPageAsync();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.RequestFailed += (_, request) =>
            failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");
    }

    public async Task DisposeAsync()
    {
        if (page is not null && artifactDirectory is not null)
        {
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactDirectory, "final.png"),
                FullPage = true
            });
        }

        if (context is not null && artifactDirectory is not null)
        {
            await context.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactDirectory, "trace.zip")
            });
            await context.DisposeAsync();
        }

        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();

        if (artifactDirectory is not null)
        {
            await File.WriteAllLinesAsync(
                Path.Combine(artifactDirectory, "console.log"),
                consoleMessages);
            await File.WriteAllLinesAsync(
                Path.Combine(artifactDirectory, "network-failures.log"),
                failedRequests);
        }

        if (server is { HasExited: false })
        {
            server.Kill(entireProcessTree: true);
            await server.WaitForExitAsync();
        }

        server?.Dispose();

        if (publishedHostDirectory is not null && Directory.Exists(publishedHostDirectory))
        {
            Directory.Delete(publishedHostDirectory, recursive: true);
        }
    }

    private static async Task PublishHostAsync(string repositoryRoot, string outputDirectory)
    {
        string projectPath = Path.Combine(
            repositoryRoot,
            "test",
            "SquadOnAks.TestHost",
            "SquadOnAks.TestHost.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not publish the deterministic test host.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The deterministic test host publish failed with code {process.ExitCode}:{Environment.NewLine}" +
                $"{standardOutput}{Environment.NewLine}{standardError}");
        }
    }

    private static Process StartServer(string publishedHostDirectory, string url)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = publishedHostDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("SquadOnAks.TestHost.dll");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(url);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the deterministic test host.");
    }

    private static async Task WaitForServerAsync(string url, Process process)
    {
        using var client = new HttpClient();

        for (int attempt = 0; attempt < 80; attempt++)
        {
            if (process.HasExited)
            {
                string standardError = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"The deterministic test host exited with code {process.ExitCode}: {standardError}");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The deterministic test host did not become ready at {url}.");
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SquadOnAks.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
