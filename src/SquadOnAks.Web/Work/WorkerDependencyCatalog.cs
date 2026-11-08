using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed class WorkerDependencyCatalog(IReadOnlyDictionary<string, string> images)
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string[]>> packages = new(StringComparer.Ordinal);

    public async Task<string> InstructionsAsync(CancellationToken cancellationToken)
    {
        var hints = new List<string>();
        foreach ((string profile, string image) in images)
        {
            if (!packages.TryGetValue(profile, out IReadOnlyDictionary<string, string[]>? known))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                FixtureValidationResult result = await DockerFixtureValidationRunner.RunDockerAsync(
                    ["run", "--rm", "--pull", "never", "--network", "none", "--read-only",
                        "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "1654:1654",
                        "--entrypoint", "sh", image, "-c",
                        "if test -d /worker/nuget; then find /worker/nuget -mindepth 2 -maxdepth 2 -type d; fi"],
                    timeout.Token);
                if (result.ExitCode != 0)
                    throw new InvalidOperationException("A declared dependency profile image is unavailable. No app code has been run.");
                known = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim().Split('/'))
                    .Where(parts => parts.Length == 5 && parts[1] == "worker" && parts[2] == "nuget" &&
                        Regex.IsMatch(parts[3], @"\A[a-z0-9][a-z0-9.-]{0,99}\z") &&
                        Regex.IsMatch(parts[4], @"\A[0-9][a-z0-9.+-]{0,59}\z"))
                    .GroupBy(parts => parts[3], StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Select(parts => parts[4]).Distinct().ToArray(),
                        StringComparer.OrdinalIgnoreCase);
                packages[profile] = known;
            }
            string pins = string.Join("; ", known.Where(pair =>
                pair.Key.StartsWith("aspire.", StringComparison.Ordinal) ||
                pair.Key.StartsWith("microsoft.entityframeworkcore", StringComparison.Ordinal) ||
                pair.Key.StartsWith("microsoft.data.sqlite", StringComparison.Ordinal) ||
                pair.Key is "npgsql" or "npgsql.entityframeworkcore.postgresql")
                .OrderBy(pair => pair.Key).Take(30)
                .Select(pair => $"{pair.Key}={string.Join(',', pair.Value)}"));
            hints.Add($"Verified offline NuGet cache for {profile}: {pins}. Use cached exact versions; " +
                "an absent dependency requires a compatible profile image, not online restore or architecture substitution. " +
                "Match the entire transitive dependency graph, not just direct references. Host-generated lockfiles may contain " +
                "different versions or Aspire dashboard runtime identifiers than the Linux worker. Use worker-compatible locks, " +
                "or explicitly re-evaluate restore offline in the disposable validation/runtime copy; never rely on a successful host build.");
        }
        return string.Join('\n', hints);
    }

    public IReadOnlyList<AppBuildIssue> Inspect(DisposableFixtureWorkspace workspace, AppBuildManifest? manifest)
    {
        if (manifest is null || !packages.TryGetValue(manifest.ValidationProfile, out var known)) return [];
        var artifacts = new WorkspaceArtifacts(workspace);
        WorkspaceFileEntry[] allFiles = artifacts.ListFiles().ToArray();
        var files = allFiles.Where(file =>
            file.Path.EndsWith(".csproj", StringComparison.Ordinal) || file.Path.EndsWith("Directory.Packages.props", StringComparison.Ordinal))
            .Where(file => !file.Path.Split('/').Any(part => part is "bin" or "obj" or ".github" or ".squad"));
        var documents = new List<(string Path, XDocument Document)>();
        foreach (WorkspaceFileEntry file in files)
        {
            if (file.Size > WorkspaceArtifacts.MaxTextBytes)
                return [new("dependency-manifest-size", "A dependency manifest is too large to verify.", file.Path)];
            try
            {
                documents.Add((file.Path, XDocument.Parse(new WorkspaceArtifacts(workspace).ReadFile(file.Path)?.Text
                    ?? throw new InvalidOperationException("A dependency manifest could not be read."))));
            }
            catch (System.Xml.XmlException)
            {
                return [new("dependency-manifest-invalid", "A dependency manifest is invalid.", file.Path)];
            }
        }
        var issues = new List<AppBuildIssue>();
        foreach ((string path, XDocument document) in documents)
            foreach (XElement reference in document.Descendants().Where(node => node.Name.LocalName == "PackageReference"))
            {
                if (reference.AncestorsAndSelf().Any(node => node.Attribute("Condition") is not null)) continue;
                string? name = reference.Attribute("Include")?.Value;
                if (name is null) continue;
                string? version = reference.Attribute("VersionOverride")?.Value ?? reference.Attribute("Version")?.Value ??
                    reference.Elements().FirstOrDefault(node => node.Name.LocalName == "Version")?.Value;
                string scope = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
                while (version is null)
                {
                    string centralPath = scope.Length == 0 ? "Directory.Packages.props" : scope + "/Directory.Packages.props";
                    XDocument? props = documents.FirstOrDefault(item => item.Path == centralPath).Document;
                    if (props is not null)
                    {
                        XElement[] pins = props.Descendants().Where(node => node.Name.LocalName == "PackageVersion" &&
                            string.Equals(node.Attribute("Include")?.Value, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (pins.Length == 1 && !pins[0].AncestorsAndSelf().Any(node => node.Attribute("Condition") is not null))
                            version = pins[0].Attribute("Version")?.Value;
                        break;
                    }
                    if (scope.Length == 0) break;
                    scope = Path.GetDirectoryName(scope)?.Replace('\\', '/') ?? "";
                }
                if (version is not null && Regex.IsMatch(version, @"\A[0-9][a-zA-Z0-9.+-]*\z") &&
                    (!known.TryGetValue(name, out string[]? available) || !available.Contains(version, StringComparer.OrdinalIgnoreCase)))
                    issues.Add(new("dependency-not-cached", "A pinned dependency is missing from this app's offline validation image. Install a compatible profile before validation.", path));
            }
        bool locked = documents.Any(item => item.Document.Descendants().Any(node =>
            node.Name.LocalName == "RestoreLockedMode" && node.Value.Equals("true", StringComparison.OrdinalIgnoreCase))) ||
            new[] { "tests/run.sh", "tests/serve.sh" }.Any(path =>
                artifacts.ReadFile(path)?.Text?.Contains("--locked-mode", StringComparison.Ordinal) == true);
        if (locked)
            foreach (WorkspaceFileEntry file in allFiles.Where(file => file.Path.EndsWith("packages.lock.json", StringComparison.Ordinal)))
            {
                if (file.Size > WorkspaceArtifacts.MaxTextBytes)
                    return [new("dependency-manifest-size", "A dependency lockfile is too large to verify.", file.Path)];
                try
                {
                    using JsonDocument document = JsonDocument.Parse(artifacts.ReadFile(file.Path)?.Text
                        ?? throw new InvalidOperationException("A dependency lockfile could not be read."));
                    if (!document.RootElement.TryGetProperty("dependencies", out JsonElement frameworks) ||
                        frameworks.ValueKind != JsonValueKind.Object)
                        return [new("dependency-manifest-invalid", "A dependency lockfile is invalid.", file.Path)];
                    foreach (JsonProperty framework in frameworks.EnumerateObject().Where(item => !item.Name.Contains('/')))
                    {
                        if (framework.Value.ValueKind != JsonValueKind.Object)
                            return [new("dependency-manifest-invalid", "A dependency lockfile is invalid.", file.Path)];
                        foreach (JsonProperty package in framework.Value.EnumerateObject())
                        {
                            if (package.Value.ValueKind != JsonValueKind.Object ||
                                !package.Value.TryGetProperty("resolved", out JsonElement resolved) ||
                                resolved.ValueKind != JsonValueKind.String) continue;
                            string? version = resolved.GetString();
                            if (!known.TryGetValue(package.Name, out string[]? available) ||
                                !available.Contains(version, StringComparer.OrdinalIgnoreCase))
                                issues.Add(new("dependency-lock-not-cached",
                                    "A locked dependency is absent from the worker cache. Use cache-compatible locks or explicitly re-evaluate offline restore in the disposable copy.",
                                    file.Path));
                        }
                    }
                }
                catch (JsonException)
                {
                    return [new("dependency-manifest-invalid", "A dependency lockfile is invalid.", file.Path)];
                }
            }
        return issues.Distinct().ToArray();
    }
}
