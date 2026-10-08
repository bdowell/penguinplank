using Microsoft.Net.Http.Headers;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Reads and validates the two mutation headers the catalog write endpoints require: the
/// <c>Idempotency-Key</c> on every command (requirement A4 §6.2) and the <c>If-Match</c> ETag on a
/// mutable-aggregate edit (requirements 6.5, 6.6).
/// </summary>
/// <remarks>
/// These are small, pure header accessors so the endpoints stay thin (coding-standards §3). Each
/// returns whether the header was present and well-formed, letting the endpoint translate a
/// missing or malformed header into a <c>400</c> validation ProblemDetails using the same error
/// surface as every other failure, rather than a raw framework fault.
/// </remarks>
public static class MutationHeaders
{
    /// <summary>The request header carrying the caller's idempotency key for a mutating command.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Reads the required <c>Idempotency-Key</c> header value.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="idempotencyKey">The non-empty key when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a non-empty key was supplied.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public static bool TryGetIdempotencyKey(HttpContext httpContext, out string? idempotencyKey)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out Microsoft.Extensions.Primitives.StringValues values))
        {
            string value = values.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                idempotencyKey = value;
                return true;
            }
        }

        idempotencyKey = null;
        return false;
    }

    /// <summary>
    /// Reads the required <c>If-Match</c> header as the opaque concurrency token the edit must
    /// match. The surrounding ETag quotes are stripped so the value matches the token a prior read
    /// returned.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="expectedVersion">The unquoted token when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a non-empty <c>If-Match</c> value was supplied.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public static bool TryGetIfMatch(HttpContext httpContext, out string? expectedVersion)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Headers.TryGetValue(HeaderNames.IfMatch, out Microsoft.Extensions.Primitives.StringValues values))
        {
            string value = values.ToString();
            string unquoted = Unquote(value);
            if (!string.IsNullOrWhiteSpace(unquoted))
            {
                expectedVersion = unquoted;
                return true;
            }
        }

        expectedVersion = null;
        return false;
    }

    /// <summary>
    /// Formats an opaque concurrency token as a quoted strong HTTP <c>ETag</c> header value.
    /// </summary>
    /// <param name="eTagToken">The opaque token from a read model.</param>
    /// <returns>The quoted ETag value to place on the response.</returns>
    public static string ToETagHeader(string eTagToken)
    {
        System.ArgumentNullException.ThrowIfNull(eTagToken);
        return $"\"{eTagToken}\"";
    }

    private static string Unquote(string value)
    {
        string trimmed = value.Trim();

        // Tolerate a weak-validator prefix and surrounding quotes so a client that echoes a strong
        // or weak ETag verbatim still matches the stored token.
        if (trimmed.StartsWith("W/", System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..].Trim();
        }

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }
}
