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

/// <summary>A draft pull request Squad opened (or found already open) for an imported app's branch.</summary>
public sealed record PullRequestResult(bool Success, int Number, string? Url, IReadOnlyList<int> Closes, string Message, bool Draft = true);

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

    /// <summary>
    /// Sends an imported app's waves back to the repository it came from, on its own branch (never the default
    /// branch). Only the changes made here are scanned, since the rest of the history is the repository's own.
    /// </summary>
    public async Task<PublishResult> PushBranchAsync(CancellationToken cancellationToken)
    {
        if (workspace.Imported is not { BaseCommit: { } baseCommit } imported)
            return new(false, null, null, true, "This app wasn't started from a GitHub repository.");
        if (!IsConfigured)
            return new(false, null, null, true, "No GitHub token is configured.");
        await publishGate.WaitAsync(cancellationToken);
        try
        {
            await collector.CheckpointAsync("Squad on AKS export", cancellationToken);
            (int countExit, string count) = await RunGitAsync(["rev-list", "--count", $"{baseCommit}..HEAD"], null, cancellationToken);
            if (countExit != 0) throw new InvalidOperationException("Could not read this app's work history.");
            if (count.Trim() == "0")
                return new(false, imported.HtmlUrl, imported.FullName, true, "There are no waves to send yet.");
            if (await HasTaintedChangesAsync(baseCommit, cancellationToken))
                return new(false, imported.HtmlUrl, imported.FullName, true,
                    "Sending is blocked because the team's changes contain credentials or Squad/Copilot runtime files. " +
                    "Ask the team in a wave to remove them; commits are never rewritten automatically.");
            string basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{settings.Token}"));
            (int exitCode, string output) = await DisposableFixtureWorkspace.GitAsync(workspace.RootPath, basic, TimeSpan.FromMinutes(2),
                cancellationToken, "push", "--quiet", imported.CloneUrl, $"HEAD:refs/heads/{imported.Branch}");
            if (exitCode != 0)
            {
                logger.LogWarning("Branch push to {Repository} failed: {Failure}", imported.FullName, redactor.Redact(output));
                return new(false, imported.HtmlUrl, imported.FullName, true,
                    output.Contains("rejected", StringComparison.OrdinalIgnoreCase)
                        ? $"GitHub rejected the push: {imported.Branch} was changed there. Nothing was overwritten."
                        : "The push to GitHub failed. Check that the token can write to this repository, then try again.");
            }
            string compare = $"{imported.HtmlUrl}/compare/{Uri.EscapeDataString(imported.DefaultBranch)}...{imported.Branch}";
            logger.LogInformation("Sent waves to {Repository} branch {Branch}.", imported.FullName, imported.Branch);
            return new(true, compare, imported.FullName, true,
                $"Sent {count.Trim()} {(count.Trim() == "1" ? "commit" : "commits")} to {imported.Branch} on {imported.FullName}.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            logger.LogWarning("Branch push failed: {Failure}", redactor.Redact(exception.Message));
            return new(false, imported.HtmlUrl, imported.FullName, true, "The push to GitHub failed. Try again in a moment.");
        }
        finally
        {
            publishGate.Release();
        }
    }

    public const string PullRequestRefused = "GitHub didn't allow a pull request on this repository.";
    private const string PullRequestFailed = "The draft pull request couldn't be opened. Try again in a moment.";

    // Squad's own instructions appended to what the person asked; the same markers the UI strips.
    private static readonly string[] InstructionMarkers = ["\n\n(Also give this app", "\n\n(When you finish, ", "\n\n(Squad on AKS"];

    /// <summary>
    /// Sends the app's waves to its own branch (the same checks as <see cref="PushBranchAsync"/>), then opens a draft pull request
    /// from that branch into the default branch, unless one is already open. GitHub's error text is logged, never returned.
    /// </summary>
    public async Task<PullRequestResult> OpenPullRequestAsync(IReadOnlyList<string> objectives, CancellationToken cancellationToken)
    {
        PublishResult pushed = await PushBranchAsync(cancellationToken);
        if (!pushed.Success) return new(false, 0, null, [], pushed.Message);
        ImportedRepository imported = workspace.Imported!;
        IReadOnlyList<int> closes = LinkedIssues(objectives);
        string repo = $"repos/{Uri.EscapeDataString(imported.Owner)}/{Uri.EscapeDataString(imported.Name)}";
        try
        {
            HttpClient client = CreateClient();
            string head = Uri.EscapeDataString($"{imported.Owner}:{imported.Branch}");
            using (HttpResponseMessage open = await client.GetAsync($"{repo}/pulls?head={head}&state=open", cancellationToken))
            {
                string listed = await open.Content.ReadAsStringAsync(cancellationToken);
                if (!open.IsSuccessStatusCode) return PullRequestRefusal(imported, open.StatusCode, listed);
                using JsonDocument list = JsonDocument.Parse(listed);
                if (list.RootElement.ValueKind == JsonValueKind.Array &&
                    list.RootElement.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } existing)
                    // Report what GitHub has for that pull request, not what Squad would have written.
                    return new(true, existing.GetProperty("number").GetInt32(), existing.GetProperty("html_url").GetString(),
                        GitHubRepositories.ClosedIssues(existing.TryGetProperty("body", out JsonElement existingBody) &&
                            existingBody.ValueKind == JsonValueKind.String ? existingBody.GetString() : null),
                        "A pull request is already open for this branch.",
                        existing.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True);
            }

            string? title = null;
            if (closes.Count > 0)
            {
                using HttpResponseMessage issue = await client.GetAsync($"{repo}/issues/{closes[0]}", cancellationToken);
                if (issue.IsSuccessStatusCode)
                {
                    using JsonDocument detail = JsonDocument.Parse(await issue.Content.ReadAsStringAsync(cancellationToken));
                    title = detail.RootElement.TryGetProperty("title", out JsonElement value) ? value.GetString() : null;
                }
            }
            using HttpResponseMessage created = await client.PostAsJsonAsync($"{repo}/pulls", new
            {
                title = PullRequestTitle(title, objectives),
                head = imported.Branch,
                @base = imported.DefaultBranch,
                body = PullRequestBody(closes),
                draft = true
            }, cancellationToken);
            string answer = await created.Content.ReadAsStringAsync(cancellationToken);
            if (!created.IsSuccessStatusCode) return PullRequestRefusal(imported, created.StatusCode, answer);
            using JsonDocument pull = JsonDocument.Parse(answer);
            int number = pull.RootElement.GetProperty("number").GetInt32();
            logger.LogInformation("Opened draft pull request #{Number} on {Repository}.", number, imported.FullName);
            return new(true, number, pull.RootElement.GetProperty("html_url").GetString(), closes, "Draft pull request opened.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Opening a pull request on {Repository} failed: {Failure}", imported.FullName, redactor.Redact(exception.Message));
            return new(false, 0, null, [], PullRequestFailed);
        }
    }

    private PullRequestResult PullRequestRefusal(ImportedRepository imported, System.Net.HttpStatusCode status, string body)
    {
        logger.LogWarning("GitHub answered {Status} for a pull request on {Repository}: {Body}", (int)status, imported.FullName,
            redactor.Redact(body.Length > 300 ? body[..300] : body));
        return new(false, 0, null, [],
            status is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.UnprocessableEntity ? PullRequestRefused : PullRequestFailed);
    }

    /// <summary>Requests of the waves that finished; failed or cancelled work never names or closes an issue.</summary>
    public static IReadOnlyList<string> CompletedObjectives(IEnumerable<WaveSummary> waves) =>
        [.. waves.Where(wave => wave.State == "Completed").Select(wave => wave.Objective)];

    /// <summary>Issue numbers the waves worked on, from requests that start "Work on issue #N:".</summary>
    public static IReadOnlyList<int> LinkedIssues(IEnumerable<string> objectives) =>
        [.. objectives.Select(objective => IssueRequestPattern().Match(WithoutInstructions(objective).TrimStart()))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Where(number => number > 0)
            .Distinct()];

    /// <summary>The first linked issue's title, else the latest wave's request (without Squad's own instructions).</summary>
    public static string PullRequestTitle(string? issueTitle, IReadOnlyList<string> objectives)
    {
        string title = !string.IsNullOrWhiteSpace(issueTitle) ? issueTitle
            : objectives.Select(WithoutInstructions).LastOrDefault(objective => !string.IsNullOrWhiteSpace(objective)) ?? "";
        title = title.Trim().Split('\n', 2)[0].Trim();
        if (title.Length > 120) title = title[..120].TrimEnd() + "…";
        return title.Length > 0 ? title : "Changes from Squad";
    }

    public static string PullRequestBody(IReadOnlyList<int> closes) =>
        string.Concat(closes.Select(number => $"Closes #{number}\n")) + (closes.Count > 0 ? "\n" : "") + "Made with Squad on AKS.";

    private static string WithoutInstructions(string? objective)
    {
        string value = objective ?? "";
        foreach (string marker in InstructionMarkers)
        {
            int cut = value.IndexOf(marker, StringComparison.Ordinal);
            if (cut >= 0) value = value[..cut];
        }
        return value;
    }

    [GeneratedRegex(@"^Work on issue #(\d{1,9}):", RegexOptions.CultureInvariant)]
    internal static partial Regex IssueRequestPattern();

    private async Task<bool> HasTaintedChangesAsync(string baseCommit, CancellationToken cancellationToken)
    {
        (int pathExit, string paths) = await RunGitAsync(["diff", "--name-only", "-z", baseCommit, "HEAD"], null, cancellationToken);
        if (pathExit != 0) throw new InvalidOperationException("Could not inspect the team's changes.");
        if (paths.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(path =>
            WorkspacePathPolicy.IsSquadRuntimePath(path) || IsCopilotRuntimePath(path) || IsCredentialPath(path)))
            return true;
        (int diffExit, string diff) = await RunGitAsync(["diff", "--no-ext-diff", "-U0", baseCommit, "HEAD"], null, cancellationToken);
        if (diffExit != 0) throw new InvalidOperationException("Could not inspect the team's changes.");
        string added = string.Join('\n', diff.Split('\n').Where(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)));
        return Regex.IsMatch(added, CredentialPattern.Replace("[[:space:]]", "\\s", StringComparison.Ordinal)) ||
            (!string.IsNullOrEmpty(settings.Token) && added.Contains(settings.Token, StringComparison.Ordinal));
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

public sealed record GitHubRepositorySummary(string Name, string FullName, bool Private, string? Description, string DefaultBranch,
    DateTimeOffset? PushedAt, int OpenIssues = 0);

/// <summary>
/// An open issue as shown to the person: no body, only what is needed to list it. The plan fields come from the repository itself:
/// Depth and Step from GitHub sub-issues (Step is the position in its parent's list, of Steps), Steps for an issue that has sub-issues,
/// and WaitingOn for its still-open blockers (GitHub's "blocked by" links, or a "Blocked by #N" line written by the repository's team).
/// </summary>
public sealed record GitHubIssueSummary(int Number, string Title, string Url, IReadOnlyList<string> Labels, bool FromTeam,
    int Depth = 0, int? Step = null, int Steps = 0, IReadOnlyList<int>? WaitingOn = null);

/// <summary>An open pull request; Closes is parsed from its body on the server, and the body itself is never returned.</summary>
public sealed record GitHubPullSummary(int Number, string Title, string Url, bool Draft, IReadOnlyList<int> Closes);

public sealed record GitHubRepositoryWork(IReadOnlyList<GitHubIssueSummary> Issues, IReadOnlyList<GitHubPullSummary> PullRequests)
{
    public static readonly GitHubRepositoryWork Empty = new([], []);
}

/// <summary>One open issue, read on the server only so its text can be given to the team as data.</summary>
public sealed record GitHubIssueDetail(int Number, string Title, IReadOnlyList<string> Labels, string? Body, bool FromTeam);

public sealed record GitHubRepositoryDetail(string FullName, string HtmlUrl, string DefaultBranch, bool Private, bool HasSquad,
    bool HasContract);

/// <summary>Reads the person's GitHub repositories (names and metadata only) so an app can start from one.</summary>
public sealed partial class GitHubRepositories(GitHubPublisherSettings settings, IHttpClientFactory httpClientFactory, SecretRedactor redactor,
    ILogger<GitHubRepositories> logger)
{
    private static readonly TimeSpan WorkCacheTime = TimeSpan.FromSeconds(60);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset At, GitHubRepositoryWork Work)> workCache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Token);

    public string Token => settings.Token ?? throw new InvalidOperationException("No GitHub token is configured.");

    public async Task<IReadOnlyList<GitHubRepositorySummary>> ListAsync(string owner, CancellationToken cancellationToken)
    {
        ValidateOwner(owner);
        HttpClient client = CreateClient();
        using JsonDocument user = await GetJsonAsync(client, "user", cancellationToken);
        bool self = string.Equals(user.RootElement.GetProperty("login").GetString(), owner, StringComparison.OrdinalIgnoreCase);
        // The person's own repositories include private ones only through /user/repos.
        string path = self
            ? "user/repos?affiliation=owner&sort=pushed&per_page=100"
            : $"orgs/{Uri.EscapeDataString(owner)}/repos?sort=pushed&per_page=100";
        using JsonDocument list = await GetJsonAsync(client, path, cancellationToken);
        return [.. list.RootElement.EnumerateArray()
            .Where(item => !item.GetProperty("archived").GetBoolean())
            .Select(item => new GitHubRepositorySummary(
                item.GetProperty("name").GetString()!,
                item.GetProperty("full_name").GetString()!,
                item.GetProperty("private").GetBoolean(),
                item.TryGetProperty("description", out JsonElement description) && description.ValueKind == JsonValueKind.String
                    ? description.GetString() : null,
                item.GetProperty("default_branch").GetString()!,
                item.TryGetProperty("pushed_at", out JsonElement pushed) && pushed.ValueKind == JsonValueKind.String
                    ? pushed.GetDateTimeOffset() : null,
                item.TryGetProperty("open_issues_count", out JsonElement open) && open.ValueKind == JsonValueKind.Number
                    ? open.GetInt32() : 0))
            .OrderByDescending(item => item.PushedAt)];
    }

    public async Task<(GitHubRepositoryDetail Detail, string CloneUrl)> GetAsync(string owner, string name, CancellationToken cancellationToken)
    {
        ValidateOwner(owner);
        if (!RepositoryPattern().IsMatch(name) || name is "." or "..")
            throw new ArgumentException("Choose a valid repository.");
        HttpClient client = CreateClient();
        string repo = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";
        using JsonDocument detail = await GetJsonAsync(client, repo, cancellationToken);
        bool Exists(string path)
        {
            using HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Head, $"{repo}/contents/{path}"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        JsonElement root = detail.RootElement;
        return (new(root.GetProperty("full_name").GetString()!, root.GetProperty("html_url").GetString()!,
                root.GetProperty("default_branch").GetString()!, root.GetProperty("private").GetBoolean(),
                Exists(".squad/team.md"), Exists(AppBuildManifest.FileName)),
            root.GetProperty("clone_url").GetString()!);
    }

    /// <summary>
    /// The repository's open issues and pull requests (titles, links and labels only), cached for a minute. A repository whose
    /// issues or pull requests can't be read (no access, turned off, gone) has an empty list, not an error.
    /// </summary>
    public async Task<GitHubRepositoryWork> GetWorkAsync(string owner, string name, CancellationToken cancellationToken)
    {
        ValidateRepository(owner, name);
        string key = $"{owner}/{name}";
        if (workCache.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow - cached.At < WorkCacheTime) return cached.Work;
        HttpClient client = CreateClient();
        string repo = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";
        List<GitHubIssueSummary> issues = [];
        var parents = new Dictionary<int, int>();
        var blockers = new Dictionary<int, HashSet<int>>();
        var withChildren = new List<int>();
        var nativeBlocked = new List<int>();
        using (JsonDocument? list = await GetListAsync(client, $"{repo}/issues?state=open&sort=created&direction=desc&per_page=100", cancellationToken))
        {
            if (list is not null)
                foreach (JsonElement item in list.RootElement.EnumerateArray())
                {
                    // The issues list includes pull requests; they are listed separately.
                    if (item.TryGetProperty("pull_request", out _)) continue;
                    int number = item.GetProperty("number").GetInt32();
                    issues.Add(new(number, Text(item, "title"), Text(item, "html_url"), Labels(item), FromTeam(item)));
                    if (ParentPattern().Match(Text(item, "parent_issue_url")) is { Success: true } parent)
                        parents[number] = int.Parse(parent.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (Count(item, "sub_issues_summary", "total") > 0) withChildren.Add(number);
                    if (Count(item, "issue_dependencies_summary", "blocked_by") > 0) nativeBlocked.Add(number);
                    // Only the repository's own people can say what blocks an issue; only issue numbers are kept from the line.
                    if (FromTeam(item))
                        foreach (Match line in BlockedByLinePattern().Matches(Text(item, "body")))
                            foreach (Match reference in IssueReferencePattern().Matches(line.Value))
                                Add(blockers, number, int.Parse(reference.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                }
        }
        // Sub-issue order and GitHub's own "blocked by" links, for a bounded number of issues.
        var children = new Dictionary<int, List<int>>();
        foreach (int parent in withChildren.Take(MaxPlanReads))
            using (JsonDocument? list = await GetListAsync(client, $"{repo}/issues/{parent}/sub_issues?per_page=100", cancellationToken))
                if (list is not null)
                    children[parent] = [.. list.RootElement.EnumerateArray().Select(child => child.GetProperty("number").GetInt32())];
        foreach (int number in nativeBlocked.Take(MaxPlanReads))
            using (JsonDocument? list = await GetListAsync(client, $"{repo}/issues/{number}/dependencies/blocked_by?per_page=100", cancellationToken))
                if (list is not null)
                    foreach (JsonElement blocker in list.RootElement.EnumerateArray())
                        Add(blockers, number, blocker.GetProperty("number").GetInt32());
        issues = PlanOrder(issues, parents, children, blockers);
        List<GitHubPullSummary> pulls = [];
        using (JsonDocument? list = await GetListAsync(client, $"{repo}/pulls?state=open&per_page=10", cancellationToken))
        {
            if (list is not null)
                foreach (JsonElement item in list.RootElement.EnumerateArray())
                    pulls.Add(new(item.GetProperty("number").GetInt32(), Text(item, "title"), Text(item, "html_url"),
                        item.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True,
                        ClosedIssues(Text(item, "body"))));
        }
        var work = new GitHubRepositoryWork(issues, pulls);
        workCache[key] = (DateTimeOffset.UtcNow, work);
        return work;
    }

    private const int MaxPlanReads = 10;

    /// <summary>
    /// Puts open issues in the order the repository sets out: each issue with its open sub-issues right after it, in GitHub's
    /// sub-issue order, and everything else newest first. WaitingOn keeps only blockers that are still open.
    /// </summary>
    internal static List<GitHubIssueSummary> PlanOrder(IReadOnlyList<GitHubIssueSummary> issues, IReadOnlyDictionary<int, int> parents,
        IReadOnlyDictionary<int, List<int>> children, IReadOnlyDictionary<int, HashSet<int>> blockers)
    {
        var open = issues.ToDictionary(issue => issue.Number);
        bool Nested(int number) => parents.TryGetValue(number, out int parent) && open.ContainsKey(parent) &&
            children.TryGetValue(parent, out List<int>? siblings) && siblings.Contains(number);
        var ordered = new List<GitHubIssueSummary>();
        var placed = new HashSet<int>();
        void Place(GitHubIssueSummary issue, int depth, int? step)
        {
            if (!placed.Add(issue.Number)) return;
            List<int> mine = children.TryGetValue(issue.Number, out List<int>? list) ? list : [];
            int[] waiting = blockers.TryGetValue(issue.Number, out HashSet<int>? blocked)
                ? [.. blocked.Where(number => number != issue.Number && open.ContainsKey(number)).Order()] : [];
            ordered.Add(issue with { Depth = depth, Step = step, Steps = mine.Count, WaitingOn = waiting });
            for (int index = 0; index < mine.Count; index++)
                if (open.TryGetValue(mine[index], out GitHubIssueSummary? child)) Place(child, Math.Min(depth + 1, 3), index + 1);
        }
        foreach (GitHubIssueSummary issue in issues)
            if (!Nested(issue.Number)) Place(issue, 0, null);
        foreach (GitHubIssueSummary issue in issues) Place(issue, 0, null);
        return ordered;
    }

    private static void Add(Dictionary<int, HashSet<int>> map, int key, int value)
    {
        if (value <= 0) return;
        if (!map.TryGetValue(key, out HashSet<int>? set)) map[key] = set = [];
        set.Add(value);
    }

    private static int Count(JsonElement item, string summary, string property) =>
        item.TryGetProperty(summary, out JsonElement value) && value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out JsonElement count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 0;

    /// <summary>Drops the cached issues and pull requests for a repository, after Squad changed them.</summary>
    public void Forget(string owner, string name) => workCache.TryRemove($"{owner}/{name}", out _);

    /// <summary>One open issue (not a pull request), or null when it can't be read or isn't open.</summary>
    public async Task<GitHubIssueDetail?> GetIssueAsync(string owner, string name, int number, CancellationToken cancellationToken)
    {
        ValidateRepository(owner, name);
        HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/issues/{number}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogInformation("GitHub issue #{Number} on {Owner}/{Name} couldn't be read ({Status}).", number, owner, name, (int)response.StatusCode);
            return null;
        }
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement item = document.RootElement;
        if (item.TryGetProperty("pull_request", out _) || Text(item, "state") != "open") return null;
        return new(item.GetProperty("number").GetInt32(), Text(item, "title"), Labels(item),
            item.TryGetProperty("body", out JsonElement body) && body.ValueKind == JsonValueKind.String ? body.GetString() : null,
            FromTeam(item));
    }

    /// <summary>Issue numbers a pull request body says it closes ("closes #12", "fixes #3", "resolves #7").</summary>
    public static IReadOnlyList<int> ClosedIssues(string? body) =>
        [.. ClosingPattern().Matches(body ?? "").Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Where(number => number > 0).Distinct()];

    private static string Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string[] Labels(JsonElement item) =>
        item.TryGetProperty("labels", out JsonElement labels) && labels.ValueKind == JsonValueKind.Array
            ? [.. labels.EnumerateArray()
                .Select(label => label.ValueKind == JsonValueKind.String ? label.GetString() : Text(label, "name"))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label!)]
            : [];

    // Only the repository's own people count as the team; anyone else's text is never given to the team beyond its title.
    private static bool FromTeam(JsonElement item) =>
        Text(item, "author_association") is "OWNER" or "MEMBER" or "COLLABORATOR";

    private async Task<JsonDocument?> GetListAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
            return null;
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GitHub {Path} answered {Status}: {Body}", path.Split('?')[0], (int)response.StatusCode,
                redactor.Redact(body.Length > 200 ? body[..200] : body));
            throw new InvalidOperationException("GitHub couldn't be reached for this account.");
        }
        JsonDocument document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind == JsonValueKind.Array) return document;
        document.Dispose();
        return null;
    }

    private static void ValidateRepository(string owner, string name)
    {
        ValidateOwner(owner);
        if (!RepositoryPattern().IsMatch(name ?? "") || name is "." or "..")
            throw new ArgumentException("Choose a valid repository.");
    }

    private static void ValidateOwner(string owner)
    {
        if (!OwnerPattern().IsMatch(owner ?? "")) throw new ArgumentException("Choose a valid GitHub account or organization.");
    }

    private HttpClient CreateClient()
    {
        if (!IsConfigured) throw new InvalidOperationException("No GitHub token is configured.");
        HttpClient client = httpClientFactory.CreateClient(GitHubPublisher.HttpClientName);
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("squad-on-aks");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private async Task<JsonDocument> GetJsonAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("GitHub couldn't find that repository, or the configured token can't read it.");
            // GitHub's own error text stays in the logs; the person sees a plain sentence.
            logger.LogWarning("GitHub {Path} answered {Status}: {Body}", path.Split('?')[0], (int)response.StatusCode,
                redactor.Redact(body.Length > 200 ? body[..200] : body));
            throw new InvalidOperationException("GitHub couldn't be reached for this account.");
        }
        return JsonDocument.Parse(body);
    }

    [GeneratedRegex(@"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s+#(\d{1,9})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ClosingPattern();

    // "Blocked by #4 and #5." up to the end of its sentence, as Squad teams write dependencies in issue bodies.
    [GeneratedRegex(@"\bblocked\s+by\b:?[^\n.]{0,200}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BlockedByLinePattern();

    [GeneratedRegex(@"(?<![\w/])#(\d{1,9})\b", RegexOptions.CultureInvariant)]
    private static partial Regex IssueReferencePattern();

    [GeneratedRegex(@"/issues/(\d{1,9})$", RegexOptions.CultureInvariant)]
    private static partial Regex ParentPattern();

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();
}
