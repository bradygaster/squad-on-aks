using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;

namespace SquadOnAks.Web.Workspace;

public sealed record PublishOwners(bool Available, string? Login, IReadOnlyList<string> Organizations, string? Reason);

public sealed record PublishRequest(string Owner, string Name, bool? Private, string? Description);

public sealed record PublishResult(bool Success, string? Url, string? FullName, bool Private, string Message);

public sealed record GitHubPublisherSettings(string? Token);

public sealed partial class GitHubPublisher(
    GitHubPublisherSettings settings,
    DisposableFixtureWorkspace workspace,
    WorkEvidenceCollector collector,
    SecretRedactor redactor,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubPublisher> logger)
{
    public const string HttpClientName = "github";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Token);

    public async Task<PublishOwners> GetOwnersAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new(false, null, [], "No GitHub token is configured for publishing.");
        }

        try
        {
            HttpClient client = CreateClient();
            using JsonDocument user = await GetJsonAsync(client, "user", cancellationToken);
            string login = user.RootElement.GetProperty("login").GetString()!;
            using JsonDocument organizations = await GetJsonAsync(client, "user/orgs?per_page=100", cancellationToken);
            string[] orgs = organizations.RootElement.EnumerateArray()
                .Select(item => item.GetProperty("login").GetString()!)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new(true, login, orgs, null);
        }
        catch (Exception exception)
        {
            string reason = redactor.Redact(exception.Message)!;
            logger.LogWarning("Could not list GitHub publish owners: {Failure}", reason);
            return new(false, null, [], reason);
        }
    }

    public async Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken)
    {
        bool isPrivate = request.Private ?? true;
        if (!IsConfigured)
        {
            return new(false, null, null, isPrivate, "No GitHub token is configured for publishing.");
        }

        string owner = request.Owner?.Trim() ?? string.Empty;
        string name = request.Name?.Trim() ?? string.Empty;
        if (!OwnerPattern().IsMatch(owner))
        {
            return new(false, null, null, isPrivate, "Choose a valid GitHub account or organization.");
        }

        if (!RepositoryPattern().IsMatch(name) || name is "." or "..")
        {
            return new(false, null, null, isPrivate,
                "Repository names may use letters, numbers, '.', '-', and '_' (1–100 characters).");
        }

        try
        {
            await collector.CheckpointAsync("Squad on AKS export", cancellationToken);

            HttpClient client = CreateClient();
            using JsonDocument user = await GetJsonAsync(client, "user", cancellationToken);
            string login = user.RootElement.GetProperty("login").GetString()!;
            string endpoint = string.Equals(owner, login, StringComparison.OrdinalIgnoreCase)
                ? "user/repos"
                : $"orgs/{owner}/repos";
            using HttpResponseMessage created = await client.PostAsJsonAsync(
                endpoint,
                new
                {
                    name,
                    description = string.IsNullOrWhiteSpace(request.Description)
                        ? "Built by a Squad on AKS"
                        : request.Description.Trim(),
                    @private = isPrivate,
                    auto_init = false
                },
                cancellationToken);
            string body = await created.Content.ReadAsStringAsync(cancellationToken);
            if (!created.IsSuccessStatusCode)
            {
                string reason = ExtractGitHubMessage(body) ?? created.ReasonPhrase ?? "unknown error";
                return new(false, null, null, isPrivate,
                    $"GitHub refused to create {owner}/{name}: {redactor.Redact(reason)}");
            }

            using JsonDocument repository = JsonDocument.Parse(body);
            string fullName = repository.RootElement.GetProperty("full_name").GetString()!;
            string htmlUrl = repository.RootElement.GetProperty("html_url").GetString()!;
            string cloneUrl = repository.RootElement.GetProperty("clone_url").GetString()!;

            (int exitCode, string output) = await PushAsync(cloneUrl, cancellationToken);
            if (exitCode != 0)
            {
                return new(false, htmlUrl, fullName, isPrivate,
                    $"Created {fullName}, but the push failed: {redactor.Redact(output)}");
            }

            logger.LogInformation("Published workspace to {Repository}.", fullName);
            return new(true, htmlUrl, fullName, isPrivate, $"Published to {fullName}.");
        }
        catch (Exception exception)
        {
            string reason = redactor.Redact(exception.Message)!;
            logger.LogWarning("Publishing failed: {Failure}", reason);
            return new(false, null, null, isPrivate, $"Publishing failed: {reason}");
        }
    }

    private async Task<(int ExitCode, string Output)> PushAsync(string cloneUrl, CancellationToken cancellationToken)
    {
        string basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{settings.Token}"));
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_CONFIG_COUNT"] = "2";
        startInfo.Environment["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
        startInfo.Environment["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: basic {basic}";
        startInfo.Environment["GIT_CONFIG_KEY_1"] = "credential.helper";
        startInfo.Environment["GIT_CONFIG_VALUE_1"] = "";
        foreach (string argument in new[] { "push", "--quiet", cloneUrl, "HEAD:refs/heads/main" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromMinutes(2));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(bounded.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(bounded.Token);
        try
        {
            await process.WaitForExitAsync(bounded.Token);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(reap.Token);
            try
            {
                await Task.WhenAll(stdout, stderr);
            }
            catch (OperationCanceledException)
            {
            }
            throw;
        }
        return (process.ExitCode, ((await stdout) + (await stderr)).Trim());
    }

    private HttpClient CreateClient()
    {
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("squad-on-aks");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub {path} returned {(int)response.StatusCode}: {ExtractGitHubMessage(body)}");
        }

        return JsonDocument.Parse(body);
    }

    private static string? ExtractGitHubMessage(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            string? message = document.RootElement.TryGetProperty("message", out JsonElement value)
                ? value.GetString()
                : null;
            if (document.RootElement.TryGetProperty("errors", out JsonElement errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                string details = string.Join("; ", errors.EnumerateArray()
                    .Select(error => error.TryGetProperty("message", out JsonElement detail)
                        ? detail.GetString()
                        : error.ToString()));
                message = string.IsNullOrWhiteSpace(details) ? message : $"{message} ({details})";
            }

            return message;
        }
        catch (JsonException)
        {
            return body.Length > 200 ? body[..200] : body;
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();
}
