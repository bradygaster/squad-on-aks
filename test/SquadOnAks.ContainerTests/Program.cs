using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string credential = "issue14-container-credential-sentinel";
const string publishCredential = "review-container-distinct-publishing-sentinel";
string repositoryRoot = FindRepositoryRoot();
string runId = Guid.NewGuid().ToString("N")[..12];
string image = $"squad-on-aks:issue14-{runId}";
string artifactRoot = Path.Combine(repositoryRoot, "artifacts", "container-tests", runId);
string temporaryRoot = Path.Combine(Path.GetTempPath(), "squad-on-aks-container-tests", runId);
string fixtureSource = Path.Combine(
    repositoryRoot,
    "src",
    "SquadOnAks.Web",
    "FixtureSource",
    "route-planner");
var diagnostics = new StringBuilder();
var containers = new List<string>();
var httpEvidence = new List<string>();

Directory.CreateDirectory(artifactRoot);
Directory.CreateDirectory(temporaryRoot);
Dictionary<string, string> sourceBefore = Snapshot(fixtureSource);

try
{
    var buildArguments = new List<string>
    {
        "build",
        "--file", "src/SquadOnAks.Web/Dockerfile",
        "--target", "production",
        "--tag", image
    };
    string? sdkImage = Environment.GetEnvironmentVariable(
        "SQUAD_CONTAINER_TEST_SDK_IMAGE");
    if (!string.IsNullOrWhiteSpace(sdkImage))
    {
        buildArguments.Add("--build-arg");
        buildArguments.Add($"SDK_IMAGE={sdkImage}");
    }
    buildArguments.Add(".");
    await RunRequiredAsync(
        "docker",
        buildArguments,
        repositoryRoot);
    diagnostics.AppendLine($"image={image}");
    ProcessResult imageIdentity = await RunRequiredAsync(
        "docker",
        ["image", "inspect", "--format", "{{.Id}} {{.Architecture}}", image],
        repositoryRoot);
    diagnostics.AppendLine($"image-identity={imageIdentity.Output.Trim()}");

    ContainerRun first = await StartContainerAsync("first");
    containers.Add(first.Name);
    await VerifyRuntimeBoundaryAsync(first);
    await VerifyNativeCliAsync(first);
    await VerifyFreshStartupAsync(first);
    await VerifyCastingWorkAndEvidenceAsync(first);
    await VerifyCredentialRedactionAsync(first);
    await VerifyConversationAsync(first, expected: true);
    await StopContainerAsync(first, exerciseActiveShutdown: false);

    Require(
        Directory.Exists(Path.Combine(first.Workspace, "fixture", ".squad")),
        "The first container did not persist generated harness state in its disposable mount.");
    Require(
        File.ReadAllText(Path.Combine(first.Workspace, "fixture", "src", "flight-plan.sh"))
            .Contains("LAUNCH ORBIT LAND", StringComparison.Ordinal),
        "The first container did not change the fixture application.");

    ContainerRun second = await StartContainerAsync("second");
    containers.Add(second.Name);
    await VerifyRuntimeBoundaryAsync(second);
    await VerifyFreshStartupAsync(second);
    await VerifyConversationAsync(second, expected: false);
    await VerifyCleanRoomAsync(second);
    await VerifyCredentialRedactionAsync(second);
    await StopContainerAsync(second, exerciseActiveShutdown: true);

    EqualSnapshots(
        sourceBefore,
        Snapshot(fixtureSource),
        "The packaged fixture source changed across container runs.");
    Require(
        httpEvidence.All(value => !value.Contains(credential, StringComparison.Ordinal)),
        "A credential appeared in captured HTTP output.");

    Console.Write(diagnostics);
    Console.WriteLine("PASS production image built and exercised through HTTP");
    Console.WriteLine("PASS real packaged Copilot CLI version and --agent argument without model access");
    Console.WriteLine("PASS two sequential containers proved clean-room reset");
    Console.WriteLine("PASS casting, activation, work, changes, and test evidence");
    Console.WriteLine("PASS read-only root with only /workspace writable");
    Console.WriteLine("PASS readiness and SIGTERM grace behavior");
    Console.WriteLine("PASS credential redaction and redacted failure diagnostics");
    return 0;
}
catch (Exception exception)
{
    diagnostics.AppendLine(exception.ToString());
    diagnostics.AppendLine("--- captured HTTP ---");
    foreach (string item in httpEvidence)
    {
        diagnostics.AppendLine(item);
    }
    foreach (string container in containers)
    {
        ProcessResult logs = await RunAsync("docker", ["logs", container], repositoryRoot);
        diagnostics.AppendLine($"--- {container} logs ---");
        diagnostics.AppendLine(logs.Output);
    }

    string diagnosticPath = Path.Combine(artifactRoot, "failure.log");
    await File.WriteAllTextAsync(diagnosticPath, Redact(diagnostics.ToString()));
    Console.Error.WriteLine(Redact(exception.Message));
    Console.Error.WriteLine($"Redacted diagnostics: {diagnosticPath}");
    return 1;
}
finally
{
    foreach (string container in containers)
    {
        _ = await RunAsync("docker", ["rm", "--force", container], repositoryRoot);
    }

    _ = await RunAsync("docker", ["image", "rm", "--force", image], repositoryRoot);
    if (Directory.Exists(temporaryRoot))
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
}

async Task<ContainerRun> StartContainerAsync(string suffix)
{
    string name = $"squad-issue14-{suffix}-{runId}";
    string workspace = Path.Combine(temporaryRoot, suffix);
    Directory.CreateDirectory(workspace);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            workspace,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute);
    }

    ProcessResult initialized = await RunAsync(
        "docker",
        [
            "run",
            "--rm",
            "--read-only",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--user", "1654:1654",
            "--mount", $"type=bind,source={workspace},target=/workspace",
            "--env", "SQUAD_WORKSPACE_PATH=/workspace/fixture",
            image,
            "--initialize-workspace"
        ],
        repositoryRoot);
    Require(
        initialized.ExitCode == 0,
        $"Could not initialize the {suffix} disposable workspace: {initialized.Output}");

    ProcessResult started = await RunAsync(
        "docker",
        [
            "run",
            "--detach",
            "--name", name,
            "--read-only",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--user", "1654:1654",
            "--publish", "127.0.0.1::8080",
            "--mount", $"type=bind,source={workspace},target=/workspace",
            "--env", $"COPILOT_TOKEN={credential}",
            "--env", $"GITHUB_TOKEN={publishCredential}",
            "--env", "SQUAD_WORKSPACE_PATH=/workspace/fixture",
            "--env", "SQUAD_REQUIRE_PRODUCTION_RUNTIME_BOUNDARY=true",
            "--env", "SQUAD_CONTAINER_PROBE_MODE=true",
            image
        ],
        repositoryRoot);
    Require(started.ExitCode == 0, $"Could not start {suffix} container: {started.Output}");

    ProcessResult port = await RunRequiredAsync(
        "docker",
        ["port", name, "8080/tcp"],
        repositoryRoot);
    string portText = port.Output.Trim().Split(':').Last();
    var baseUrl = new Uri($"http://127.0.0.1:{portText}");
    int unavailableSamples = await WaitForReadyAsync(baseUrl, name);
    diagnostics.AppendLine($"{suffix}.startup-unavailable-samples={unavailableSamples}");
    return new(name, workspace, baseUrl);
}

async Task<int> WaitForReadyAsync(Uri baseUrl, string container)
{
    using var client = new HttpClient { BaseAddress = baseUrl };
    var timeout = Stopwatch.StartNew();
    int unavailable = 0;
    while (timeout.Elapsed < TimeSpan.FromSeconds(60))
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync("/health/ready");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return unavailable;
            }
        }
        catch (HttpRequestException)
        {
        }

        unavailable++;
        await Task.Delay(100);
    }

    ProcessResult logs = await RunAsync("docker", ["logs", container], repositoryRoot);
    throw new InvalidOperationException(
        $"Container did not become ready within 60 seconds: {Redact(logs.Output)}");
}

async Task VerifyRuntimeBoundaryAsync(ContainerRun run)
{
    ProcessResult inspection = await RunRequiredAsync(
        "docker",
        [
            "inspect",
            "--format",
            "{{json .Mounts}}\n{{.HostConfig.ReadonlyRootfs}}\n{{.Config.User}}\n{{json .HostConfig.CapDrop}}\n{{json .HostConfig.SecurityOpt}}",
            run.Name
        ],
        repositoryRoot);
    string[] lines = inspection.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    using JsonDocument mounts = JsonDocument.Parse(lines[0]);
    JsonElement[] mountItems = mounts.RootElement.EnumerateArray().ToArray();
    Require(
        mountItems.Length == 1 &&
        mountItems[0].GetProperty("Destination").GetString() == "/workspace" &&
        mountItems[0].GetProperty("RW").GetBoolean(),
        "The container must have exactly one writable mount at /workspace.");
    Require(lines[1] == "true", "The container root filesystem is not read-only.");
    Require(lines[2] == "1654:1654", "The container is not running as the production non-root identity.");
    Require(lines[3].Contains("ALL", StringComparison.Ordinal), "The container did not drop all capabilities.");
    Require(
        lines[4].Contains("no-new-privileges", StringComparison.Ordinal),
        "The container permits privilege escalation.");

    Require(
        (await DockerExecAsync(run, "printf writable > /workspace/write-proof")).ExitCode == 0,
        "The workspace mount is not writable.");
    foreach (string path in new[] { "/app/write-proof", "/tmp/write-proof", "/home/write-proof" })
    {
        Require(
            (await DockerExecAsync(run, $"printf forbidden > {path}")).ExitCode != 0,
            $"Unexpected writable path outside /workspace: {path}");
    }
}

async Task VerifyNativeCliAsync(ContainerRun run)
{
    ProcessResult version = await DockerExecAsync(run, "\"$COPILOT_CLI_PATH\" --version");
    Require(
        version.ExitCode == 0 &&
        version.Output.Contains("GitHub Copilot CLI 1.0.103.", StringComparison.Ordinal),
        $"Production must package the pinned full Copilot CLI: {version.Output}");
    ProcessResult help = await DockerExecAsync(run, "\"$COPILOT_CLI_PATH\" --agent squad --help");
    Require(
        help.ExitCode == 0 &&
        help.Output.Contains("--agent <agent>", StringComparison.Ordinal),
        $"Production Copilot CLI must accept Squad's --agent argument: {help.Output}");
}

async Task VerifyFreshStartupAsync(ContainerRun run)
{
    using var client = new HttpClient { BaseAddress = run.BaseUrl };
    using HttpResponseMessage ready = await client.GetAsync("/health/ready");
    using HttpResponseMessage live = await client.GetAsync("/health/live");
    Require(ready.StatusCode == HttpStatusCode.OK, "Readiness was not healthy after initialization.");
    Require(live.StatusCode == HttpStatusCode.OK, "Liveness was not healthy after initialization.");
    Require(string.IsNullOrEmpty(await ready.Content.ReadAsStringAsync()), "Readiness exposed details.");
    Require(string.IsNullOrEmpty(await live.Content.ReadAsStringAsync()), "Liveness exposed details.");

    JsonElement diagnosticsJson = await GetJsonAsync(client, "/diagnostics/workspace");
    Require(
        diagnosticsJson.GetProperty("state").GetString() == "Needs cast",
        "Fresh startup did not report Needs cast.");
    Require(
        diagnosticsJson.GetProperty("harnessFiles").GetArrayLength() == 0,
        "Fresh startup contained generated harness files.");
    Require(
        diagnosticsJson.GetProperty("isEphemeral").GetBoolean(),
        "Fresh startup did not report an ephemeral workspace.");

    JsonElement cast = await GetJsonAsync(client, "/casting/status");
    Require(cast.GetProperty("state").GetString() == "Needs cast", "Fresh cast status was not Needs cast.");
    Require(!cast.GetProperty("hasCast").GetBoolean(), "Fresh startup unexpectedly had a cast.");
}

async Task VerifyCastingWorkAndEvidenceAsync(ContainerRun run)
{
    using var client = new HttpClient { BaseAddress = run.BaseUrl };
    string casting = await SendAgUiAsync(
        client,
        "casting",
        "Explicitly confirm the proposed cast.");
    Require(casting.Contains("Native cast created.", StringComparison.Ordinal), "Casting did not stream a result.");
    await VerifyHealthyAsync(client, "after casting");

    JsonElement cast = await GetJsonAsync(client, "/casting/status");
    Require(cast.GetProperty("state").GetString() == "Cast review", "Generated cast did not enter review.");
    JsonElement review = await GetJsonAsync(client, "/casting/review");
    Require(review.GetArrayLength() == 4, "Cast review did not expose the expected allowlist.");

    JsonElement routing = review.EnumerateArray().Single(
        item => item.GetProperty("path").GetString() == ".squad/routing.md");
    using HttpResponseMessage update = await client.PutAsJsonAsync(
        "/casting/review",
        new
        {
            path = ".squad/routing.md",
            content = routing.GetProperty("content").GetString() + "\n# reviewed in container\n"
        });
    update.EnsureSuccessStatusCode();

    using HttpResponseMessage activation = await client.PostAsync("/casting/activate", null);
    activation.EnsureSuccessStatusCode();
    JsonElement activationJson = await ReadJsonAsync(activation);
    Require(
        activationJson.GetProperty("state").GetString() == "Ready",
        "Cast activation did not enter Ready.");
    await VerifyHealthyAsync(client, "after activation");

    await File.AppendAllTextAsync(Path.Combine(run.Workspace, "fixture", "tests", "run.sh"),
        "\nprintf '%s\\n' \"$COPILOT_TOKEN\" \"$GITHUB_TOKEN\"\n");
    string work = await SendAgUiAsync(
        client,
        "work",
        "Repair the known fixture defect.");
    Require(work.Contains("Fixture repair complete.", StringComparison.Ordinal), "Work did not stream completion.");
    await VerifyHealthyAsync(client, "after application edits");

    JsonElement evidence = await GetJsonAsync(client, "/work/evidence");
    Require(!evidence.GetRawText().Contains(credential, StringComparison.Ordinal) &&
        !evidence.GetRawText().Contains(publishCredential, StringComparison.Ordinal),
        "Generated validation must redact both distinct environment credentials.");
    JsonElement savedWave = await GetJsonAsync(client, "/waves/1");
    Require(!savedWave.GetRawText().Contains(publishCredential, StringComparison.Ordinal) &&
        savedWave.GetRawText().Contains("[REDACTED]", StringComparison.Ordinal),
        "Saved wave evidence must redact the publishing credential.");
    JsonElement[] items = evidence.GetProperty("items").EnumerateArray().ToArray();
    Require(
        items.Any(item =>
            item.GetProperty("kind").GetString() == "specialist" &&
            item.GetProperty("state").GetString() == "Succeeded"),
        "Specialist completion evidence was missing.");
    Require(
        items.Any(item =>
            item.GetProperty("kind").GetString() == "application" &&
            item.GetProperty("path").GetString() == "src/flight-plan.sh"),
        "Application change evidence was missing.");
    Require(
        items.Any(item =>
            item.GetProperty("kind").GetString() == "validation" &&
            item.GetProperty("exitCode").GetInt32() == 0 &&
            item.GetProperty("output").GetString()!.Contains("PASS route order", StringComparison.Ordinal)),
        "Passing fixture test evidence was missing.");
}

static async Task VerifyHealthyAsync(HttpClient client, string scenario)
{
    foreach (string path in new[] { "/health/ready", "/health/live" })
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        Require(response.StatusCode == HttpStatusCode.OK, $"{path} must remain healthy {scenario}.");
        Require(string.IsNullOrEmpty(await response.Content.ReadAsStringAsync()),
            $"{path} must not expose details {scenario}.");
    }
}

async Task VerifyCredentialRedactionAsync(ContainerRun run)
{
    using var client = new HttpClient { BaseAddress = run.BaseUrl };
    string response = await SendAgUiAsync(
        client,
        "work",
        "Trigger the credential failure.");
    Require(!response.Contains(credential, StringComparison.Ordinal), "Credential leaked through AG-UI.");
    Require(response.Contains("RUN_ERROR", StringComparison.Ordinal), "AG-UI did not preserve failure diagnostics.");
    ProcessResult logs = await RunRequiredAsync(
        "docker",
        ["logs", run.Name],
        repositoryRoot);
    Require(!logs.Output.Contains(credential, StringComparison.Ordinal), "Credential leaked through container logs.");
    Require(logs.Output.Contains("[REDACTED]", StringComparison.Ordinal), "Container failure log was not redacted.");
    httpEvidence.Add(response);
}

async Task VerifyConversationAsync(ContainerRun run, bool expected)
{
    using var client = new HttpClient { BaseAddress = run.BaseUrl };
    string response = await SendAgUiAsync(
        client,
        expected ? "work" : "cast-review",
        "Send a contextual follow-up.");
    Require(
        response.Contains($"retained-context:{expected.ToString().ToLowerInvariant()}", StringComparison.Ordinal),
        $"Conversation reset expectation was not {expected}.");
}

async Task VerifyCleanRoomAsync(ContainerRun run)
{
    using var client = new HttpClient { BaseAddress = run.BaseUrl };
    string response = await SendAgUiAsync(
        client,
        "cast-review",
        "Inspect clean room.");
    Require(response.Contains("clean-room:true", StringComparison.Ordinal), "Replacement was not pristine.");
    Require(response.Contains("harness-absent:true", StringComparison.Ordinal), "Replacement retained harness files.");
    Require(
        !Directory.Exists(Path.Combine(run.Workspace, "fixture", ".squad")),
        "Replacement mount retained cast or harness state.");
    Require(
        File.ReadAllText(Path.Combine(run.Workspace, "fixture", "src", "flight-plan.sh"))
            .Contains("LAND ORBIT LAUNCH", StringComparison.Ordinal),
        "Replacement mount retained the application change.");
}

async Task StopContainerAsync(ContainerRun run, bool exerciseActiveShutdown)
{
    Task<string>? active = null;
    if (exerciseActiveShutdown)
    {
        var client = new HttpClient { BaseAddress = run.BaseUrl };
        active = SendAgUiAsync(client, "work", "Start long-running work.");
        await WaitForWorkStateAsync(client, "Active");
    }

    var stopwatch = Stopwatch.StartNew();
    ProcessResult signal = await RunAsync(
        "docker",
        ["kill", "--signal", "TERM", run.Name],
        repositoryRoot);
    Require(signal.ExitCode == 0, $"Could not send SIGTERM: {signal.Output}");

    bool unavailable = false;
    using var readinessClient = new HttpClient
    {
        BaseAddress = run.BaseUrl,
        Timeout = TimeSpan.FromMilliseconds(300)
    };
    while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
    {
        try
        {
            using HttpResponseMessage response = await readinessClient.GetAsync("/health/ready");
            unavailable = response.StatusCode != HttpStatusCode.OK;
        }
        catch (HttpRequestException)
        {
            unavailable = true;
        }
        catch (TaskCanceledException)
        {
            unavailable = true;
        }

        ProcessResult running = await RunRequiredAsync(
            "docker",
            ["inspect", "--format", "{{.State.Running}} {{.State.ExitCode}}", run.Name],
            repositoryRoot);
        if (running.Output.StartsWith("false ", StringComparison.Ordinal))
        {
            string[] state = running.Output.Trim().Split(' ');
            Require(state[1] == "0", $"Container exited non-zero after SIGTERM: {running.Output}");
            break;
        }

        await Task.Delay(100);
    }

    Require(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "SIGTERM exceeded the 10-second test grace period.");
    Require(unavailable, "Readiness remained available after SIGTERM.");
    if (active is not null)
    {
        try
        {
            _ = await active.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
        }
    }

    ProcessResult logs = await RunRequiredAsync("docker", ["logs", run.Name], repositoryRoot);
    Require(!logs.Output.Contains(credential, StringComparison.Ordinal), "Credential leaked during shutdown.");
    diagnostics.AppendLine($"{run.Name}.sigterm-ms={stopwatch.ElapsedMilliseconds}");
}

async Task WaitForWorkStateAsync(HttpClient client, string expected)
{
    var timeout = Stopwatch.StartNew();
    while (timeout.Elapsed < TimeSpan.FromSeconds(5))
    {
        JsonElement status = await GetJsonAsync(client, "/work/status");
        if (status.GetProperty("state").GetString() == expected)
        {
            return;
        }

        await Task.Delay(50);
    }

    throw new InvalidOperationException($"Work did not enter {expected}.");
}

async Task<string> SendAgUiAsync(HttpClient client, string phase, string prompt)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/ag-ui")
    {
        Content = JsonContent.Create(new
        {
            threadId = "container-thread",
            runId = Guid.NewGuid().ToString("N"),
            messages = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString("N"),
                    role = "user",
                    content = prompt
                }
            },
            state = new { },
            tools = Array.Empty<object>(),
            context = Array.Empty<object>(),
            forwardedProps = new { prompt }
        })
    };
    request.Headers.Add("X-Squad-Phase", phase);
    using HttpResponseMessage response = await client.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    httpEvidence.Add(body);
    response.EnsureSuccessStatusCode();
    return body;
}

async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
{
    using HttpResponseMessage response = await client.GetAsync(path);
    response.EnsureSuccessStatusCode();
    return await ReadJsonAsync(response);
}

static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
{
    await using Stream stream = await response.Content.ReadAsStreamAsync();
    using JsonDocument document = await JsonDocument.ParseAsync(stream);
    return document.RootElement.Clone();
}

Task<ProcessResult> DockerExecAsync(ContainerRun run, string command) =>
    RunAsync("docker", ["exec", run.Name, "sh", "-c", command], repositoryRoot);

async Task<ProcessResult> RunRequiredAsync(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory)
{
    ProcessResult result = await RunAsync(fileName, arguments, workingDirectory);
    if (result.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Command failed ({fileName} {string.Join(' ', arguments)}): {Redact(result.Output)}");
    }

    return result;
}

static async Task<ProcessResult> RunAsync(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory)
{
    ProcessStartInfo startInfo = new(fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };
    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start {fileName}.");
    Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
    Task<string> standardError = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    string output = string.Join(
        Environment.NewLine,
        new[] { await standardOutput, await standardError }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.TrimEnd()));
    return new(process.ExitCode, output);
}

static Dictionary<string, string> Snapshot(string path) =>
    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        .ToDictionary(
            file => Path.GetRelativePath(path, file).Replace('\\', '/'),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
            StringComparer.Ordinal);

static void EqualSnapshots(
    IReadOnlyDictionary<string, string> expected,
    IReadOnlyDictionary<string, string> actual,
    string message)
{
    if (!expected.OrderBy(item => item.Key, StringComparer.Ordinal)
        .SequenceEqual(actual.OrderBy(item => item.Key, StringComparer.Ordinal)))
    {
        throw new InvalidOperationException(message);
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string Redact(string value) =>
    value.Replace(credential, "[REDACTED]", StringComparison.Ordinal)
        .Replace(publishCredential, "[REDACTED]", StringComparison.Ordinal);

static string FindRepositoryRoot()
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

internal sealed record ContainerRun(string Name, string Workspace, Uri BaseUrl);

internal sealed record ProcessResult(int ExitCode, string Output);
