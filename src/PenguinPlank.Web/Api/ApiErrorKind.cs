namespace PenguinPlank.Web.Api;

/// <summary>
/// The category of failure a typed API call can report, so UI code can branch without parsing
/// status codes or error-code strings.
/// </summary>
public enum ApiErrorKind
{
    /// <summary>The request was rejected as invalid (HTTP 400).</summary>
    Validation,

    /// <summary>The session is missing or expired, or credentials were refused (HTTP 401).</summary>
    Unauthenticated,

    /// <summary>The signed-in actor may not perform the operation (HTTP 403).</summary>
    Forbidden,

    /// <summary>The target record does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>The request conflicts with existing state, such as a duplicate key (HTTP 409).</summary>
    Conflict,

    /// <summary>
    /// The edit targeted a stale version (HTTP 412). The caller must refresh and re-apply the edit
    /// rather than overwrite newer state (requirements A4 6.5, 6.6).
    /// </summary>
    StaleVersion,

    /// <summary>An upload exceeded an allowed bound (HTTP 413).</summary>
    PayloadTooLarge,

    /// <summary>The server is rate limiting the caller (HTTP 429).</summary>
    RateLimited,

    /// <summary>
    /// The server could not be reached (network loss, DNS failure, or timeout). The mutation was
    /// not confirmed and is never queued for later replay (requirement A5 7.7).
    /// </summary>
    Connectivity,

    /// <summary>The server reported an unexpected failure (HTTP 5xx).</summary>
    Server,

    /// <summary>The response was not understood or had an unmapped status.</summary>
    Unexpected,
}
