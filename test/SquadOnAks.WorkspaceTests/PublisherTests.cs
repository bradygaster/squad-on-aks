using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class PublisherTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        string root = Path.Combine(Path.GetTempPath(), "squad-publisher-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, root,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            var redactor = new SecretRedactor("test-publish-credential");
            var collector = new WorkEvidenceCollector(workspace, new WorkEvidenceStore(), redactor,
                NullLogger<WorkEvidenceCollector>.Instance);
            using var handler = new GitHubHandler();
            using var factory = new ClientFactory(handler);
            var publisher = new GitHubPublisher(new("test-publish-credential"), workspace, collector,
                redactor, factory, NullLogger<GitHubPublisher>.Instance);

            PublishResult publicRequest = await publisher.PublishAsync(
                new("octodemo", "sample", false, null), CancellationToken.None);
            Require(!publicRequest.Success && publicRequest.Private && handler.Requests == 0,
                "Public publication must fail before GitHub requests.");

            foreach (bool? visibility in new bool?[] { null, true })
            {
                handler.CreateResponse = """{"message":"Repository name already exists"}""";
                handler.CreateStatus = HttpStatusCode.UnprocessableEntity;
                PublishResult collision = await publisher.PublishAsync(
                    new("octodemo", "sample", visibility, null), CancellationToken.None);
                Require(!collision.Success && collision.Url is null &&
                    collision.Message.Contains("Repository name already exists"),
                    "Creation errors must not report a published repository.");
                Require(handler.LastPath == "/orgs/octodemo/repos" && handler.Private && !handler.AutoInit,
                    "Org publication must explicitly request a private empty repository.");
            }

            handler.CreateStatus = HttpStatusCode.Created;
            handler.CreateResponse = """
                {"id":101,"full_name":"sample-user/incomplete","html_url":"https://github.com/sample-user/incomplete","private":true}
                """;
            PublishResult interrupted = await publisher.PublishAsync(
                new("sample-user", "incomplete", true, null), CancellationToken.None);
            Require(!interrupted.Success && interrupted.Url == "https://github.com/sample-user/incomplete" &&
                interrupted.FullName == "sample-user/incomplete" && interrupted.Message.Contains("incomplete"),
                "A failure after creation must preserve the created repository link.");
            Require(handler.LastPath == "/user/repos", "Personal publication must use the personal endpoint.");

            handler.CreateResponse = JsonSerializer.Serialize(new
            {
                id = 102,
                full_name = "sample-user/sample",
                html_url = "https://github.com/sample-user/sample",
                @private = true,
                clone_url = new Uri(Path.Combine(root, "nonexistent-remote.git")).AbsoluteUri
            });
            PublishResult failedPush = await publisher.PublishAsync(
                new("sample-user", "sample", true, null), CancellationToken.None);
            Require(!failedPush.Success && failedPush.Url == "https://github.com/sample-user/sample" &&
                failedPush.Message.Contains("push failed"),
                "A failed Git push must preserve the created repository link without reporting success.");

            handler.CreateResponse = """
                {"id":103,"full_name":"sample-user/not-private","html_url":"https://github.com/sample-user/not-private","private":false}
                """;
            PublishResult wrongVisibility = await publisher.PublishAsync(
                new("sample-user", "not-private", true, null), CancellationToken.None);
            Require(!wrongVisibility.Success && wrongVisibility.Message.Contains("No workspace content was pushed"),
                "Unconfirmed private visibility must block pushing.");

            await VerifyTaintedHistoryBlocksPublishAsync(fixtureSource, root);
            await VerifyFailedPushRetryUsesSamePrivateRepositoryAsync(fixtureSource, root);
            await VerifyConcurrentPublishDoesNotCreateDuplicateAsync(fixtureSource, root);
            await VerifyRetryNeverFallsBackToPublicAsync(fixtureSource, root);
            await VerifyRetryRequiresTheSameRepositoryIdentityAsync(fixtureSource, root);
            Console.WriteLine("PASS private-only publish requests, empty repo payload, creation errors and partial-success links");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task VerifyTaintedHistoryBlocksPublishAsync(string fixtureSource, string root)
    {
        string testRoot = Path.Combine(root, "tainted-history");
        using var harness = new PublisherHarness(fixtureSource, testRoot);
        string secretPath = Path.Combine(testRoot, "src", "history-marker.js");
        string runtimePath = Path.Combine(testRoot, ".squad", "session-state", "session", "events.jsonl");
        string copilotRuntimePath = Path.Combine(testRoot, ".copilot", "runtime", "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(secretPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(copilotRuntimePath)!);
        await File.WriteAllTextAsync(secretPath,
            "const retiredCredential = 'ghp_test_tainted_secret_12345678901234567890';\n");
        await File.WriteAllTextAsync(runtimePath, "{\"event\":\"private runtime transcript\"}\n");
        await File.WriteAllTextAsync(copilotRuntimePath, "{\"session\":\"private runtime data\"}\n");
        await GitAsync(testRoot, "add", "src/history-marker.js");
        await GitAsync(testRoot, "add", "-f", ".squad/session-state/session/events.jsonl");
        await GitAsync(testRoot, "add", "-f", ".copilot/runtime/session.json");
        await CommitAsync(testRoot, "historical workspace snapshot");
        File.Delete(secretPath);
        File.Delete(runtimePath);
        File.Delete(copilotRuntimePath);
        await GitAsync(testRoot, "add", "-u");
        await CommitAsync(testRoot, "remove private history markers");
        string[] historyBefore = await ReadHistoryAsync(testRoot);

        PublishResult result = await harness.Publisher.PublishAsync(
            new("sample-user", "tainted-history", true, null), CancellationToken.None);

        Require(!result.Success && result.Message.Contains("history", StringComparison.OrdinalIgnoreCase) &&
            result.Message.Contains("remove", StringComparison.OrdinalIgnoreCase),
            "Tainted history must block publication with actionable guidance.");
        Require(harness.Handler.Requests == 0,
            "Tainted history must be rejected before any remote GitHub request.");
        string[] historyAfter = await ReadHistoryAsync(testRoot);
        Require(historyBefore.SequenceEqual(historyAfter),
            "A tainted publish attempt must not rewrite commit SHAs.");
    }

    private static async Task VerifyFailedPushRetryUsesSamePrivateRepositoryAsync(
        string fixtureSource,
        string root)
    {
        string testRoot = Path.Combine(root, "retry-same-repository");
        using var harness = new PublisherHarness(fixtureSource, testRoot);
        string remotePath = await CreateBareRepositoryAsync(testRoot, failFirstPush: true);
        ConfigureRepository(harness.Handler, "retry-same-repository", remotePath, isPrivate: true);
        var request = new PublishRequest("sample-user", "retry-same-repository", true, null);

        PublishResult first = await harness.Publisher.PublishAsync(request, CancellationToken.None);
        Require(!first.Success && first.Url == "https://github.com/sample-user/retry-same-repository",
            "The first failed push must preserve the created private repository link.");
        Require(Directory.Exists(Path.Combine(testRoot, ".git", "squad-publish")),
            "A created repository must be durably recorded in the workspace before pushing.");

        PublishResult retry = await harness.Publisher.PublishAsync(request, CancellationToken.None);
        Require(retry.Success && retry.Url == first.Url,
            "Retry after a failed push must complete to the original repository.");
        Require(harness.Handler.CreateRequests == 1 && harness.Handler.RepositoryGetRequests == 1,
            "Retry must verify and reuse the exact created repository without creating another.");
        Require(harness.Handler.AllCreateRequestsPrivate,
            "Create requests must remain private-only.");
        string localHead = (await GitAsync(testRoot, "rev-parse", "HEAD")).Trim();
        string remoteHead = (await GitAsync(
            testRoot,
            "-c", "safe.bareRepository=all",
            "--git-dir", remotePath,
            "rev-parse", "refs/heads/main")).Trim();
        Require(remoteHead == localHead,
            "Retry must push the original workspace commit SHA without rewriting history.");

        PublishResult repeated = await harness.Publisher.PublishAsync(request, CancellationToken.None);
        Require(repeated.Success && repeated.Url == first.Url && harness.Handler.CreateRequests == 1,
            "A repeated publication click must remain idempotent after success.");
    }

    private static async Task VerifyConcurrentPublishDoesNotCreateDuplicateAsync(
        string fixtureSource,
        string root)
    {
        string testRoot = Path.Combine(root, "concurrent-publish");
        using var harness = new PublisherHarness(fixtureSource, testRoot);
        string remotePath = await CreateBareRepositoryAsync(testRoot, failFirstPush: false);
        ConfigureRepository(harness.Handler, "concurrent-publish", remotePath, isPrivate: true);
        harness.Handler.CreateDelay = TimeSpan.FromMilliseconds(100);
        var request = new PublishRequest("sample-user", "concurrent-publish", true, null);

        PublishResult[] results = await Task.WhenAll(
            harness.Publisher.PublishAsync(request, CancellationToken.None),
            harness.Publisher.PublishAsync(request, CancellationToken.None));

        Require(results.All(result => result.Success) && results.Select(result => result.Url).Distinct().Count() == 1,
            "Concurrent same-destination submissions must converge on one published repository.");
        Require(harness.Handler.CreateRequests == 1 && harness.Handler.AllCreateRequestsPrivate,
            "Concurrent same-destination submissions must not issue duplicate or public creation requests.");
    }

    private static async Task VerifyRetryNeverFallsBackToPublicAsync(string fixtureSource, string root)
    {
        string testRoot = Path.Combine(root, "no-public-fallback");
        using var harness = new PublisherHarness(fixtureSource, testRoot);
        string remotePath = await CreateBareRepositoryAsync(testRoot, failFirstPush: true);
        ConfigureRepository(harness.Handler, "no-public-fallback", remotePath, isPrivate: true);
        var request = new PublishRequest("sample-user", "no-public-fallback", true, null);

        PublishResult first = await harness.Publisher.PublishAsync(request, CancellationToken.None);
        Require(!first.Success, "The first push should fail in the retry visibility test.");
        ConfigureRepository(harness.Handler, "no-public-fallback", remotePath, isPrivate: false);
        PublishResult retry = await harness.Publisher.PublishAsync(request, CancellationToken.None);

        Require(!retry.Success && retry.Private &&
            retry.Message.Contains("private", StringComparison.OrdinalIgnoreCase),
            "A retry must stop with an actionable error if the created repository is no longer private.");
        Require(harness.Handler.CreateRequests == 1 && harness.Handler.AllCreateRequestsPrivate,
            "A failed private retry must never create a second or public fallback repository.");
        (int remoteHeadExitCode, _) = await RunGitAsync(
            remotePath, "rev-parse", "--verify", "refs/heads/main");
        Require(remoteHeadExitCode != 0,
            "No workspace content may be pushed after private visibility cannot be confirmed.");
    }

    private static async Task VerifyRetryRequiresTheSameRepositoryIdentityAsync(
        string fixtureSource,
        string root)
    {
        string testRoot = Path.Combine(root, "repository-identity");
        using var harness = new PublisherHarness(fixtureSource, testRoot);
        string remotePath = await CreateBareRepositoryAsync(testRoot, failFirstPush: true);
        ConfigureRepository(harness.Handler, "repository-identity", remotePath, isPrivate: true);
        var request = new PublishRequest("sample-user", "repository-identity", true, null);

        PublishResult first = await harness.Publisher.PublishAsync(request, CancellationToken.None);
        Require(!first.Success, "The first push should fail in the repository identity test.");
        ConfigureRepository(
            harness.Handler,
            "repository-identity",
            remotePath,
            isPrivate: true,
            repositoryId: 4243);
        PublishResult retry = await harness.Publisher.PublishAsync(request, CancellationToken.None);

        Require(!retry.Success && retry.Message.Contains("saved repository", StringComparison.OrdinalIgnoreCase),
            "A retry must refuse a same-name repository with a different GitHub identity.");
        Require(harness.Handler.CreateRequests == 1,
            "An identity mismatch must not create another repository.");
        (int remoteHeadExitCode, _) = await RunGitAsync(
            remotePath, "rev-parse", "--verify", "refs/heads/main");
        Require(remoteHeadExitCode != 0,
            "A retry must not push to a different repository with the same name.");
    }

    private static void ConfigureRepository(
        GitHubHandler handler,
        string name,
        string cloneUrl,
        bool isPrivate,
        long repositoryId = 4242)
    {
        string response = JsonSerializer.Serialize(new
        {
            id = repositoryId,
            full_name = $"sample-user/{name}",
            html_url = $"https://github.com/sample-user/{name}",
            @private = isPrivate,
            clone_url = new Uri(cloneUrl).AbsoluteUri
        });
        handler.CreateResponse = response;
        handler.RepositoryResponse = response;
    }

    private static async Task<string> CreateBareRepositoryAsync(string root, bool failFirstPush)
    {
        string parent = Path.GetDirectoryName(root)!;
        string remoteName = Path.GetFileName(root);
        string remotePath = Path.Combine(parent, $"{remoteName}-remote.git");
        Directory.CreateDirectory(parent);
        await GitAsync(root, "init", "--bare", remotePath);
        if (failFirstPush)
        {
            string markerPath = Path.Combine(parent, $"{remoteName}-first-push-failed");
            string hookPath = Path.Combine(remotePath, "hooks", "pre-receive");
            await File.WriteAllTextAsync(hookPath, $"""
                #!/bin/sh
                if [ ! -f "{markerPath}" ]; then
                  : > "{markerPath}"
                  echo "intentional first-push failure" >&2
                  exit 1
                fi
                exit 0
                """);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(hookPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        return remotePath;
    }

    private static async Task<string> CommitAsync(string root, string message)
    {
        await GitAsync(root, "-c", "user.name=Publisher test", "-c",
            "user.email=publisher-test@example.invalid", "commit", "--quiet", "-m", message);
        return (await GitAsync(root, "rev-parse", "HEAD")).Trim();
    }

    private static async Task<string[]> ReadHistoryAsync(string root) =>
        (await GitAsync(root, "rev-list", "--reverse", "HEAD"))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private static async Task<string> GitAsync(
        string workingDirectory,
        params string[] arguments)
    {
        (int exitCode, string output) = await RunGitAsync(workingDirectory, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");
        }
        return output;
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
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
            ?? throw new InvalidOperationException("Could not start git.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode,
            string.Join(Environment.NewLine, new[] { await stdout, await stderr }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.TrimEnd())));
    }

    private sealed class PublisherHarness : IDisposable
    {
        private readonly ClientFactory factory;

        public PublisherHarness(string fixtureSource, string root)
        {
            Workspace = new DisposableFixtureWorkspace(
                fixtureSource,
                root,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            Workspace.Reset();
            var redactor = new SecretRedactor("test-publish-credential");
            var collector = new WorkEvidenceCollector(
                Workspace,
                new WorkEvidenceStore(),
                redactor,
                NullLogger<WorkEvidenceCollector>.Instance);
            Handler = new GitHubHandler();
            factory = new ClientFactory(Handler);
            Publisher = new GitHubPublisher(
                new("test-publish-credential"),
                Workspace,
                collector,
                redactor,
                factory,
                NullLogger<GitHubPublisher>.Instance);
        }

        public DisposableFixtureWorkspace Workspace { get; }
        public GitHubPublisher Publisher { get; }
        public GitHubHandler Handler { get; }

        public void Dispose() => factory.Dispose();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class GitHubHandler : HttpMessageHandler
    {
        private int requests;
        private int createRequests;
        private int repositoryGetRequests;
        private bool allCreateRequestsPrivate = true;

        public int Requests => Volatile.Read(ref requests);
        public int CreateRequests => Volatile.Read(ref createRequests);
        public int RepositoryGetRequests => Volatile.Read(ref repositoryGetRequests);
        public string? LastPath { get; private set; }
        public bool Private { get; private set; }
        public bool AutoInit { get; private set; }
        public bool AllCreateRequestsPrivate => allCreateRequestsPrivate;
        public TimeSpan CreateDelay { get; set; }
        public string CreateResponse { get; set; } = "{}";
        public string RepositoryResponse { get; set; } = "{}";
        public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.Created;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                Interlocked.Increment(ref requests);
                if (request.RequestUri!.AbsolutePath.StartsWith("/repos/", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref repositoryGetRequests);
                    return new(HttpStatusCode.OK)
                    {
                        Content = new StringContent(RepositoryResponse, Encoding.UTF8, "application/json")
                    };
                }
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"login":"sample-user"}""", Encoding.UTF8, "application/json")
                };
            }
            Interlocked.Increment(ref requests);
            Interlocked.Increment(ref createRequests);
            LastPath = request.RequestUri!.AbsolutePath;
            using JsonDocument payload = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Private = payload.RootElement.GetProperty("private").GetBoolean();
            allCreateRequestsPrivate &= Private;
            AutoInit = payload.RootElement.GetProperty("auto_init").GetBoolean();
            if (CreateDelay > TimeSpan.Zero)
            {
                await Task.Delay(CreateDelay, cancellationToken);
            }
            return new(CreateStatus)
            {
                Content = new StringContent(CreateResponse, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        private readonly List<HttpClient> clients = [];
        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(handler, disposeHandler: false);
            clients.Add(client);
            return client;
        }
        public void Dispose()
        {
            foreach (HttpClient client in clients) client.Dispose();
        }
    }
}
