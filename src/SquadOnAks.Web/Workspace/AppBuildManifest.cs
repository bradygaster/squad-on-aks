using System.Text.Json;
using System.Text.Json.Serialization;

namespace SquadOnAks.Web.Workspace;

/// <summary>A rule of the app build contract was broken. Messages are fixed, plain sentences that name the rule.</summary>
public sealed class AppBuildContractException(string message) : InvalidOperationException(message);

public sealed record AppRuntimeSpec(
    IReadOnlyList<string> HealthPaths,
    IReadOnlyList<AppBrowserJourney> Journeys,
    string Database = "none",
    string StartScript = "tests/serve.sh",
    IReadOnlyList<AppBrowserJourney>? RestartJourneys = null);

public sealed record AppBuildManifest(
    int SchemaVersion,
    string Architecture,
    string ValidationProfile,
    AppRuntimeSpec? Runtime = null)
{
    public const string FileName = "app.build.json";
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Example(string architecture = "server", string profile = "dotnet", string database = "none")
    {
        var assertion = new AppBrowserStep("text", "[data-testid=results]", "Acceptance proof");
        var journey = new AppBrowserJourney("Save a result",
            [new("fill", "[data-testid=input]", "Acceptance proof"), new("click", "[data-testid=save]"), assertion]);
        return JsonSerializer.Serialize(new AppBuildManifest(1, architecture, profile,
            new(["/health/ready"], [journey], database, RestartJourneys:
                database == "none" ? null : [new("Read saved result after restart", [assertion])])), json);
    }

    public static AppBuildManifest? Read(DisposableFixtureWorkspace workspace)
    {
        string path = workspace.PathPolicy.ResolveArtifactPath(FileName);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 64 * 1024)
            throw new InvalidOperationException("The app build contract exceeds its size limit.");
        AppBuildManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AppBuildManifest>(File.ReadAllText(path), json)
                ?? throw new InvalidOperationException("The app build contract is empty.");
        }
        catch (JsonException exception)
        {
            // The JSON path is built from the contract's own field names; name it only when it has that bounded shape.
            if (exception.Path is { } location && System.Text.RegularExpressions.Regex.IsMatch(location, @"\A\$(?:\.[A-Za-z][A-Za-z0-9]{0,40}|\[[0-9]{1,3}\]){1,8}\z"))
                throw new AppBuildContractException($"Remove the field the contract doesn't define, or fix its value, at {location}.");
            throw new InvalidOperationException("The app build contract is invalid. Ask the team to correct app.build.json.", exception);
        }
        manifest.Validate(workspace);
        return manifest;
    }

    public void Validate(DisposableFixtureWorkspace workspace)
    {
        if (SchemaVersion != 1 || Architecture is not ("static" or "server" or "aspire") ||
            string.IsNullOrWhiteSpace(ValidationProfile) ||
            ValidationProfile.Length > 40 ||
            ValidationProfile.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new AppBuildContractException("The app build contract needs schemaVersion 1, a supported architecture and validation profile.");

        if (Runtime is null)
        {
            if (Architecture != "static")
                throw new AppBuildContractException("A server-backed app needs a runtime, readiness paths and browser journeys.");
            return;
        }
        if (Runtime.Database is not ("none" or "sqlite" or "postgres") ||
            Runtime.StartScript != "tests/serve.sh" ||
            Runtime.HealthPaths is not { Count: >= 1 and <= 8 } ||
            Runtime.HealthPaths.Any(path => !ValidHttpPath(path)) ||
            Runtime.Journeys is not { Count: >= 1 and <= 8 })
            throw new AppBuildContractException("The runtime needs bounded readiness paths, browser journeys, and a supported database.");
        _ = workspace.PathPolicy.ResolveArtifactPath(Runtime.StartScript);
        if (Runtime.RestartJourneys?.Count > 8 ||
            Runtime.Database != "none" && Runtime.RestartJourneys is not { Count: >= 1 and <= 8 })
            throw new AppBuildContractException("A database-backed app needs browser journeys that verify persistence after restart.");
        foreach (AppBrowserJourney journey in Runtime.Journeys.Concat(Runtime.RestartJourneys ?? []))
        {
            if (journey is null || string.IsNullOrWhiteSpace(journey.Name) || journey.Name.Length > 100 ||
                journey.Steps is not { Count: >= 1 and <= 40 } ||
                !journey.Steps.Any(step => step?.Action is "text" or "visible" or "count") ||
                Runtime.Journeys.Contains(journey) && !journey.Steps.Any(step => step?.Action is "click" or "fill" or "press"))
                throw new AppBuildContractException("Each browser journey needs user interaction and an observable result.");
            foreach (AppBrowserStep step in journey.Steps)
            {
                if (step is null || step.Action is not ("click" or "fill" or "press" or "text" or "visible" or "count" or "reload") ||
                    step.Action != "reload" && string.IsNullOrWhiteSpace(step.Selector) || step.Selector?.Length > 300 ||
                    step.Value?.Length > 1000 ||
                    step.Action is "fill" or "press" or "text" && step.Value is null ||
                    step.Action == "count" && (!int.TryParse(step.Value, out int count) || count < 0 || count > 1000))
                    throw new AppBuildContractException("A browser journey contains an unsupported or unbounded step.");
            }
            if (Runtime.RestartJourneys?.Contains(journey) == true &&
                (journey.Steps.Any(step => step.Action is not ("text" or "count" or "visible" or "reload")) ||
                !journey.Steps.Any(step => step.Action is "text" or "count" && step.Selector is not ("body" or "html" or "*") &&
                    !string.IsNullOrWhiteSpace(step.Value) &&
                    (step.Action != "count" || int.Parse(step.Value) > 0) &&
                    Runtime.Journeys.SelectMany(before => before.Steps)
                        .Any(before => before.Action == step.Action && before.Selector == step.Selector && before.Value == step.Value))))
                throw new AppBuildContractException("Restart journeys may only use text, count, visible and reload, and must read back an asserted result of the original journey without recreating it.");
        }
    }

    public static bool ValidHttpPath(string? path) =>
        path is { Length: > 0 and <= 300 } && path.StartsWith('/') &&
        !path.StartsWith("//", StringComparison.Ordinal) &&
        !path.Any(c => char.IsControl(c) || c is '\\' or '#' or '?' or '%') &&
        path.Split('/').All(segment => segment is not ("." or ".."));
}

public sealed record AppBuildCapabilities(
    int DotnetMajor,
    IReadOnlyList<string> ValidationProfiles,
    bool RuntimeAvailable,
    bool PostgresAvailable = false,
    bool ClusterBuild = false);
