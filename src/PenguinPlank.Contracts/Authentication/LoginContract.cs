namespace PenguinPlank.Contracts.Authentication;

/// <summary>
/// The versioned request body for a staff login (requirement A5 §7.1).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). On success the server
/// establishes a secure, HttpOnly, same-origin cookie session; no bearer token is returned to the
/// browser (requirement A5 §7.2). The password is never logged or echoed back.
/// </remarks>
public sealed record LoginContract
{
    /// <summary>The staff member's email (and username).</summary>
    public required string Email { get; init; }

    /// <summary>The staff member's password.</summary>
    public required string Password { get; init; }
}
