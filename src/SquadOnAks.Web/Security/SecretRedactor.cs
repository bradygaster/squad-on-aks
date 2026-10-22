namespace SquadOnAks.Web.Security;

public sealed class SecretRedactor(string secret)
{
    public const string Replacement = "[REDACTED]";

    public string? Redact(string? value) =>
        value is null || string.IsNullOrEmpty(secret)
            ? value
            : value.Replace(secret, Replacement, StringComparison.Ordinal);
}
