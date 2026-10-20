namespace SquadOnAks.Playwright.LiveLocal;

public sealed class LiveLocalContractTests
{
    [Fact(Skip = "Opt in after starting the real app with `aspire start --isolated`.")]
    [Trait("Layer", "LiveLocal")]
    public void NativeCastingAndFixtureWork()
    {
    }
}
