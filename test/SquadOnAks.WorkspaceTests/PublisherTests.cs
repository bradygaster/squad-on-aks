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
                {"full_name":"sample-user/sample","html_url":"https://github.com/sample-user/sample","private":true}
                """;
            PublishResult interrupted = await publisher.PublishAsync(
                new("sample-user", "sample", true, null), CancellationToken.None);
            Require(!interrupted.Success && interrupted.Url == "https://github.com/sample-user/sample" &&
                interrupted.FullName == "sample-user/sample" && interrupted.Message.Contains("incomplete"),
                "A failure after creation must preserve the created repository link.");
            Require(handler.LastPath == "/user/repos", "Personal publication must use the personal endpoint.");

            handler.CreateResponse = JsonSerializer.Serialize(new
            {
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
                {"full_name":"sample-user/sample","html_url":"https://github.com/sample-user/sample","private":false}
                """;
            PublishResult wrongVisibility = await publisher.PublishAsync(
                new("sample-user", "sample", true, null), CancellationToken.None);
            Require(!wrongVisibility.Success && wrongVisibility.Message.Contains("No workspace content was pushed"),
                "Unconfirmed private visibility must block pushing.");
            Console.WriteLine("PASS private-only publish requests, empty repo payload, creation errors and partial-success links");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class GitHubHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastPath { get; private set; }
        public bool Private { get; private set; }
        public bool AutoInit { get; private set; }
        public string CreateResponse { get; set; } = "{}";
        public HttpStatusCode CreateStatus { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (request.Method == HttpMethod.Get)
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"login":"sample-user"}""", Encoding.UTF8, "application/json")
                };
            }
            LastPath = request.RequestUri!.AbsolutePath;
            using JsonDocument payload = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Private = payload.RootElement.GetProperty("private").GetBoolean();
            AutoInit = payload.RootElement.GetProperty("auto_init").GetBoolean();
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
