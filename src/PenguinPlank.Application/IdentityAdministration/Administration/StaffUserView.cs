using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The Application read model for a staff account (requirement A2 §5.9, §5.10): the account's id,
/// email, and authorization role.
/// </summary>
/// <remarks>
/// A role-safe projection of a staff account that deliberately carries <b>no credential</b> — no
/// password hash, no security stamp, no lockout secret. It is mapped from the ASP.NET Core Identity
/// user in Infrastructure; no Identity entity crosses the Application boundary (coding-standards
/// §3). The user-administration endpoints are Owner-only (requirement A2 §5.10).
/// </remarks>
public sealed record StaffUserView
{
    /// <summary>The account's stable identifier.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The account's email (and username).</summary>
    public required string Email { get; init; }

    /// <summary>The authorization role the account carries (Owner or Staff).</summary>
    public required Role Role { get; init; }
}
