using SquadOnAks.Web.Workspace;

namespace SquadOnAks.Web.Work;

public sealed class ProfiledFixtureValidationRunner(
    IFixtureValidationRunner fallback,
    IReadOnlyDictionary<string, IFixtureValidationRunner> profiles) : IFixtureValidationRunner
{
    public Task<FixtureValidationResult> RunAsync(
        DisposableFixtureWorkspace workspace, string appId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        AppBuildManifest? manifest = AppBuildManifest.Read(workspace);
        if (manifest is null) return fallback.RunAsync(workspace, appId, timeout, cancellationToken);
        if (!profiles.TryGetValue(manifest.ValidationProfile, out IFixtureValidationRunner? runner))
            throw new InvalidOperationException("This app's dependency validation profile is not installed.");
        return runner.RunAsync(workspace, appId, timeout, cancellationToken);
    }
}
