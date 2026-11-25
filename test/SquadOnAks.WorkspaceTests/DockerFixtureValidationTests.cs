using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class DockerFixtureValidationTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        foreach (string invalid in new[]
        {
            "", "soaks-worker:local-proof", "ghcr.io/example/worker:latest",
            $"worker@sha256:{new string('a', 64)}", $"sha256:{new string('a', 63)}",
            $"ghcr.io/example/worker:latest@sha256:{new string('a', 64)}"
        })
        {
            try
            {
                _ = new DockerFixtureValidationRunner(invalid, NullLogger<DockerFixtureValidationRunner>.Instance);
                throw new InvalidOperationException($"Mutable or malformed image was accepted: {invalid}");
            }
            catch (ArgumentException)
            {
            }
        }
        _ = new DockerFixtureValidationRunner(
            $"ghcr.io/example/worker@sha256:{new string('a', 64)}",
            NullLogger<DockerFixtureValidationRunner>.Instance);
        Console.WriteLine("PASS Docker validation rejects mutable and unqualified images");

        string? image = Environment.GetEnvironmentVariable("SQUAD_VALIDATION_DOCKER_TEST_IMAGE");
        if (string.IsNullOrWhiteSpace(image))
        {
            Console.WriteLine("SKIP real Docker validation: SQUAD_VALIDATION_DOCKER_TEST_IMAGE is unset");
            return;
        }

        var runner = new DockerFixtureValidationRunner(image, NullLogger<DockerFixtureValidationRunner>.Instance);
        string appId = $"docker-validation-test-{Guid.NewGuid():N}";
        string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), appId));
        string? originalCopilot = Environment.GetEnvironmentVariable("COPILOT_TOKEN");
        string? originalGitHub = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, Path.Combine(root, "workspace"),
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            string privateState = Path.Combine(workspace.RootPath, ".squad", "session-state", "private");
            Directory.CreateDirectory(privateState);
            await File.WriteAllTextAsync(Path.Combine(privateState, "events.jsonl"), "private runtime");
            await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, ".squad", "session-store.db-wal"), "private database");
            string dependencies = Path.Combine(workspace.RootPath, "node_modules", "private");
            Directory.CreateDirectory(dependencies);
            await File.WriteAllTextAsync(Path.Combine(dependencies, "secret"), "private dependency");
            string outside = Path.Combine(root, "host-secret");
            await File.WriteAllTextAsync(outside, "private host file");
            File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "linked-secret"), outside);
            Environment.SetEnvironmentVariable("COPILOT_TOKEN", "docker-validation-copilot-canary");
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "docker-validation-github-canary");

            await WriteScriptAsync(workspace, """
                set -eu
                test "$(id -u)" = 1654
                test -f src/flight-plan.sh
                test -f tests/run.sh
                test -f /source/src/flight-plan.sh
                test ! -e .git
                test ! -e /source/.git
                test ! -e .squad/session-state
                test ! -e /source/.squad/session-state
                test ! -e .squad/session-store.db-wal
                test ! -e node_modules
                test ! -e linked-secret
                test ! -e /var/run/docker.sock
                test ! -e /root/.kube/config
                test "${COPILOT_TOKEN+x}" != x
                test "${GITHUB_TOKEN+x}" != x
                touch /tmp/scratch
                touch "$HOME/scratch"
                printf 'scratch\n' > src/flight-plan.sh
                test "$(cat src/flight-plan.sh)" = scratch
                if (printf 'escaped\n' > /source/src/flight-plan.sh) 2>/dev/null; then exit 31; fi
                if (touch /source/escaped) 2>/dev/null; then exit 32; fi
                if (touch /escaped) 2>/dev/null; then exit 33; fi
                printf 'PASS isolated Docker fixture\n'
                """);
            Dictionary<string, string> before = Snapshot(workspace.RootPath);
            FixtureValidationResult success = await runner.RunAsync(
                workspace, appId, TimeSpan.FromSeconds(30), CancellationToken.None);
            Require(success.ExitCode == 0 && success.Output.Contains("PASS isolated Docker fixture"),
                $"Docker isolation validation failed: {success.ExitCode}: {success.Output}");
            Require(before.OrderBy(pair => pair.Key).SequenceEqual(Snapshot(workspace.RootPath).OrderBy(pair => pair.Key)),
                "Docker validation must not mutate any workspace files, including private metadata.");
            await RequireNoContainersAsync(appId);
            Console.WriteLine("PASS real Docker snapshot isolation, credential exclusion, scratch writes and unchanged source");

            foreach (int exitCode in new[] { 42, 125 })
            {
                await WriteScriptAsync(workspace, $"printf 'deliberate failure\\n'; exit {exitCode}\n");
                FixtureValidationResult failure = await runner.RunAsync(
                    workspace, appId, TimeSpan.FromSeconds(30), CancellationToken.None);
                Require(failure.ExitCode == exitCode && failure.Output.Contains("deliberate failure"),
                    $"Validation must preserve the test exit code {exitCode}: {failure}");
                await RequireNoContainersAsync(appId);
            }
            Console.WriteLine("PASS real Docker nonzero test exits preserved");

            var missingImage = new DockerFixtureValidationRunner(
                $"sha256:{new string('0', 64)}", NullLogger<DockerFixtureValidationRunner>.Instance);
            try
            {
                await missingImage.RunAsync(workspace, appId, TimeSpan.FromSeconds(30), CancellationToken.None);
                throw new InvalidOperationException("A missing local image must not report validation success.");
            }
            catch (InvalidOperationException exception) when (
                exception.Message.Contains("Docker fixture validation could not execute", StringComparison.Ordinal))
            {
                Require(exception.Message.Contains("No such image", StringComparison.Ordinal),
                    "A missing local image must report the Docker error without pulling.");
            }
            await RequireNoContainersAsync(appId);
            Console.WriteLine("PASS real Docker missing local image reports an explicit configuration error without pulling");

            await WriteScriptAsync(workspace, "sleep 120\n");
            var elapsed = Stopwatch.StartNew();
            try
            {
                await runner.RunAsync(workspace, appId, TimeSpan.FromSeconds(2), CancellationToken.None);
                throw new InvalidOperationException("Docker validation did not time out.");
            }
            catch (TimeoutException)
            {
                Require(elapsed.Elapsed < TimeSpan.FromSeconds(25), "Docker timeout cleanup must be bounded.");
            }
            await RequireNoContainersAsync(appId);

            using var cancel = new CancellationTokenSource();
            Task<FixtureValidationResult> cancelled = runner.RunAsync(
                workspace, appId, TimeSpan.FromSeconds(60), cancel.Token);
            string containerName;
            try
            {
                containerName = await WaitForContainerAsync(appId);
            }
            finally
            {
                cancel.Cancel();
            }
            elapsed.Restart();
            try
            {
                await cancelled;
                throw new InvalidOperationException("Docker validation ignored cancellation.");
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                Require(elapsed.Elapsed < TimeSpan.FromSeconds(25), "Docker cancellation cleanup must be bounded.");
            }
            await RequireNoContainersAsync(appId);
            Require(!Directory.Exists(Path.Combine(AppContext.BaseDirectory, containerName)),
                "Cancellation must remove the owned export folder.");
            Console.WriteLine("PASS real Docker timeout/cancellation remove owned containers and exports");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_TOKEN", originalCopilot);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalGitHub);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Task WriteScriptAsync(DisposableFixtureWorkspace workspace, string script) =>
        File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "tests", "run.sh"), script);

    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            .ToDictionary(path => Path.GetRelativePath(root, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    private static async Task RequireNoContainersAsync(string appId)
    {
        string containers = await ContainersAsync(appId);
        Require(string.IsNullOrWhiteSpace(containers),
            $"Validation left owned containers behind: {containers}");
    }

    private static async Task<string> WaitForContainerAsync(string appId)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            string containers = await ContainersAsync(appId);
            if (!string.IsNullOrWhiteSpace(containers)) return containers.Trim();
            await Task.Delay(100, deadline.Token);
        }
    }

    private static async Task<string> ContainersAsync(string appId)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            "ps", "-a", "--filter", $"label=soaks.validation.app-id={appId}", "--format", "{{.Names}}"
        })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(bounded.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(bounded.Token);
        try
        {
            await process.WaitForExitAsync(bounded.Token);
            await Task.WhenAll(stdout, stderr);
            Require(process.ExitCode == 0, $"Could not check validation containers: {await stderr}");
            return await stdout;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
