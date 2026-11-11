using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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

public sealed record PublishedRepository(string FullName, string Url);

public sealed partial class GitHubPublisher(
    GitHubPublisherSettings settings,
    DisposableFixtureWorkspace workspace,
    WorkEvidenceCollector collector,
    SecretRedactor redactor,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubPublisher> logger)
{
    public const string HttpClientName = "github";

    private const string CredentialPattern =
        "(gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|" +
        "-----BEGIN [A-Z ]*PRIVATE KEY-----|[\"']?(api[_-]?key|access[_-]?token|auth[_-]?token|" +
        "client[_-]?secret|password|github[_-]?token|copilot[_-]?token|secret)[\"']?" +
        "[[:space:]]*[:=][[:space:]]*[\"']?[A-Za-z0-9_+/=-]{24,})";

    private readonly SemaphoreSlim publishGate = new(1, 1);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Token);

    /// <summary>Repositories this app created on GitHub. Deleting the app leaves them in place.</summary>
    public IReadOnlyList<PublishedRepository> ListPublished()
    {
        string directory = Path.Combine(workspace.RootPath, ".git", "squad-publish");
        if (!Directory.Exists(directory)) return [];
        var repositories = new List<PublishedRepository>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<PersistedPublication>(File.ReadAllText(path)) is { RepositoryId: > 0 } saved)
                    repositories.Add(new(saved.FullName, saved.Url));
            }
            catch (JsonException)
            {
                logger.LogWarning("A saved publication record is unreadable.");
            }
        }
        return [.. repositories.OrderBy(repository => repository.FullName, StringComparer.OrdinalIgnoreCase)];
    }

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
        const bool isPrivate = true;
        if (request.Private is false)
        {
            return new(false, null, null, true, "Publishing supports new private repositories only.");
        }
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

        await publishGate.WaitAsync(cancellationToken);
        try
        {
            return await PublishValidatedAsync(owner, name, request, cancellationToken);
        }
        finally
        {
            publishGate.Release();
        }
    }

    private async Task<PublishResult> PublishValidatedAsync(
        string owner,
        string name,
        PublishRequest request,
        CancellationToken cancellationToken)
    {
        const bool isPrivate = true;
        string? createdUrl = null;
        string? createdFullName = null;
        try
        {
            await collector.CheckpointAsync("Squad on AKS export", cancellationToken);
            if (await HasTaintedHistoryAsync(cancellationToken))
            {
                return new(false, null, null, isPrivate,
                    "Publishing is blocked because the full Git history contains credentials or Squad/Copilot runtime files. " +
                    "Remove the sensitive content from every published commit and rotate exposed credentials; " +
                    "commits are never rewritten automatically.");
            }

            HttpClient client = CreateClient();
            PersistedPublication? saved = await ReadPublicationAsync(owner, name, cancellationToken);
            if (saved is not null)
            {
                createdUrl = saved.Url;
                createdFullName = saved.FullName;
                using JsonDocument existingRepository = await GetJsonAsync(
                    client,
                    $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}",
                    cancellationToken);
                if (!existingRepository.RootElement.GetProperty("private").GetBoolean())
                {
                    return new(false, saved.Url, saved.FullName, true,
                        $"GitHub no longer confirms {saved.FullName} is private. No workspace content was pushed, " +
                        "and no public fallback will be created.");
                }

                string existingFullName = existingRepository.RootElement.GetProperty("full_name").GetString()!;
                if (!string.Equals(existingFullName, saved.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return new(false, saved.Url, saved.FullName, true,
                        "GitHub returned a different repository than the one saved for retry. No content was pushed.");
                }

                long existingRepositoryId = existingRepository.RootElement.GetProperty("id").GetInt64();
                if (existingRepositoryId != saved.RepositoryId)
                {
                    return new(false, saved.Url, saved.FullName, true,
                        "The saved repository no longer matches this destination. No content was pushed.");
                }

                string existingCloneUrl = existingRepository.RootElement.GetProperty("clone_url").GetString()!;
                return await PushToRepositoryAsync(existingCloneUrl, saved, cancellationToken);
            }

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
                    @private = true,
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
            long repositoryId = repository.RootElement.GetProperty("id").GetInt64();
            createdFullName = fullName;
            createdUrl = htmlUrl;
            var publication = new PersistedPublication(owner, name, fullName, htmlUrl, repositoryId);
            await SavePublicationAsync(publication, cancellationToken);
            if (!repository.RootElement.GetProperty("private").GetBoolean())
            {
                return new(false, htmlUrl, fullName, true,
                    $"GitHub did not confirm private visibility for {fullName}. No workspace content was pushed.");
            }
            string cloneUrl = repository.RootElement.GetProperty("clone_url").GetString()!;
            return await PushToRepositoryAsync(cloneUrl, publication, cancellationToken);
        }
        catch (Exception exception)
        {
            string reason = redactor.Redact(exception.Message)!;
            logger.LogWarning("Publishing failed: {Failure}", reason);
            return new(false, createdUrl, createdFullName, isPrivate,
                createdUrl is null
                    ? $"Publishing failed: {reason}"
                    : $"Created {createdFullName}, but publication is incomplete: {reason} " +
                    "Retry this same private repository; no replacement repository will be created.");
        }
    }

    private async Task<PublishResult> PushToRepositoryAsync(
        string cloneUrl,
        PersistedPublication publication,
        CancellationToken cancellationToken)
    {
        (int exitCode, string output) = await PushAsync(cloneUrl, cancellationToken);
        if (exitCode != 0)
        {
            return new(false, publication.Url, publication.FullName, true,
                $"Created {publication.FullName}, but the push failed. Retry this same private repository: " +
                redactor.Redact(output));
        }

        logger.LogInformation("Published workspace to {Repository}.", publication.FullName);
        return new(true, publication.Url, publication.FullName, true, $"Published to {publication.FullName}.");
    }

    private async Task<bool> HasTaintedHistoryAsync(CancellationToken cancellationToken)
    {
        (int historyExitCode, string history) = await RunGitAsync(
            ["rev-list", "--reverse", "HEAD"],
            null,
            cancellationToken);
        if (historyExitCode != 0)
        {
            throw new InvalidOperationException("Could not inspect the full workspace commit history.");
        }

        foreach (string commit in history.Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            (int pathExitCode, string paths) = await RunGitAsync(
                ["ls-tree", "-r", "--name-only", "-z", commit],
                null,
                cancellationToken);
            if (pathExitCode != 0)
            {
                throw new InvalidOperationException("Could not inspect paths in the full workspace history.");
            }

            if (paths.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(path =>
                WorkspacePathPolicy.IsSquadRuntimePath(path) ||
                IsCopilotRuntimePath(path) ||
                IsCredentialPath(path)))
            {
                return true;
            }

            (int credentialExitCode, _) = await RunGitAsync(
                ["grep", "-I", "-q", "-E", CredentialPattern, commit, "--"],
                null,
                cancellationToken);
            if (credentialExitCode == 0)
            {
                return true;
            }
            if (credentialExitCode != 1)
            {
                throw new InvalidOperationException("Could not scan content in the full workspace history.");
            }

            if (!string.IsNullOrEmpty(settings.Token))
            {
                (int tokenExitCode, _) = await RunGitAsync(
                    ["grep", "-I", "-q", "-F", "-f", "-", commit, "--"],
                    settings.Token,
                    cancellationToken);
                if (tokenExitCode == 0)
                {
                    return true;
                }
                if (tokenExitCode != 1)
                {
                    throw new InvalidOperationException("Could not scan credentials in the full workspace history.");
                }
            }
        }

        return false;
    }

    private static bool IsCopilotRuntimePath(string path)
    {
        string[] segments = path.Replace('\\', '/').Split('/');
        return segments.Any(segment =>
            segment.Equals(".copilot", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".copilot-runtime", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".copilot-session-state", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCredentialPath(string path)
    {
        string file = Path.GetFileName(path);
        if (file is ".env.example" or ".env.sample" or ".env.template")
        {
            return false;
        }

        return file.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            file.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("credentials", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("credentials.json", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("credentials.yml", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("credentials.yaml", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("secrets.json", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("secrets.yml", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("secrets.yaml", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("secrets.toml", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("id_rsa", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("id_ed25519", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".p12", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".key", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PersistedPublication?> ReadPublicationAsync(
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        string path = PublicationStatePath(owner, name);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        PersistedPublication? publication;
        try
        {
            publication = JsonSerializer.Deserialize<PersistedPublication>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The saved publication retry state is corrupt; refusing to create another repository.",
                exception);
        }

        if (publication is null ||
            !string.Equals(publication.Owner, owner, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(publication.Name, name, StringComparison.OrdinalIgnoreCase) ||
            publication.RepositoryId <= 0)
        {
            throw new InvalidOperationException(
                "The saved publication retry state does not match this destination; refusing to create another repository.");
        }

        return publication;
    }

    private async Task SavePublicationAsync(
        PersistedPublication publication,
        CancellationToken cancellationToken)
    {
        string path = PublicationStatePath(publication.Owner, publication.Name);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(publication),
            cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private string PublicationStatePath(string owner, string name)
    {
        string key = $"{owner.ToUpperInvariant()}/{name.ToUpperInvariant()}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(workspace.RootPath, ".git", "squad-publish", $"{hash}.json");
    }

    private sealed record PersistedPublication(
        string Owner,
        string Name,
        string FullName,
        string Url,
        long RepositoryId);

    private async Task<(int ExitCode, string Output)> RunGitAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspace.RootPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git history inspection.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(30));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(bounded.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(bounded.Token);
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteLineAsync(standardInput.AsMemory(), bounded.Token);
                process.StandardInput.Close();
            }
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
            throw;
        }

        return (process.ExitCode, ((await stdout) + (await stderr)).Trim());
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
