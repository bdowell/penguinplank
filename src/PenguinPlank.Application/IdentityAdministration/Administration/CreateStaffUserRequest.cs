using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// A request to provision a staff account by an Owner (requirement A2 §5.9, §5.10).
/// </summary>
/// <remarks>
/// Staff accounts are created only by an Owner through this Owner-only path — there is no public
/// self-registration (requirement A2 §5.9). The password is validated against the configured
/// Identity policy by the implementation; it is never logged or returned. The requested
/// <see cref="Role"/> is one of the two Phase A roles.
/// </remarks>
public sealed record CreateStaffUserRequest
{
    /// <summary>Creates the request.</summary>
    /// <param name="email">The new account's email (and username).</param>
    /// <param name="password">The initial password; validated against the Identity policy.</param>
    /// <param name="role">The authorization role to assign.</param>
    public CreateStaffUserRequest(string? email, string? password, Role role)
    {
        Email = email;
        Password = password;
        Role = role;
    }

    /// <summary>The new account's email (and username).</summary>
    public string? Email { get; }

    /// <summary>The initial password. Never logged or returned to a caller.</summary>
    public string? Password { get; }

    /// <summary>The authorization role to assign (Owner or Staff).</summary>
    public Role Role { get; }
}
