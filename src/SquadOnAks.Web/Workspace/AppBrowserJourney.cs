namespace SquadOnAks.Web.Workspace;

// Shared with the cluster journey runner (src/SquadOnAks.Worker/journeys), which links this file.
public sealed record AppBrowserStep(string Action, string Selector = "", string? Value = null);
public sealed record AppBrowserJourney(string Name, IReadOnlyList<AppBrowserStep> Steps);
