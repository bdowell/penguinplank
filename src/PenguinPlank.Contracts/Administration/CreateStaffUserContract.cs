namespace PenguinPlank.Contracts.Administration;

/// <summary>
/// The versioned request body an Owner submits to provision a staff account (requirement A2 §5.9,
/// §5.10).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). This is the only
/// account-creation surface — there is no public self-registration (requirement A2 §5.9). The
/// password is validated against the configured Identity policy server-side and is never returned
/// in any response.
/// </remarks>
public sealed record CreateStaffUserContract
{
    /// <summary>The new account's email (and username).</summary>
    public required string Email { get; init; }

    /// <summary>The initial password; validated against the Identity policy server-side.</summary>
    public required string Password { get; init; }

    /// <summary>The role to assign as a stable string ("Owner" or "Staff").</summary>
    public required string Role { get; init; }
}
