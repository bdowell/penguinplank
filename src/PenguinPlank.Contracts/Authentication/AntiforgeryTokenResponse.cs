namespace PenguinPlank.Contracts.Authentication;

/// <summary>
/// The versioned transport shape carrying a CSRF (antiforgery) request token the browser echoes on
/// a mutating request (requirement A5 §7.2).
/// </summary>
/// <remarks>
/// <para>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). The browser fetches this
/// token and sends it back in the configured antiforgery request header on every POST/PATCH/DELETE;
/// the server validates it so a cross-site request that carries the session cookie but not a valid
/// token is rejected (CSRF protection on mutations). The matching antiforgery <em>cookie</em> is
/// set by the server as an HttpOnly cookie as part of issuing the token; the two together are what
/// the validation checks.
/// </para>
/// </remarks>
public sealed record AntiforgeryTokenResponse
{
    /// <summary>The CSRF request token value to send back in the antiforgery header on mutations.</summary>
    public required string RequestToken { get; init; }

    /// <summary>The name of the HTTP header the request token must be sent in.</summary>
    public required string HeaderName { get; init; }
}
