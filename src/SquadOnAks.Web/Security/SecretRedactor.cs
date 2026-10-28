namespace SquadOnAks.Web.Security;

public sealed class SecretRedactor(params string?[] secrets)
{
    public const string Replacement = "[REDACTED]";

    private readonly string[] values = secrets.Where(secret => !string.IsNullOrEmpty(secret))
        .Select(secret => secret!).Distinct(StringComparer.Ordinal)
        .OrderByDescending(secret => secret.Length).ToArray();

    public string? Redact(string? value)
    {
        foreach (string secret in values)
        {
            value = value?.Replace(secret, Replacement, StringComparison.Ordinal);
        }
        return value;
    }
}
