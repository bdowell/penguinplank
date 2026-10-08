namespace PenguinPlank.Domain.Common;

/// <summary>
/// Pure, directly-testable helpers that translate a mutable aggregate's opaque
/// <c>rowversion</c> concurrency token to and from an HTTP entity tag (ETag), and compare
/// two tokens for equality.
/// </summary>
/// <remarks>
/// <para>
/// A mutable aggregate (<see cref="VersionedEntity"/>) carries a SQL <c>rowversion</c>
/// exposed to clients as a <b>strong</b> HTTP ETag on reads. On an edit the client echoes the
/// token back in an <c>If-Match</c> header; the server performs a conditional
/// <c>UPDATE ... WHERE rowversion = @ifMatch</c> and refuses a stale version rather than
/// silently overwriting newer state (design "ETag / If-Match optimistic concurrency";
/// requirements A4 §6.5, §6.6). This type owns only the <em>encoding</em>: converting the
/// byte token to a header value and parsing it back. It performs no I/O, so it is tested with
/// ordinary values and needs no database or application startup (coding-standards §1).
/// </para>
/// <para>
/// <b>Encoding choice — Base64, strong validator.</b> The ETag payload is Base64
/// (<see cref="System.Convert.ToBase64String(byte[])"/>) rather than hexadecimal. Both are
/// lossless for an opaque 8-byte SQL <c>rowversion</c>; Base64 is chosen because it is the
/// idiomatic .NET encoding for a byte token, is more compact than hex, and round-trips through
/// <see cref="System.Convert.FromBase64String(string)"/> without a custom parser. The value is
/// wrapped in double quotes per the HTTP grammar for an entity-tag and emitted as a
/// <b>strong</b> validator (no <c>W/</c> weak prefix): the token identifies an exact row
/// version, so byte-for-byte equality is the required comparison for <c>If-Match</c>. Because
/// the token is opaque, clients must treat the whole header value as a round-trip token and
/// never parse its contents.
/// </para>
/// </remarks>
public static class ETag
{
    private const char QuoteCharacter = '"';
    private const string WeakPrefix = "W/";

    /// <summary>
    /// Encodes a <c>rowversion</c> token as a strong HTTP ETag header value, including the
    /// surrounding double quotes (for example <c>"AAAAAAAAB9E="</c>).
    /// </summary>
    /// <param name="rowVersion">
    /// The opaque concurrency token from <see cref="VersionedEntity.RowVersion"/>. An empty
    /// array encodes to an empty-quoted tag (<c>""</c>), representing a row that has not yet
    /// been assigned a version.
    /// </param>
    /// <returns>A strong entity-tag string suitable for an HTTP <c>ETag</c> response header.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="rowVersion"/> is <see langword="null"/>.
    /// </exception>
    public static string Encode(byte[] rowVersion)
    {
        System.ArgumentNullException.ThrowIfNull(rowVersion);

        return string.Concat(
            QuoteCharacter,
            System.Convert.ToBase64String(rowVersion),
            QuoteCharacter);
    }

    /// <summary>
    /// Parses an <c>If-Match</c> header value back into the <c>rowversion</c> byte token.
    /// </summary>
    /// <remarks>
    /// Accepts the value with or without its surrounding double quotes and tolerates a leading
    /// weak-validator <c>W/</c> prefix and surrounding whitespace, so a client that echoes the
    /// exact <c>ETag</c> it received round-trips cleanly. A wildcard (<c>*</c>) is <b>not</b> a
    /// specific version token and is rejected here; a caller that wants to allow
    /// <c>If-Match: *</c> (edit regardless of version) must handle that header case before
    /// calling this method.
    /// </remarks>
    /// <param name="ifMatch">The raw <c>If-Match</c> header value supplied by the client.</param>
    /// <param name="rowVersion">
    /// When this method returns <see langword="true"/>, the decoded concurrency token;
    /// otherwise an empty array.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="ifMatch"/> is a well-formed strong token
    /// that decoded successfully; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? ifMatch, out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return false;
        }

        string candidate = ifMatch.Trim();

        if (candidate.StartsWith(WeakPrefix, System.StringComparison.Ordinal))
        {
            candidate = candidate[WeakPrefix.Length..].Trim();
        }

        // A wildcard is not a specific version token; reject it here.
        if (candidate == "*")
        {
            return false;
        }

        if (candidate.Length >= 2
            && candidate[0] == QuoteCharacter
            && candidate[^1] == QuoteCharacter)
        {
            candidate = candidate[1..^1];
        }

        try
        {
            rowVersion = System.Convert.FromBase64String(candidate);
            return true;
        }
        catch (System.FormatException)
        {
            // A malformed (non-Base64) If-Match value is caller input, not a programming
            // error, so it is reported as a parse failure rather than thrown.
            rowVersion = [];
            return false;
        }
    }

    /// <summary>
    /// Compares two <c>rowversion</c> tokens for exact (strong) equality.
    /// </summary>
    /// <remarks>
    /// Used to decide whether a client's parsed <c>If-Match</c> token still matches the
    /// aggregate's current version before issuing the conditional update. Two
    /// <see langword="null"/> tokens are treated as equal; a <see langword="null"/> and a
    /// non-<see langword="null"/> token are not.
    /// </remarks>
    /// <param name="left">The first token (for example the client's parsed <c>If-Match</c>).</param>
    /// <param name="right">The second token (for example the current row version).</param>
    /// <returns><see langword="true"/> when both tokens are byte-for-byte equal.</returns>
    public static bool Matches(byte[]? left, byte[]? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.AsSpan().SequenceEqual(right);
    }
}
