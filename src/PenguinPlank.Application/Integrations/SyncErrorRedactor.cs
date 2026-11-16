using System.Text.RegularExpressions;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure function producing a safe-to-display error summary for the sync exception queue
/// (requirement 3.12). It removes bearer tokens, credential-like key/value pairs, email addresses,
/// and long opaque token strings, then bounds the length.
/// </summary>
public static partial class SyncErrorRedactor
{
    /// <summary>The maximum length of a redacted error (matches the persisted column width).</summary>
    public const int MaximumLength = 2000;

    /// <summary>The text substituted for removed content.</summary>
    public const string RedactionMarker = "[redacted]";

    /// <summary>The text used when no error detail is supplied.</summary>
    public const string UnknownError = "Unknown error.";

    private const int RegexTimeoutMilliseconds = 250;

    /// <summary>Redacts a raw provider or exception message.</summary>
    /// <param name="rawError">The raw message, possibly containing secrets or personal data.</param>
    /// <returns>A non-empty redacted message of at most <see cref="MaximumLength"/> characters.</returns>
    public static string Redact(string? rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
        {
            return UnknownError;
        }

        string redacted = BearerPattern().Replace(rawError, "Bearer " + RedactionMarker);
        redacted = CredentialPairPattern().Replace(redacted, "$1=" + RedactionMarker);
        redacted = EmailPattern().Replace(redacted, RedactionMarker);
        redacted = OpaqueTokenPattern().Replace(redacted, RedactionMarker);
        redacted = redacted.Trim();

        return redacted.Length <= MaximumLength ? redacted : redacted[..MaximumLength];
    }

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(
        @"(token|secret|password|passwd|api[_-]?key|authorization|credential|signature)\s*[:=]\s*\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeoutMilliseconds)]
    private static partial Regex CredentialPairPattern();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b[A-Za-z0-9_\-+/=]{32,}\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex OpaqueTokenPattern();
}
