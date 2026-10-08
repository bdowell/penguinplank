namespace PenguinPlank.Contracts.Authentication;

/// <summary>
/// The versioned transport shape describing the current authenticated staff actor, returned by
/// login and by the "who am I" endpoint (requirement A5 §7.1, §7.3).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3) that carries no credential —
/// only the authenticated account's id, email, and role as a stable string ("Owner" or "Staff").
/// The browser relies on the HttpOnly cookie for the session and never stores a bearer token
/// (requirement A5 §7.2).
/// </remarks>
public sealed record CurrentActorResponse
{
    /// <summary>The authenticated account's stable identifier.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The authenticated account's email (and username).</summary>
    public required string Email { get; init; }

    /// <summary>The authorization role as a stable string ("Owner" or "Staff").</summary>
    public required string Role { get; init; }
}
