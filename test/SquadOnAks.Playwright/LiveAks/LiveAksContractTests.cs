namespace SquadOnAks.Playwright.LiveAks;

public sealed class LiveAksContractTests
{
    [Fact(Skip = "Opt in with an explicit deployed public URL and kubectl context.")]
    [Trait("Layer", "LiveAks")]
    public void PublicJourneyAndPodReplacement()
    {
    }
}
