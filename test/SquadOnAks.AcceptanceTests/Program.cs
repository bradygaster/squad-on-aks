using SquadOnAks.AcceptanceTests;

string layer = Arguments.GetValue(args, "--layer") ?? "deterministic";
string? baseUrl = Arguments.GetValue(args, "--base-url");

IAcceptanceLayer acceptanceLayer = layer switch
{
    "deterministic" => new DeterministicAcceptanceLayer(),
    "native-casting" => new NativeCastingAcceptanceLayer(baseUrl),
    "live-local" => new LiveAcceptanceLayer(
        "live-local",
        "SQUAD_ACCEPTANCE_LIVE_LOCAL",
        baseUrl ?? Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_BASE_URL")),
    "live-aks" => new LiveAcceptanceLayer(
        "live-aks",
        "SQUAD_ACCEPTANCE_LIVE_AKS",
        baseUrl ?? Environment.GetEnvironmentVariable("SQUAD_ACCEPTANCE_BASE_URL")),
    _ => throw new ArgumentException($"Unknown acceptance layer '{layer}'.")
};

await acceptanceLayer.RunAsync();

internal static class Arguments
{
    public static string? GetValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
