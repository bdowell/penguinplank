namespace PenguinPlank.Contracts.Administration;

/// <summary>
/// The versioned transport shape returned for a staff account on the Owner-only <c>/users</c>
/// surface (requirement A2 §5.9, §5.10).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). It deliberately carries
/// <b>no credential</b> — no password hash, no security stamp, no lockout secret — only the
/// account's id, email, and role as a stable string ("Owner" or "Staff").
/// </remarks>
public sealed record StaffUserResponse
{
    /// <summary>The account's stable identifier.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The account's email (and username).</summary>
    public required string Email { get; init; }

    /// <summary>The authorization role as a stable string ("Owner" or "Staff").</summary>
    public required string Role { get; init; }
}
