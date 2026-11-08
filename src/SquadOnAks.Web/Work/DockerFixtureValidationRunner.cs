using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed class DockerFixtureValidationRunner : IFixtureValidationRunner
{
    private const int MaxOutputCharacters = 1024 * 1024;
    private readonly string image;
    private readonly ILogger<DockerFixtureValidationRunner> logger;

    public DockerFixtureValidationRunner(string image, ILogger<DockerFixtureValidationRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(image) ||
            !Regex.IsMatch(image,
                @"\A(?:sha256:[a-f0-9]{64}|(?:localhost|[a-z0-9]+(?:[.-][a-z0-9]+)+)(?::[0-9]+)?/[a-z0-9]+(?:[._-][a-z0-9]+)*(?:/[a-z0-9]+(?:[._-][a-z0-9]+)*)*@sha256:[a-f0-9]{64})\z",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Local validation requires an immutable sha256 image ID or registry-qualified @sha256 digest; tags are not allowed.",
                nameof(image));
        }

        this.image = image;
        this.logger = logger;
    }

    public async Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace,
        string appId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        string name = $"daydream-validate-{Guid.NewGuid():N}";
        string exportRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, name));
        string relative = Path.GetRelativePath(workspace.RootPath, exportRoot);
        if (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Validation exports must be outside the app workspace.");
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        bool dockerStarted = false;
        try
        {
            bounded.Token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(exportRoot);
            string snapshot = Path.Combine(exportRoot, "source");
            string archivePath = Path.Combine(exportRoot, "source.zip");
            await using (FileStream archive = File.Create(archivePath))
            {
                await new WorkspaceArtifacts(workspace).WriteZipAsync(archive, bounded.Token);
            }

            await ZipFile.ExtractToDirectoryAsync(archivePath, snapshot, bounded.Token);
            if (!OperatingSystem.IsWindows())
            {
                foreach (string directory in Directory.EnumerateDirectories(exportRoot, "*", SearchOption.AllDirectories)
                    .Prepend(exportRoot))
                {
                    bounded.Token.ThrowIfCancellationRequested();
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                foreach (string file in Directory.EnumerateFiles(snapshot, "*", SearchOption.AllDirectories))
                {
                    bounded.Token.ThrowIfCancellationRequested();
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                }
            }

            // Only the artifact export is mounted. Tests run on a writable, disposable tmpfs copy.
            string startedMarker = $"{name}:tests-started";
            string[] arguments =
            [
                "run", "--name", name, "--label", $"daydream.validation.app-id={appId}",
                "--rm", "--pull", "never", "--network", "none", "--read-only",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                "--user", "1654:1654", "--pids-limit", "128", "--memory", "1g", "--cpus", "1",
                "--tmpfs", "/tmp:rw,nosuid,nodev,size=512m,mode=1777",
                "--mount", $"type=bind,source={snapshot},target=/source,readonly",
                "--workdir", "/tmp", "--env", "HOME=/tmp/home", "--env", "DOTNET_CLI_HOME=/tmp/home",
                "--entrypoint", "sh", image, "-c",
                "set -eu; unset COPILOT_TOKEN GITHUB_TOKEN; mkdir -p /tmp/home /tmp/source; " +
                "cp -R /source/. /tmp/source/; cd /tmp/source; " +
                $"printf '%s\\n' '{startedMarker}' >&2; exec sh tests/run.sh"
            ];
            dockerStarted = true;
            FixtureValidationResult result = await RunDockerAsync(arguments, bounded.Token);
            if (!result.Output.Contains(startedMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Docker fixture validation could not execute (exit {result.ExitCode}): {result.Output}");
            }
            return result with
            {
                Output = result.Output.Replace(startedMarker, "", StringComparison.Ordinal).Trim()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Docker fixture validation {ContainerName} exceeded its execution limit.", name);
            throw new TimeoutException("Docker fixture validation exceeded its execution limit.");
        }
        finally
        {
            var failures = new List<Exception>();
            if (dockerStarted)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    FixtureValidationResult removed = await RunDockerAsync(
                        ["rm", "--force", name], cleanup.Token);
                    if (removed.ExitCode != 0 &&
                        !removed.Output.Contains($"No such container: {name}", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Docker validation container cleanup failed for {name}: {removed.Output}");
                    }
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to remove validation container {ContainerName}.", name);
                    failures.Add(exception);
                }
            }
            try
            {
                if (Directory.Exists(exportRoot))
                {
                    Directory.Delete(exportRoot, recursive: true);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to remove validation export {ExportPath}.", exportRoot);
                failures.Add(exception);
            }
            if (failures.Count != 0)
            {
                throw new AggregateException("Docker fixture validation cleanup failed.", failures);
            }
        }
    }

    internal static async Task<FixtureValidationResult> RunDockerAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (environment is not null)
            foreach ((string key, string value) in environment) start.Environment[key] = value;
        Process started;
        try
        {
            started = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the Docker CLI for fixture validation.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("The Docker CLI is unavailable for fixture validation.", exception);
        }
        using Process process = started;
        Task<string> stdout = ReadBoundedAsync(process.StandardOutput, cancellationToken);
        Task<string> stderr = ReadBoundedAsync(process.StandardError, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
        }
        catch
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }
            using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(reap.Token);
            try
            {
                await Task.WhenAll(stdout, stderr);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The cancelled pipe readers have been observed; the container is removed by the caller.
            }
            throw;
        }
        return new(process.ExitCode, string.Join(Environment.NewLine,
            new[] { await stdout, await stderr }.Where(value => value.Length != 0)));
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        char[] buffer = new char[4096];
        bool truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer, cancellationToken)) != 0)
        {
            int retained = Math.Min(count, MaxOutputCharacters - output.Length);
            output.Append(buffer, 0, retained);
            truncated |= retained != count;
        }
        if (truncated)
        {
            output.AppendLine().Append("[validation output truncated]");
        }
        return output.ToString().TrimEnd();
    }
}
